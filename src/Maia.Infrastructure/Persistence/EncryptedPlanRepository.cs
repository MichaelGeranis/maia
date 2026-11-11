using System.Security.Cryptography;
using System.Text.Json;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Infrastructure.Security;

namespace Maia.Infrastructure.Persistence;

/// <summary>
/// Stores the plan as one encrypted JSON file in the app's private data folder.
/// Writes are durable and atomic (a temp file flushed to disk, then renamed into place) and the previous version is kept
/// as a backup copy, so a crash or power cut mid-write can never leave the parent without her plan.
/// <para>Screens and the reminders each load the plan, often for the same change. The file is still read
/// every time, but while it holds the very bytes read or written last, the plan comes from what they held: it is not
/// decrypted or parsed again. Each load still gets a plan of its own to change.</para>
/// </summary>
public sealed class EncryptedPlanRepository : IPlanRepository
{
    public const string FileName = "plan.maia";
    public const string BackupFileName = "plan.maia.bak";

    /// <summary>Reported (once) when the main file can't be read and the backup copy is loaded instead.</summary>
    public const string FallbackOperation = "plan.load.fallback";

    /// <summary>Starts what is reported (once per kind) when a stored record had to be repaired on loading.</summary>
    public const string RepairOperationPrefix = "plan.load.repair.";

    private const string Purpose = "plan";

    private readonly string _directory;
    private readonly AesGcmCipher _cipher;
    private readonly IErrorReporter? _errors;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    // Set when the main file failed to load and the plan came from the backup copy: the next save must keep that copy.
    private volatile bool _mainIsDamaged;

    // The main file as last read or written, and the plan it holds: while the file still holds exactly these bytes, a
    // load builds the plan from this instead of decrypting and parsing it again.
    private volatile ReadPlan? _last;

    /// <param name="directory">The folder the plan lives in.</param>
    /// <param name="cipher">Encrypts with the phone's data key.</param>
    /// <param name="errors">Receives codes (never contents) for a fallback to the backup copy or a repaired record.</param>
    public EncryptedPlanRepository(string directory, AesGcmCipher cipher, IErrorReporter? errors = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _cipher = cipher;
        _errors = errors;
    }

    private string PlanPath => Path.Combine(_directory, FileName);

    private string BackupPath => Path.Combine(_directory, BackupFileName);

    /// <inheritdoc />
    public bool LastLoadUsedPreviousCopy { get; private set; }

    /// <summary>Whether a plan is stored in <paramref name="directory"/>, readable or not.</summary>
    public static bool HasStoredPlan(string directory) =>
        File.Exists(Path.Combine(directory, FileName)) || File.Exists(Path.Combine(directory, BackupFileName));

    /// <summary>
    /// Loads the plan. If the main file is damaged (for example a torn write on an old OS), falls back to the backup
    /// copy rather than losing everything, and reports it. A plan saved by a newer Maia is not damage: it is refused,
    /// and nothing falls back to an older copy.
    /// </summary>
    /// <exception cref="PlanUnreadableException">
    /// A plan is stored but can't be opened: its key is gone, both copies are damaged, or a newer Maia saved it.
    /// </exception>
    public async Task<PregnancyPlan?> LoadAsync(CancellationToken cancellationToken)
    {
        LastLoadUsedPreviousCopy = false;
        var hasMain = File.Exists(PlanPath);
        var hasBackup = File.Exists(BackupPath);
        if (!hasMain && !hasBackup)
        {
            return null;
        }

        try
        {
            return hasMain
                ? await ReadMainAsync(hasBackup, cancellationToken).ConfigureAwait(false)
                : await ReadBackupOnlyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DataKeyUnavailableException ex)
        {
            throw PlanUnreadableException.KeyUnavailable(ex);
        }
        catch (NewerPlanSchemaException ex)
        {
            throw PlanUnreadableException.FromNewerApp(ex);
        }
        catch (Exception ex) when (IsDamage(ex))
        {
            throw PlanUnreadableException.Damaged(ex);
        }
    }

    /// <summary>The main file, or the backup copy when the main file is damaged and there is one.</summary>
    private async Task<PregnancyPlan> ReadMainAsync(bool hasBackup, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await ReadAsync(PlanPath, cancellationToken).ConfigureAwait(false);
            _mainIsDamaged = false;
            return plan;
        }
        catch (Exception ex) when (IsDamage(ex) && hasBackup)
        {
            var fallback = await ReadAsync(BackupPath, cancellationToken).ConfigureAwait(false);
            _mainIsDamaged = true;
            LastLoadUsedPreviousCopy = true;
            ReportOnce(ex, FallbackOperation);
            return fallback;
        }
    }

    /// <summary>Only the backup copy is left, as when a save stopped between its two renames.</summary>
    private async Task<PregnancyPlan> ReadBackupOnlyAsync(CancellationToken cancellationToken)
    {
        var backup = await ReadAsync(BackupPath, cancellationToken).ConfigureAwait(false);
        LastLoadUsedPreviousCopy = true;
        ReportOnce(new FileNotFoundException("The main plan file is missing."), FallbackOperation);
        return backup;
    }

    public async Task SaveAsync(PregnancyPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Directory.CreateDirectory(_directory);

        var document = PlanDocumentMapper.ToDocument(plan);
        var json = JsonSerializer.SerializeToUtf8Bytes(document, PersistenceJsonContext.Default.PlanDocument);
        var envelope = await _cipher.EncryptAsync(json, Purpose, cancellationToken).ConfigureAwait(false);

        var tempPath = Path.Combine(_directory, $"{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await DurableFile.WriteAsync(tempPath, envelope, cancellationToken).ConfigureAwait(false);
            if (File.Exists(PlanPath) && !_mainIsDamaged)
            {
                // The current file becomes the backup copy.
                File.Replace(tempPath, PlanPath, BackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                // The first save, or the plan came from the backup copy because the main file is damaged: keep that
                // good copy rather than rotating the damaged file into its place.
                File.Move(tempPath, PlanPath, overwrite: true);
            }

            _last = new ReadPlan(envelope, document);
            _mainIsDamaged = false;
            ForgetReports();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>Deletes the plan, its backup copy and any temp file an interrupted save left behind.</summary>
    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (Directory.Exists(_directory))
        {
            foreach (var path in Directory.EnumerateFiles(_directory, FileName + "*").ToList())
            {
                File.Delete(path);
            }
        }

        _last = null;
        _mainIsDamaged = false;
        ForgetReports();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether a read failed because of what is in the file: it doesn't decrypt (torn, tampered with, or another key's),
    /// isn't a plan, or holds a record the domain can't take. Not a missing key, a newer Maia, cancellation or an I/O error.
    /// </summary>
    private static bool IsDamage(Exception ex) =>
        ex is not ObjectDisposedException
        && ex is CryptographicException or InvalidDataException or JsonException or DomainException or ArgumentException
            or FormatException or InvalidOperationException;

    private async Task<PregnancyPlan> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var envelope = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        PlanDocument document;
        if (path == PlanPath && _last is { } last && envelope.AsSpan().SequenceEqual(last.Envelope))
        {
            // The very file read or written last: only a new copy of the plan is made from what it held.
            document = last.Document;
        }
        else
        {
            var json = await _cipher.DecryptAsync(envelope, Purpose, cancellationToken).ConfigureAwait(false);
            document = PlanDocumentMigrator.Read(json);
            if (path == PlanPath)
            {
                _last = new ReadPlan(envelope, document);
            }
        }

        var repairs = new List<string>();
        var plan = PlanDocumentMapper.ToDomain(document, repairs);
        foreach (var code in repairs)
        {
            ReportOnce(new InvalidDataException("A stored record was repaired on loading."), RepairOperationPrefix + code);
        }

        return plan;
    }

    /// <summary>Reports each kind of problem once, not on every load, until the next save or delete.</summary>
    private void ReportOnce(Exception exception, string operation)
    {
        if (_errors is null)
        {
            return;
        }

        lock (_reported)
        {
            if (!_reported.Add(operation))
            {
                return;
            }
        }

        _errors.Report(exception, operation);
    }

    private void ForgetReports()
    {
        lock (_reported)
        {
            _reported.Clear();
        }
    }

    /// <summary>The main file's encrypted bytes and the plan they hold. Never changed: each load maps a new plan from it.</summary>
    private sealed record ReadPlan(byte[] Envelope, PlanDocument Document);
}
