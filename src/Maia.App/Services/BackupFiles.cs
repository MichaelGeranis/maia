using Maia.Presentation.Localization;
using Maia.Presentation.Settings;

namespace Maia.App.Services;

/// <summary>
/// Backup files on the phone: a new backup is written into a private cache folder and handed to the share sheet
/// (Files, email, Drive…); restoring picks one with the phone's file picker. The file is encrypted either way.
/// </summary>
public sealed class MauiBackupFiles : IBackupFiles
{
    private static string Folder => Path.Combine(FileSystem.CacheDirectory, "backup");

    /// <summary>Removes backup files left in the cache from earlier sharing.</summary>
    public static void Clear()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }

    public async Task ShareAsync(string fileName, Func<Stream, Task> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        Clear();
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, fileName);
        await using (var file = File.Create(path))
        {
            await write(file);
        }

        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = Text.Get("Backup_Title"),
            File = new ShareFile(path, "application/octet-stream"),
        }));
    }

    public async Task<BackupFile?> PickAsync()
    {
        // Backups arrive through email, Drive or Files with any media type, so every file is offered;
        // the reader says plainly if one isn't a Maia backup.
        var picked = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickAsync(new PickOptions { PickerTitle = Text.Get("Restore_Choose") }));
        return picked is null ? null : new BackupFile(picked.FileName, picked.OpenReadAsync);
    }
}
