using System.Security.Cryptography;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Plans;

namespace Maia.Presentation.Results;

/// <summary>
/// A small picture of a result for its tile on Results: a photo made small, or a PDF's first page. Made in memory, one
/// result at a time (so only one decrypted file is held at once), and never written anywhere; the tile keeps it for as
/// long as the result is in her plan. A file the phone can't draw keeps its tile's icon. Register as a singleton.
/// </summary>
public sealed class ResultThumbnails : IDisposable
{
    /// <summary>How wide a thumbnail is drawn, in pixels: sharp on a tile, a few tens of kilobytes.</summary>
    public const int Width = 360;

    private const int Quality = 80;

    private readonly PlanService _plans;
    private readonly IPhotoProcessor _photos;
    private readonly IResultRenderer _renderer;
    private readonly IErrorReporter _errors;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    public ResultThumbnails(PlanService plans, IPhotoProcessor photos, IResultRenderer renderer, IErrorReporter errors)
    {
        _plans = plans;
        _photos = photos;
        _renderer = renderer;
        _errors = errors;
    }

    /// <returns>The thumbnail as an encoded picture, or null when this file can't be drawn (it keeps its icon).</returns>
    public async Task<byte[]?> MakeAsync(ResultAttachment result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var mediaType = result.MediaType ?? string.Empty;
        var pdf = ResultViewing.IsPdf(mediaType);
        if (result.BlobId is null || (!pdf && !(PhotoPolicy.IsPhoto(mediaType) && _renderer.CanShowImage(mediaType))))
        {
            return null;
        }

        await _oneAtATime.WaitAsync().ConfigureAwait(false);
        try
        {
            var content = await _plans.ReadResultFileAsync(result).ConfigureAwait(false);
            if (!pdf)
            {
                return await _photos.ToJpegAsync(new PickedFile(result.DisplayName, mediaType, content), Width, Quality).ConfigureAwait(false);
            }

            using var pages = await _renderer.OpenPdfAsync(content).ConfigureAwait(false);
            return pages is { Ratios.Count: > 0 } ? await pages.RenderAsync(0, Width).ConfigureAwait(false) : null;
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or InvalidDataException)
        {
            // The stored file is gone or damaged: the tile keeps its icon, and only the code is reported.
            _errors.Report(ex, "results.thumbnail");
            return null;
        }
#pragma warning disable CA1031 // A tile's picture is never worth an error on screen: the icon stays; the code is reported.
        catch (Exception ex)
        {
            _errors.Report(ex, "results.thumbnail");
            return null;
        }
#pragma warning restore CA1031
        finally
        {
            _oneAtATime.Release();
        }
    }

    public void Dispose() => _oneAtATime.Dispose();
}
