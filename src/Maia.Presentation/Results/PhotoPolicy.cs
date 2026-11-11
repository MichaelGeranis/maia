namespace Maia.Presentation.Results;

/// <summary>
/// How photos are kept (ADR 0002). A photo whose long edge is over <see cref="LongEdge"/> pixels, or a HEIC photo,
/// is stored as a new JPEG at quality <see cref="JpegQuality"/>, turned upright and with no metadata. A smaller JPEG,
/// PNG or WebP is kept as it is, with any location removed from its metadata. PDFs are never changed.
/// </summary>
public static class PhotoPolicy
{
    /// <summary>Longest side kept, in pixels: a paper report stays easy to read, at a fraction of a camera's size.</summary>
    public const int LongEdge = 3000;

    public const int JpegQuality = 90;

    public static bool IsPhoto(string mediaType) =>
        mediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsJpeg(string mediaType) =>
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a photo of this type and size is stored as a new, smaller JPEG.</summary>
    public static bool NeedsNewJpeg(string mediaType, PhotoSize size) =>
        size.LongEdge > LongEdge || !(IsJpeg(mediaType) || IsType(mediaType, "image/png") || IsType(mediaType, "image/webp"));

    /// <summary>"scan.heic" becomes "scan.jpg"; a name that already ends in .jpg or .jpeg stays.</summary>
    public static string JpegName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var extension = Path.GetExtension(name);
        return string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
            ? name
            : Path.ChangeExtension(name, ".jpg");
    }

    private static bool IsType(string mediaType, string type) => string.Equals(mediaType, type, StringComparison.OrdinalIgnoreCase);
}
