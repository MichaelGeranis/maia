using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Results;

/// <summary>
/// Adding, removing and opening results on Results: what she uploads, photographs or shares into Maia is kept in her
/// results, apart from the entries on her journey. Files are encrypted by <see cref="PlanService"/>; failures come back
/// as a calm message and are reported by code only.
/// </summary>
public sealed class ResultFiling
{
    private const int ReadChunk = 81920;

    private readonly PlanService _plans;
    private readonly IResultPicker _picker;
    private readonly IPhotoProcessor _photos;
    private readonly IResultViewer _viewer;
    private readonly ResultViewing _viewing;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly IErrorReporter _errors;

    public ResultFiling(
        PlanService plans,
        IResultPicker picker,
        IPhotoProcessor photos,
        IResultViewer viewer,
        ResultViewing viewing,
        INotifier notifier,
        IClock clock,
        IErrorReporter errors)
    {
        _plans = plans;
        _picker = picker;
        _photos = photos;
        _viewer = viewer;
        _viewing = viewing;
        _notifier = notifier;
        _clock = clock;
        _errors = errors;
    }

    /// <summary>Whether "Take a photo" can be offered: the phone has a camera app.</summary>
    public bool CanTakePhoto => _picker.CanTakePhoto;

    /// <summary>"Upload results": picks files and keeps them in her results.</summary>
    public async Task<AddedResults> UploadToResultsAsync()
    {
        IReadOnlyList<PickedFile> files;
        try
        {
            files = await _picker.PickAsync();
        }
#pragma warning disable CA1031 // A picker failure is shown calmly; details go to the error reporter.
        catch (Exception ex)
        {
            _errors.Report(ex, "results.pick");
            return new AddedResults([], Text.Get("Common_Error"));
        }
#pragma warning restore CA1031

        return await KeepAsync(files);
    }

    /// <summary>
    /// "Take a photo": opens the camera app and keeps the photo in her results, named for when it was taken
    /// ("Photo Wed 30 Sep, 10:42.jpg").
    /// </summary>
    public async Task<AddedResults> TakePhotoToResultsAsync()
    {
        PickedFile? photo;
        try
        {
            photo = await _picker.TakePhotoAsync();
        }
        catch (CameraUnavailableException)
        {
            return new AddedResults([], Text.Get("Results_CameraUnavailable"));
        }
#pragma warning disable CA1031 // A camera failure is shown calmly; details go to the error reporter.
        catch (Exception ex)
        {
            _errors.Report(ex, "results.camera");
            return new AddedResults([], Text.Get("Common_Error"));
        }
#pragma warning restore CA1031

        if (photo is null)
        {
            return AddedResults.None;
        }

        var now = TimeZoneInfo.ConvertTime(_clock.UtcNow, _clock.TimeZone);
        var name = Text.Format("Results_PhotoName", DisplayFormat.Date(DateOnly.FromDateTime(now.DateTime)), DisplayFormat.Time(TimeOnly.FromDateTime(now.DateTime)));
        return await KeepAsync([photo with { Name = name }]);
    }

    /// <summary>
    /// Keeps <paramref name="files"/> in her results (what she picked, or what another app shared with Maia), one at a
    /// time, so only one is ever held in memory. A file Maia can't keep (not a PDF or photo, over 25 MB, empty, or
    /// unreadable) is turned away before it is read whole and named in the message; the others are still kept. Photos
    /// are made smaller first (<see cref="PhotoPolicy"/>).
    /// </summary>
    public async Task<AddedResults> KeepAsync(IReadOnlyList<PickedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var added = new List<AttachmentId>();
        var skipped = new List<(string Name, Skip Why)>();
        string? failure = null;
        foreach (var file in files)
        {
            try
            {
                if (failure is null)
                {
                    var (id, why) = await AttachAsync(file);
                    if (id is { } kept)
                    {
                        added.Add(kept);
                    }
                    else
                    {
                        skipped.Add((file.Name, why!));
                    }
                }
            }
#pragma warning disable CA1031 // Any other failure (the plan can't be saved) stops here, calmly; details go to the error reporter.
            catch (Exception ex)
            {
                _errors.Report(ex, "results.attach");
                failure = Text.Get("Common_Error");
            }
#pragma warning restore CA1031
            finally
            {
                file.Cleanup?.Invoke();
            }
        }

        if (added.Count > 0)
        {
            _notifier.Show(DisplayFormat.Count("Toast_AddedToResultsOne", "Toast_AddedToResults", added.Count));
        }

        return new AddedResults(added, failure ?? SkippedMessage(files.Count, skipped));
    }

    public Task<string?> RemoveAsync(ResultItem result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return RunAsync(() => _plans.RemoveResultAsync(result.Id), "results.remove");
    }

    /// <summary>
    /// Opens a result in Maia's own viewer, so the decrypted file never reaches
    /// another app unasked. A file the viewer can't draw (a HEIC photo on Android 7 and 8, a protected PDF) gets the
    /// calm note that it opens in another app, which may keep its own copy, and goes there only when she asks.
    /// </summary>
    public async Task<string?> OpenAsync(ResultItem result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return await RunAsync(
            async () =>
            {
                // The viewer reads her plan and the file once.
                await _viewing.ShowAsync(result.Id);
            },
            "results.open");
    }

    /// <summary>
    /// Shares the results she chose through the phone's share sheet. Her plan is read once; each file is decrypted only
    /// as the share sheet's copy is written, one at a time.
    /// </summary>
    /// <returns>Null when the share sheet opened, else a calm line saying it couldn't.</returns>
    public Task<string?> ShareAsync(IReadOnlyCollection<ResultItem> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return RunAsync(
            async () =>
            {
                if (results.Count == 0)
                {
                    return;
                }

                var plan = await _plans.GetPlanAsync();
                var shared = new List<ResultToShare>(results.Count);
                foreach (var result in results)
                {
                    var attachment = plan.FindResult(result.Id);
                    shared.Add(new ResultToShare(
                        attachment.DisplayName,
                        attachment.MediaType ?? "application/octet-stream",
                        () => _plans.ReadResultFileAsync(attachment)));
                }

                await _viewer.ShareFilesAsync(shared);
            },
            "results.share");
    }

    /// <summary>
    /// Reads a file whole, but never past <paramref name="max"/> bytes: a file that turns out larger is dropped as
    /// soon as it passes the limit.
    /// </summary>
    /// <returns>Its bytes, or null when it is larger than <paramref name="max"/>.</returns>
    internal static async Task<byte[]?> ReadAsync(PickedFile file, long max)
    {
        await using var stream = await file.OpenAsync();
        if (stream.CanSeek)
        {
            var length = stream.Length - stream.Position;
            if (length > max)
            {
                return null;
            }

            var buffer = new byte[length];
            await stream.ReadExactlyAsync(buffer);
            return buffer;
        }

        using var memory = new MemoryStream(file.Size is long size && size > 0 && size <= max ? (int)size : 0);
        var chunk = new byte[ReadChunk];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (memory.Length + read > max)
            {
                return null;
            }

            memory.Write(chunk, 0, read);
        }

        return memory.Length == memory.Capacity ? memory.GetBuffer() : memory.ToArray();
    }

    /// <returns>The result it became once kept, else why it was turned away.</returns>
    /// <exception cref="Exception">Saving it failed (the plan or the file store): the caller stops there.</exception>
    private async Task<(AttachmentId? Id, Skip? Why)> AttachAsync(PickedFile file)
    {
        if (!AttachmentPolicy.IsAllowed(file.MediaType))
        {
            return (null, Skip.Type);
        }

        var (name, mediaType) = (file.Name, file.MediaType);
        byte[] content;
        try
        {
            if (PhotoPolicy.IsPhoto(mediaType)
                && await _photos.MeasureAsync(file) is { } size
                && PhotoPolicy.NeedsNewJpeg(mediaType, size))
            {
                content = await _photos.ToJpegAsync(file, PhotoPolicy.LongEdge, PhotoPolicy.JpegQuality);
                (name, mediaType) = (PhotoPolicy.JpegName(name), "image/jpeg");
            }
            else
            {
                if (file.Size > AttachmentPolicy.MaxSizeBytes || await ReadAsync(file, AttachmentPolicy.MaxSizeBytes) is not { } original)
                {
                    return (null, Skip.TooLarge);
                }

                content = PhotoPolicy.IsPhoto(mediaType) && original.Length > 0 ? _photos.WithoutLocation(mediaType, original) : original;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file went away or can't be read (a cloud file while offline, say): skip it, and say which. Only
            // reading her file lands here: Maia failing to save it is not the file's fault, and stops the upload.
            _errors.Report(ex, "results.read");
            return (null, Skip.Unreadable);
        }

        if (content.Length == 0)
        {
            return (null, Skip.Empty);
        }

        try
        {
            return (await _plans.AttachFileAsync(new FileToAttach(name, mediaType, content)), null);
        }
        catch (AttachmentRejectedException ex)
        {
            return (null, ex.Code switch
            {
                "attachment.size.too_large" => Skip.TooLarge,
                "attachment.size.empty" => Skip.Empty,
                _ => Skip.Type,
            });
        }
    }

    /// <summary>
    /// One file picked and turned away: why, in a sentence. Several: "These files couldn't be added: scan.tiff (not a
    /// PDF or photo), video.pdf (over 25 MB)."
    /// </summary>
    private static string? SkippedMessage(int picked, List<(string Name, Skip Why)> skipped)
    {
        if (skipped.Count == 0)
        {
            return null;
        }

        if (picked == 1)
        {
            return Text.Get(skipped[0].Why.Sentence);
        }

        var list = string.Join(", ", skipped.Select(s => $"{s.Name} ({Text.Get(s.Why.Reason)})"));
        return Text.Format(skipped.Count == 1 ? "Results_SkippedOne" : "Results_Skipped", list);
    }

    private async Task<string?> RunAsync(Func<Task> change, string operation)
    {
        try
        {
            await change();
            return null;
        }
        catch (AttachmentRejectedException ex)
        {
            return Text.Get(ex.Code switch
            {
                "attachment.size.too_large" => "Results_TooLarge",
                "attachment.size.empty" => "Results_Empty",
                _ => "Results_Unsupported",
            });
        }
#pragma warning disable CA1031 // Any other failure is shown calmly; details go to the error reporter.
        catch (Exception ex)
        {
            _errors.Report(ex, operation);
            return ErrorMessages.For(ex);
        }
#pragma warning restore CA1031
    }

    /// <summary>Why a file was turned away: a short reason for a list, and a sentence when it was the only one.</summary>
    private sealed record Skip(string Reason, string Sentence)
    {
        public static readonly Skip Type = new("Results_SkipType", "Results_Unsupported");
        public static readonly Skip TooLarge = new("Results_SkipTooLarge", "Results_TooLarge");
        public static readonly Skip Empty = new("Results_SkipEmpty", "Results_Empty");
        public static readonly Skip Unreadable = new("Results_SkipUnreadable", "Results_Unreadable");
    }
}

/// <summary>What an upload added to her results (their ids), and any message to show.</summary>
public sealed record AddedResults(IReadOnlyList<AttachmentId> Ids, string? Message)
{
    public static readonly AddedResults None = new([], null);
}
