using Maia.Application.Common;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Settings;

/// <summary>A backup file she picked: its name, and a way to read it.</summary>
public sealed record BackupFile(string Name, Func<Task<Stream>> OpenAsync);

/// <summary>The phone's side of backups. Implemented by the app.</summary>
public interface IBackupFiles
{
    /// <summary>Writes a backup into a private cache file through <paramref name="write"/>, then opens the share sheet for it.</summary>
    Task ShareAsync(string fileName, Func<Stream, Task> write);

    /// <returns>The file she picked, or null if she cancelled.</returns>
    Task<BackupFile?> PickAsync();
}

/// <summary>Calm, plain words for why a backup could not be opened.</summary>
public static class BackupMessages
{
    public static string For(BackupUnreadableException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Text.Get(error.Code switch
        {
            BackupUnreadableException.WrongPasswordCode => "Backup_WrongPassword",
            BackupUnreadableException.NotABackupCode => "Backup_NotABackup",
            BackupUnreadableException.NewerAppCode => "Backup_NewerApp",
            _ => "Backup_Damaged",
        });
    }
}
