namespace Maia.Presentation.Results;

/// <summary>
/// A file she picked: a PDF report or a photo of one. Nothing is read until <see cref="OpenAsync"/>, so a file Maia
/// can't keep is turned away before it is loaded, and files are read one at a time.
/// </summary>
/// <param name="Name">Its name, as the phone shows it.</param>
/// <param name="MediaType">Its type, such as <c>application/pdf</c> or <c>image/jpeg</c>.</param>
/// <param name="Size">Its size in bytes, when the phone knows it.</param>
/// <param name="OpenAsync">Opens it for reading. It may be called more than once.</param>
public sealed record PickedFile(string Name, string MediaType, long? Size, Func<Task<Stream>> OpenAsync)
{
    /// <summary>A file already in memory.</summary>
    public PickedFile(string name, string mediaType, byte[] content)
        : this(name, mediaType, content?.Length, () => Task.FromResult<Stream>(new MemoryStream(content ?? [], writable: false)))
    {
    }

    /// <summary>Removes any copy the phone made for Maia (a camera photo), once the file is filed or turned away.</summary>
    public Action? Cleanup { get; init; }
}

/// <summary>The phone's file and photo picker (PDFs and images), and its camera. Implemented by the app.</summary>
public interface IResultPicker
{
    /// <summary>Whether the phone has a camera app to photograph a paper report with.</summary>
    bool CanTakePhoto { get; }

    /// <returns>The files she picked; empty if she cancelled.</returns>
    Task<IReadOnlyList<PickedFile>> PickAsync();

    /// <summary>Opens the phone's camera app for one photo. Maia needs no camera permission: the camera app takes it.</summary>
    /// <returns>The photo she took, or null if she cancelled.</returns>
    /// <exception cref="CameraUnavailableException">There is no camera app, or the camera is switched off on this phone.</exception>
    Task<PickedFile?> TakePhotoAsync();
}

/// <summary>The phone can't take a photo right now: no camera app, or the camera is switched off.</summary>
public sealed class CameraUnavailableException : Exception
{
    public CameraUnavailableException()
        : base("The camera is not available.")
    {
    }

    public CameraUnavailableException(string message)
        : base(message)
    {
    }

    public CameraUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A photo's size in pixels, the way up it is meant to be seen.</summary>
public readonly record struct PhotoSize(int Width, int Height)
{
    public int LongEdge => Math.Max(Width, Height);
}

/// <summary>
/// The pixel work for keeping photos (ADR 0002): reading a photo's size, and writing a smaller JPEG. Implemented by
/// the app; <see cref="PhotoPolicy"/> decides when.
/// </summary>
public interface IPhotoProcessor
{
    /// <returns>Its size, turned upright; null when the phone can't read the picture.</returns>
    Task<PhotoSize?> MeasureAsync(PickedFile photo);

    /// <summary>
    /// A new JPEG of the photo: turned upright, its long edge at most <paramref name="longEdge"/> pixels, at
    /// <paramref name="quality"/>, and with no metadata at all (so no location).
    /// </summary>
    Task<byte[]> ToJpegAsync(PickedFile photo, int longEdge, int quality);

    /// <summary>
    /// The same photo (a JPEG, PNG or WebP kept as it is) with any location in its metadata removed; the picture is not
    /// touched.
    /// </summary>
    byte[] WithoutLocation(string mediaType, byte[] photo);
}

/// <summary>A result she chose to share: its name, its type, and a way to read (decrypt) its file when its turn comes.</summary>
public sealed record ResultToShare(string Name, string MediaType, Func<Task<byte[]>> ReadAsync);

/// <summary>Hands decrypted results to another app: one to view, or several to share. Implemented by the app.</summary>
public interface IResultViewer
{
    Task OpenFileAsync(string name, string mediaType, byte[] content);

    /// <summary>
    /// Opens the phone's share sheet with <paramref name="results"/> (email, WhatsApp, Viber…). Each is decrypted, one at
    /// a time, into Maia's private cache, only for the app she picks to read; the copies go once she is back in Maia.
    /// </summary>
    Task ShareFilesAsync(IReadOnlyList<ResultToShare> results);

    /// <summary>
    /// After "Delete everything" or "Start again": removes every plaintext copy in Maia's cache, internal and external.
    /// That is files decrypted for viewing, MAUI's copies of picked and shared files (its FilePicker and MediaPicker
    /// folder, <c>CacheDir/&lt;hash&gt;</c>), a camera photo not yet filed and a backup being shared.
    /// </summary>
    void ClearCopies();
}
