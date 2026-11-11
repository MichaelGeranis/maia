using Maia.Domain.Common;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Results;

/// <summary>
/// Draws result files inside Maia, so a decrypted report or photo is never handed to another app, which could keep
/// its own copy or list it in its recent files. Implemented by the app.
/// </summary>
public interface IResultRenderer
{
    /// <summary>Whether this phone can show a photo of this type itself (HEIC needs Android 9).</summary>
    bool CanShowImage(string mediaType);

    /// <summary>Opens a PDF to draw its pages.</summary>
    /// <returns>Its pages, or null when this phone can't draw it (password-protected or damaged).</returns>
    Task<IPdfPages?> OpenPdfAsync(byte[] pdf);
}

/// <summary>An open PDF. Disposing it closes it and lets go of the decrypted content.</summary>
public interface IPdfPages : IDisposable
{
    /// <summary>Each page's height divided by its width, in page order.</summary>
    IReadOnlyList<double> Ratios { get; }

    /// <summary>Draws one page, <paramref name="width"/> pixels wide, as an encoded picture (a JPEG on Android).</summary>
    Task<byte[]> RenderAsync(int index, int width);
}

/// <summary>
/// Which result the in-app viewer shows next, and whether it can: PDFs and the photo types this phone can draw.
/// Register as a singleton; the viewer takes the request once.
/// </summary>
public sealed class ResultViewing
{
    private readonly IResultRenderer _renderer;
    private readonly INavigator _navigator;
    private AttachmentId? _pending;

    public ResultViewing(IResultRenderer renderer, INavigator navigator)
    {
        _renderer = renderer;
        _navigator = navigator;
    }

    public static bool IsPdf(string? mediaType) => string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether Maia draws a file of this type itself. The viewer explains anything else, and hands it to another app
    /// only when she asks.
    /// </summary>
    public bool CanShow(string? mediaType) =>
        IsPdf(mediaType) || (mediaType is not null && PhotoPolicy.IsPhoto(mediaType) && _renderer.CanShowImage(mediaType));

    /// <summary>Opens the viewer on one result.</summary>
    public Task ShowAsync(AttachmentId result)
    {
        _pending = result;
        return _navigator.GoToResultViewerAsync();
    }

    internal AttachmentId? Take()
    {
        var pending = _pending;
        _pending = null;
        return pending;
    }
}
