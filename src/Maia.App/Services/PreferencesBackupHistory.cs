using Maia.Presentation.Settings;

namespace Maia.App.Services;

/// <summary>
/// When she last kept a backup and put the reminder away, in the app's preferences: two timestamps (UTC ticks), nothing
/// about her plan.
/// </summary>
public sealed class PreferencesBackupHistory : IBackupHistory
{
    private const string LastBackupKey = "backup.last";
    private const string DismissedKey = "backup.reminder_dismissed";

    public DateTimeOffset? LastBackup => Read(LastBackupKey);

    public DateTimeOffset? ReminderDismissed => Read(DismissedKey);

    public void BackupMade(DateTimeOffset at) => Preferences.Default.Set(LastBackupKey, at.UtcTicks);

    public void DismissReminder(DateTimeOffset at) => Preferences.Default.Set(DismissedKey, at.UtcTicks);

    public void Forget()
    {
        Preferences.Default.Remove(LastBackupKey);
        Preferences.Default.Remove(DismissedKey);
    }

    private static DateTimeOffset? Read(string key)
    {
        var ticks = Preferences.Default.Get(key, 0L);
        return ticks > 0 && ticks <= DateTimeOffset.MaxValue.UtcTicks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
    }
}
