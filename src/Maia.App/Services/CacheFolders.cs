namespace Maia.App.Services;

/// <summary>
/// Plaintext copies of her files in Maia's cache, and removing them: a file decrypted for another app to open
/// (<c>open</c>), MAUI's own copies of files it shares or picks (its Essentials folders, in the internal and the
/// external cache), a camera photo not yet filed, a backup being shared. The whole cache is emptied at start-up and
/// after "Delete everything"; what was handed to another app is removed as soon as she comes back to Maia.
/// </summary>
public static class CacheFolders
{
    /// <summary>The folders MAUI Essentials copies shared and picked files into (Microsoft.Maui.Essentials 10).</summary>
    private static readonly string[] EssentialsFolders = ["2203693cc04e0be7f4f024d5f9499e13", "0d1705adc4a6b4ec531e316ec956755a"];

    private static int _handedOut;

    /// <summary>Folder for a file decrypted so another app can open it.</summary>
    public static string OpenFolder => Path.Combine(FileSystem.CacheDirectory, "open");

    /// <summary>
    /// Sends Launcher and Share copies to the internal cache, which only Maia can read, instead of the external cache
    /// that apps with storage access could read on older Android versions. Call before any Launcher or Share call.
    /// </summary>
    public static void KeepSharedCopiesInternal()
    {
#if ANDROID
        FileProvider.TemporaryLocation = FileProviderLocation.Internal;
#endif
    }

    /// <summary>Marks that a decrypted file was just handed to another app.</summary>
    public static void HandedOut() => Interlocked.Exchange(ref _handedOut, 1);

    /// <summary>
    /// When she comes back to Maia from another app's viewer: removes the file that was handed out, and MAUI's copies
    /// of it. Nothing happens when nothing was handed out (coming back from the camera or a picker, say).
    /// </summary>
    public static void ClearAfterViewing()
    {
        if (Interlocked.Exchange(ref _handedOut, 0) == 0)
        {
            return;
        }

        Delete(OpenFolder);
        foreach (var root in Roots())
        {
            foreach (var folder in EssentialsFolders)
            {
                Delete(Path.Combine(root, folder));
            }
        }
    }

    /// <summary>Empties Maia's cache, internal and external: at start-up and after "Delete everything".</summary>
    public static void ClearAll()
    {
        Interlocked.Exchange(ref _handedOut, 0);
        foreach (var root in Roots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(root))
            {
                if (Directory.Exists(entry))
                {
                    Delete(entry);
                }
                else
                {
                    DeleteFile(entry);
                }
            }
        }
    }

    /// <summary>Deletes a copy the phone made in Maia's cache (a picked or photographed file) once Maia has read it.</summary>
    public static void Forget(string? path)
    {
        if (!string.IsNullOrEmpty(path) && IsInside(path, FileSystem.CacheDirectory))
        {
            DeleteFile(path);
        }
    }

    private static IEnumerable<string> Roots()
    {
        yield return FileSystem.CacheDirectory;
#if ANDROID
        if (Platform.AppContext.ExternalCacheDir?.AbsolutePath is { } external)
        {
            yield return external;
        }
#endif
    }

    private static void Delete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A file still open elsewhere: it goes at the next start.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Still open elsewhere: it goes at the next start.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private static bool IsInside(string path, string folder)
    {
        var full = Path.GetFullPath(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal);
    }
}
