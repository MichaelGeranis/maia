using Maia.Presentation.Results;

namespace Maia.App.Services;

/// <summary>
/// The phone's file picker, limited to what Maia keeps: PDF reports and photos. Files are handed over unread, so
/// <see cref="ResultFiling"/> checks each one's type and size first and reads them one at a time.
/// </summary>
public sealed class MauiFilePicker : IResultPicker
{
    private static readonly FilePickerFileType ReportsAndPhotos = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = ["application/pdf", "image/*"],
        [DevicePlatform.iOS] = ["com.adobe.pdf", "public.image"],
        [DevicePlatform.WinUI] = [".pdf", ".jpg", ".jpeg", ".png", ".heic", ".webp"],
    });

#if ANDROID
    public bool CanTakePhoto => Platforms.Android.CameraCapture.IsAvailable;

    public Task<PickedFile?> TakePhotoAsync() => Platforms.Android.CameraCapture.TakeAsync();
#else
    public bool CanTakePhoto => MediaPicker.Default.IsCaptureSupported;

    public async Task<PickedFile?> TakePhotoAsync()
    {
        var photo = await MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.CapturePhotoAsync());
        return photo is null
            ? null
            : new PickedFile(photo.FileName, photo.ContentType, null, photo.OpenReadAsync) { Cleanup = () => CacheFolders.Forget(photo.FullPath) };
    }
#endif

    public async Task<IReadOnlyList<PickedFile>> PickAsync()
    {
#if ANDROID
        // Read straight from the provider: MAUI's picker would first copy every file, in plaintext, into the cache.
        return await Platforms.Android.DocumentPicker.PickAsync();
#else
        var picked = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickMultipleAsync(new PickOptions { FileTypes = ReportsAndPhotos }));
        return (picked ?? []).OfType<FileResult>()
            .Select(file => new PickedFile(file.FileName, file.ContentType, null, file.OpenReadAsync) { Cleanup = () => CacheFolders.Forget(file.FullPath) })
            .ToList();
#endif
    }
}

/// <summary>Where the phone can't size or re-encode a photo, photos are kept as picked, with any location in a JPEG, PNG or WebP removed.</summary>
public sealed class PlainPhotoProcessor : IPhotoProcessor
{
    public Task<PhotoSize?> MeasureAsync(PickedFile photo) => Task.FromResult<PhotoSize?>(null);

    public Task<byte[]> ToJpegAsync(PickedFile photo, int longEdge, int quality) =>
        throw new NotSupportedException("Photos are not re-encoded on this platform.");

    public byte[] WithoutLocation(string mediaType, byte[] photo) => Maia.Infrastructure.Photos.PhotoLocation.Remove(mediaType, photo);
}

/// <summary>
/// Opens results Maia doesn't draw itself (see <see cref="ResultViewing"/>) in the phone's viewer. The file is decrypted into Maia's private cache only for that app to open: it is removed, with MAUI's own
/// copies of it, as soon as she comes back to Maia, and in any case at the next start (see <see cref="CacheFolders"/>).
/// What the other app keeps (its recent files, its own copy) is outside Maia's reach; the viewer says so.
/// </summary>
public sealed class MauiResultViewer : IResultViewer
{
    public async Task OpenFileAsync(string name, string mediaType, byte[] content)
    {
        var folder = CacheFolders.OpenFolder;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);
        var safeName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(folder, string.IsNullOrWhiteSpace(safeName) ? "result" : safeName);
        await File.WriteAllBytesAsync(path, content);
        CacheFolders.HandedOut();
#if ANDROID
        await MainThread.InvokeOnMainThreadAsync(() => Platforms.Android.OutsideViewer.Open(path, mediaType, name));
#else
        await MainThread.InvokeOnMainThreadAsync(() => Launcher.Default.OpenAsync(new OpenFileRequest(name, new ReadOnlyFile(path, mediaType))));
#endif
    }

    /// <summary>
    /// The share sheet with the results she chose. Each is decrypted in turn into its own folder in Maia's private cache
    /// (two results may share a name), under a name with its file ending, for the app she picks to read; they are removed
    /// as soon as she comes back to Maia, and in any case at the next start (see <see cref="CacheFolders"/>).
    /// </summary>
    public async Task ShareFilesAsync(IReadOnlyList<ResultToShare> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var folder = CacheFolders.OpenFolder;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        var files = new List<(string Path, string MediaType)>(results.Count);
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var own = Path.Combine(folder, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(own);
            var path = Path.Combine(own, ShareFiles.FileName(result.Name, result.MediaType));
            CacheFolders.HandedOut();
            await File.WriteAllBytesAsync(path, await result.ReadAsync());
            files.Add((path, result.MediaType));
        }

        var title = Maia.Presentation.Localization.Text.Get("Results_ShareTitle");
#if ANDROID
        await MainThread.InvokeOnMainThreadAsync(() => Platforms.Android.ResultSharer.Share(files, title));
#else
        CacheFolders.KeepSharedCopiesInternal();
        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(
            new ShareMultipleFilesRequest(title, files.Select(f => new ShareFile(f.Path, f.MediaType)).ToList())));
#endif
    }

    /// <summary>After "Delete everything": the whole cache goes, not only what was opened.</summary>
    public void ClearCopies() => CacheFolders.ClearAll();
}

/// <summary>Where the phone can't draw PDFs inside Maia, they open in its own viewer; common photo types are drawn here.</summary>
public sealed class PlainResultRenderer : IResultRenderer
{
    public bool CanShowImage(string mediaType) => mediaType is "image/jpeg" or "image/png" or "image/webp";

    public Task<IPdfPages?> OpenPdfAsync(byte[] pdf) => Task.FromResult<IPdfPages?>(null);
}
