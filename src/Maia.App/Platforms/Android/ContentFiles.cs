using Android.Content;
using Android.Provider;
using Android.Webkit;
using Maia.Application.Plans;
using Maia.Presentation.Results;
using AndroidUri = Android.Net.Uri;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Turns a file another app hands Maia (the document picker, a share) into a <see cref="PickedFile"/>: its name, type
/// and size come from the provider, and its content is read straight from it when needed. Nothing is copied to disk,
/// so no plaintext copy of her result is left in Maia's cache.
/// </summary>
internal static class ContentFiles
{
    public static PickedFile From(Context context, AndroidUri uri)
    {
        var resolver = context.ContentResolver ?? throw new InvalidOperationException("No content resolver.");
        string? name = null;
        long? size = null;
        try
        {
            using var cursor = resolver.Query(uri, [IOpenableColumns.DisplayName, IOpenableColumns.Size], null, null, null);
            if (cursor is not null && cursor.MoveToFirst())
            {
                name = cursor.IsNull(0) ? null : cursor.GetString(0);
                size = cursor.IsNull(1) ? null : cursor.GetLong(1);
            }
        }
        catch (Java.Lang.Exception)
        {
            // A plain file:// link, or a provider without these columns: the name comes from the path below.
        }

        name = string.IsNullOrWhiteSpace(name) ? uri.LastPathSegment ?? "result" : name;
        return new PickedFile(name, MediaTypeOf(resolver.GetType(uri), name), size, () => OpenAsync(resolver, uri));
    }

    /// <summary>The provider's type, or one from the name when the provider's isn't one Maia keeps ("image/jpg", "application/octet-stream").</summary>
    private static string MediaTypeOf(string? reported, string name)
    {
        var type = Normalize(reported);
        if (type is not null && AttachmentPolicy.IsAllowed(type))
        {
            return type;
        }

        var extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        return Normalize(MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension)) ?? type ?? "application/octet-stream";
    }

    private static string? Normalize(string? type) => type?.ToLowerInvariant() switch
    {
        null or "" => null,
        "image/jpg" or "image/pjpeg" => "image/jpeg",
        var other => other,
    };

    private static Task<Stream> OpenAsync(ContentResolver resolver, AndroidUri uri)
    {
        try
        {
            return Task.FromResult(resolver.OpenInputStream(uri) ?? throw new IOException("The file could not be opened."));
        }
        catch (Java.IO.IOException ex)
        {
            throw new IOException("The file could not be opened.", ex);
        }
        catch (Java.Lang.SecurityException ex)
        {
            throw new UnauthorizedAccessException("Maia may no longer read this file.", ex);
        }
    }
}
