using Maia.Domain.Plans;
using Maia.Domain.Templates;

namespace Maia.Application.Abstractions;

/// <summary>Current time and the parent's time zone. Injected so every rule is testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    TimeZoneInfo TimeZone { get; }

    /// <summary>Today's date in <see cref="TimeZone"/>.</summary>
    DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, TimeZone).DateTime);

    /// <summary>Now in <see cref="TimeZone"/>, to the minute: where a new booking or task starts.</summary>
    DateTime Now
    {
        get
        {
            var now = TimeZoneInfo.ConvertTime(UtcNow, TimeZone).DateTime;
            return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMinute));
        }
    }
}

/// <summary>Stores the single plan kept on this device.</summary>
public interface IPlanRepository
{
    /// <returns>The plan, or null when none is stored.</returns>
    /// <exception cref="Common.PlanUnreadableException">A plan is stored but can't be opened (see its code).</exception>
    Task<PregnancyPlan?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(PregnancyPlan plan, CancellationToken cancellationToken);

    /// <summary>Deletes the plan, its backup copy and anything an interrupted save left behind.</summary>
    Task DeleteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the last load had to fall back to the previous copy because the newest one was unreadable. The newest
    /// copy may point at result files the previous one doesn't know, so nothing is tidied away after such a load.
    /// </summary>
    bool LastLoadUsedPreviousCopy { get; }
}

/// <summary>
/// The key that encrypts her plan and result files. It lives in the phone's keystore and never leaves it (ADR 0002).
/// </summary>
public interface IDataKey
{
    /// <summary>
    /// Forgets the key, so no copy of her files left on this phone can be decrypted any more. Call it only once her files
    /// are gone: a new key is made the next time Maia saves something.
    /// </summary>
    Task ForgetAsync(CancellationToken cancellationToken);
}

/// <summary>Plan templates shipped with the app, one per country.</summary>
public interface IPlanTemplateCatalog
{
    IReadOnlyList<string> SupportedCountries { get; }

    Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken);
}

/// <summary>Encrypted storage for result files. Blob ids are opaque to callers.</summary>
public interface IAttachmentStore
{
    Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    Task<byte[]> ReadAsync(string blobId, CancellationToken cancellationToken);

    /// <summary>Deletes a blob. Deleting a missing blob is not an error.</summary>
    Task DeleteAsync(string blobId, CancellationToken cancellationToken);

    /// <summary>Deletes every stored blob, including any left behind by a crash.</summary>
    Task DeleteAllAsync(CancellationToken cancellationToken);

    /// <summary>Every stored blob and when it was written, to find the ones no plan points at.</summary>
    Task<IReadOnlyList<StoredBlob>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>A stored result file: its opaque id and when it was written.</summary>
public sealed record StoredBlob(string BlobId, DateTimeOffset SavedAt);

/// <summary>
/// Receives failures from background reactions (such as setting reminders or tidying files) that must not fail the
/// user's action. The app implements it with logging and a small problem log on the phone (see
/// <see cref="Common.ProblemReport"/>): operation codes and exception types only, never health data.
/// </summary>
public interface IErrorReporter
{
    void Report(Exception exception, string operation);

    /// <summary>
    /// Removes the reports kept on this phone, as part of "Delete everything". A reporter that keeps nothing has
    /// nothing to remove.
    /// </summary>
    void Forget()
    {
    }
}

/// <summary>
/// The encrypted backup file she can keep and restore on another phone (ADR 0005). The key comes from a password
/// she chooses; the device key never leaves the keystore.
/// </summary>
public interface IPlanBackupFormat
{
    /// <summary>
    /// Writes the plan and every result file, read through <paramref name="readFile"/> (by blob id). When
    /// <paramref name="readFile"/> returns null the file can't be read (missing or damaged): it is left out, and so is
    /// its result in the plan inside the backup, so the backup still restores.
    /// </summary>
    /// <returns>The blob ids of the files that were left out.</returns>
    Task<IReadOnlySet<string>> WriteAsync(Stream output, PregnancyPlan plan, Func<string, Task<byte[]?>> readFile, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a backup. Each result file goes to <paramref name="saveFile"/>, which returns its new blob id; the plan
    /// comes back pointing at the new ids.
    /// </summary>
    /// <exception cref="Common.BackupUnreadableException">Wrong password, not a backup, damaged, or from a newer app.</exception>
    Task<PregnancyPlan> ReadAsync(Stream input, string password, Func<ReadOnlyMemory<byte>, Task<string>> saveFile, CancellationToken cancellationToken);
}

/// <summary>
/// Small notes Maia keeps on the phone before they belong to a plan, such as her setup choices while the plan is not
/// made yet, so they outlive Android closing Maia in the background. Encrypted like the plan (ADR 0002).
/// </summary>
public interface IDraftStore
{
    /// <returns>The draft saved under <paramref name="name"/>, or null when there is none or it can't be read any more.</returns>
    Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken);

    Task WriteAsync(string name, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Deletes the draft. Deleting a missing one is not an error.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken);
}
