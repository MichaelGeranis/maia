namespace Maia.Presentation.Settings;

/// <summary>
/// When she last kept a backup (made the file and said it is saved, or restored her plan from one), and when she last
/// put the backup reminder away.
/// Only these two timestamps are kept (in the app's preferences): nothing about the plan or the file. Implemented by
/// the app.
/// </summary>
public interface IBackupHistory
{
    DateTimeOffset? LastBackup { get; }

    /// <summary>When she last chose "Not now" on the reminder on Your week.</summary>
    DateTimeOffset? ReminderDismissed { get; }

    /// <summary>
    /// She kept a backup file: she said it is saved after the share sheet closed. Making the file alone does not count,
    /// since the sheet can close without it going anywhere. Restoring her plan from a backup counts too: that file holds
    /// her plan as it then stands.
    /// </summary>
    void BackupMade(DateTimeOffset at);

    void DismissReminder(DateTimeOffset at);

    /// <summary>Forgets both, when she deletes her data: a backup of that plan does not cover the next one.</summary>
    void Forget();
}

/// <summary>
/// When Your week reminds her, calmly, to keep a backup: her plan lives only on this phone (Android backs up none of
/// it), so a lost or broken phone would take it with it. Only once there is something worth keeping, when she has never
/// made a backup or the last one is about a month old, and never again for a month after she puts it away.
/// </summary>
public static class BackupReminder
{
    /// <summary>How old a backup (or a "Not now") may be before she is reminded again.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromDays(30);

    public static bool IsDue(bool worthKeeping, DateTimeOffset now, DateTimeOffset? lastBackup, DateTimeOffset? dismissed)
    {
        return worthKeeping && !IsRecent(lastBackup) && !IsRecent(dismissed);

        bool IsRecent(DateTimeOffset? at) => at is { } when && now - when < Every;
    }
}
