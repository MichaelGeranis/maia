using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Domain.Plans;
using Maia.Infrastructure.Persistence;
using Maia.Infrastructure.Security;

namespace Maia.Testing;

/*
 * Fakes for the application's ports. Each one behaves like the adapter the app really uses, minus the disk, the
 * keystore and the phone, so a test that passes here would pass on the phone too. When a real adapter changes how it
 * behaves, change its fake with it.
 */

/// <summary>A clock frozen at a chosen instant, in Athens time unless told otherwise.</summary>
public sealed class FakeClock : IClock
{
    public static readonly TimeZoneInfo Athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");

    public FakeClock(DateOnly today, TimeZoneInfo? timeZone = null)
    {
        TimeZone = timeZone ?? Athens;
        SetToday(today);
    }

    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>The phone's time zone. It can change while Maia runs, as when she travels; the instant stays the same.</summary>
    public TimeZoneInfo TimeZone { get; set; }

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, TimeZone).DateTime);

    /// <summary>Moves the clock to noon local time on <paramref name="today"/>.</summary>
    public void SetToday(DateOnly today) => SetTime(today, new TimeOnly(12, 0));

    /// <summary>Moves the clock to <paramref name="time"/> local time on <paramref name="day"/>.</summary>
    public void SetTime(DateOnly day, TimeOnly time)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        UtcNow = new DateTimeOffset(local, TimeZone.GetUtcOffset(local)).ToUniversalTime();
    }
}

/// <summary>
/// Keeps the plan the way <see cref="EncryptedPlanRepository"/> keeps it on disk: as its stored document, written and
/// read through the real persistence mapping (without the encryption). Every load hands out a fresh plan, so a change
/// that was never saved (a failed save, a command that throws half-way) and its events are gone on the next load, and
/// a field the mapping forgets is lost here too.
/// </summary>
public sealed class InMemoryPlanRepository : IPlanRepository
{
    private byte[]? _document;

    /// <summary>A fresh copy of the stored plan, as the next load sees it; null when there is none.</summary>
    public PregnancyPlan? Stored => _document is null ? null : StoredPlan.Read(_document);

    public int SaveCount { get; private set; }

    /// <summary>How many times the plan was loaded, as each load decrypts and reads the whole file on the phone.</summary>
    public int LoadCount { get; private set; }

    /// <summary>As after a torn write of the newest copy: the plan came from the previous one.</summary>
    public bool LastLoadUsedPreviousCopy { get; set; }

    /// <summary>Makes the next save throw this. The stored plan stays as it was, as on disk.</summary>
    public Exception? FailNextSave { get; set; }

    /// <summary>
    /// Makes every load throw this, as the phone does when a plan is stored but can't be opened (its key is gone, both
    /// copies are damaged, or a newer Maia saved it). Deleting the plan clears it, as deleting the files does.
    /// </summary>
    public PlanUnreadableException? Unreadable { get; set; }

    /// <summary>Makes every load throw this, as when the plan no longer decrypts.</summary>
    public Exception? FailLoads { get; set; }

    /// <summary>Makes the next load throw this, once, as when storage is briefly not ready.</summary>
    public Exception? FailNextLoad { get; set; }

    /// <summary>Runs as each load starts, before anything is read: a test can hold a load there or count them.</summary>
    public Func<Task>? BeforeLoad { get; set; }

    public async Task<PregnancyPlan?> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCount++;
        if (BeforeLoad is { } before)
        {
            await before();
        }

        if (FailNextLoad is { } once)
        {
            FailNextLoad = null;
            throw once;
        }

        return Unreadable is { } unreadable ? throw unreadable
            : FailLoads is { } failure ? throw failure
            : Stored;
    }

    public Task SaveAsync(PregnancyPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var document = StoredPlan.Write(plan);
        if (FailNextSave is { } failure)
        {
            FailNextSave = null;
            throw failure;
        }

        _document = document;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        _document = null;
        Unreadable = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores her plan as an older Maia saved it: the stored document as <paramref name="toOlder"/> changes it (its
    /// <c>schemaVersion</c> and the fields that schema had). Every load then upgrades it, as on the phone, until the
    /// next save.
    /// </summary>
    public void StoreAsSavedBefore(Action<JsonObject> toOlder)
    {
        ArgumentNullException.ThrowIfNull(toOlder);
        var document = JsonNode.Parse(_document ?? throw new InvalidOperationException("No plan is stored.")) as JsonObject
            ?? throw new InvalidOperationException("The stored plan is not a JSON object.");
        toOlder(document);
        _document = Encoding.UTF8.GetBytes(document.ToJsonString());
    }
}

/// <summary>The plan's stored JSON, written and read exactly as <see cref="EncryptedPlanRepository"/> does before and after encryption.</summary>
internal static class StoredPlan
{
    public static byte[] Write(PregnancyPlan plan) =>
        JsonSerializer.SerializeToUtf8Bytes(PlanDocumentMapper.ToDocument(plan), PersistenceJsonContext.Default.PlanDocument);

    public static PregnancyPlan Read(byte[] json) => PlanDocumentMapper.ToDomain(PlanDocumentMigrator.Read(json));
}

/// <summary>
/// The phone's data key, as the plan service sees it: counts how often it was forgotten, and can look at what is left on
/// the phone at that moment.
/// </summary>
public sealed class RecordingDataKey : IDataKey
{
    public int Forgotten { get; private set; }

    /// <summary>Runs as the key is forgotten, for example to check that nothing it opens is left.</summary>
    public Action? WhenForgotten { get; set; }

    public Task ForgetAsync(CancellationToken cancellationToken)
    {
        WhenForgotten?.Invoke();
        Forgotten++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// The phone's keystore in memory. It outlives a restart (a new key provider over the same store) and fails the ways
/// Android's does: a key it can no longer decrypt comes back missing, a read or write can throw, and a write can be
/// dropped without a word.
/// </summary>
public sealed class InMemorySecretStore : ISecretStore
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public Exception? FailReads { get; set; }

    public Exception? FailWrites { get; set; }

    public bool DropWrites { get; set; }

    public Task<string?> GetAsync(string name) =>
        FailReads is { } failure ? Task.FromException<string?>(failure) : Task.FromResult(Values.GetValueOrDefault(name));

    public Task SetAsync(string name, string value)
    {
        if (FailWrites is { } failure)
        {
            return Task.FromException(failure);
        }

        if (!DropWrites)
        {
            Values[name] = value;
        }

        return Task.CompletedTask;
    }

    public void Remove(string name) => Values.Remove(name);
}

/// <summary>
/// Result files in memory, handled like the encrypted store on the phone: 32-character hex ids, a fresh copy on every
/// read, <see cref="FileNotFoundException"/> for a file that is gone, a <see cref="CryptographicException"/> for one
/// that is damaged, and an id the store could not have made is refused.
/// </summary>
public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    public Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

    /// <summary>Files that no longer decrypt, as after a torn write or a flipped bit on disk.</summary>
    public HashSet<string> Damaged { get; } = new(StringComparer.Ordinal);

    /// <summary>When each file was written, by the store's clock (<see cref="Now"/>).</summary>
    public Dictionary<string, DateTimeOffset> SavedAt { get; } = new(StringComparer.Ordinal);

    /// <summary>The time a save is stamped with; tests set it to their clock.</summary>
    public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    public Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        Blobs[id] = content.ToArray();
        SavedAt[id] = Now();
        return Task.FromResult(id);
    }

    public Task<byte[]> ReadAsync(string blobId, CancellationToken cancellationToken)
    {
        EnsureValid(blobId);
        if (Damaged.Contains(blobId))
        {
            throw new AuthenticationTagMismatchException("The stored file does not decrypt.");
        }

        return Blobs.TryGetValue(blobId, out var content)
            ? Task.FromResult(content.ToArray())
            : throw new FileNotFoundException("No stored file has this id.");
    }

    public Task DeleteAsync(string blobId, CancellationToken cancellationToken)
    {
        EnsureValid(blobId);
        Blobs.Remove(blobId);
        SavedAt.Remove(blobId);
        return Task.CompletedTask;
    }

    public Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        Blobs.Clear();
        SavedAt.Clear();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredBlob>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredBlob> blobs = Blobs.Keys.Select(id => new StoredBlob(id, SavedAt.GetValueOrDefault(id, DateTimeOffset.MinValue))).ToList();
        return Task.FromResult(blobs);
    }

    private static void EnsureValid(string blobId)
    {
        if (blobId is not { Length: 32 } || !blobId.All(char.IsAsciiHexDigitLower))
        {
            throw new ArgumentException("Invalid blob id.", nameof(blobId));
        }
    }
}

public sealed class RecordingErrorReporter : IErrorReporter
{
    public List<(Exception Exception, string Operation)> Reports { get; } = [];

    /// <summary>How many times the reports kept on the phone were removed ("Delete everything").</summary>
    public int Forgotten { get; private set; }

    public void Report(Exception exception, string operation) => Reports.Add((exception, operation));

    public void Forget() => Forgotten++;
}

/// <summary>
/// The real backup format with the fewest key-derivation rounds it accepts, so tests stay quick. A restore therefore
/// builds a new plan from the backup, pointing at the ids its files get on this phone, just as on a new phone.
/// </summary>
public sealed class InMemoryBackupFormat : IPlanBackupFormat
{
    private readonly EncryptedBackupFormat _format = new(EncryptedBackupFormat.MinIterations);

    /// <summary>How many backups were written.</summary>
    public int Written { get; private set; }

    public async Task<IReadOnlySet<string>> WriteAsync(Stream output, PregnancyPlan plan, Func<string, Task<byte[]?>> readFile, string password, CancellationToken cancellationToken)
    {
        var skipped = await _format.WriteAsync(output, plan, readFile, password, cancellationToken);
        Written++;
        return skipped;
    }

    public Task<PregnancyPlan> ReadAsync(Stream input, string password, Func<ReadOnlyMemory<byte>, Task<string>> saveFile, CancellationToken cancellationToken) =>
        _format.ReadAsync(input, password, saveFile, cancellationToken);
}

/// <summary>
/// Drafts in memory, kept across a "restart" (a new session over the same store), as the encrypted files on the phone
/// are. Reads and writes can be made to fail.
/// </summary>
public sealed class InMemoryDraftStore : IDraftStore
{
    public Dictionary<string, byte[]> Drafts { get; } = new(StringComparer.Ordinal);

    public Exception? FailWrites { get; set; }

    public Exception? FailReads { get; set; }

    public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) =>
        FailReads is { } failure ? Task.FromException<byte[]?>(failure) : Task.FromResult(Drafts.GetValueOrDefault(name)?.ToArray());

    public Task WriteAsync(string name, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        if (FailWrites is { } failure)
        {
            return Task.FromException(failure);
        }

        Drafts[name] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        Drafts.Remove(name);
        return Task.CompletedTask;
    }
}
