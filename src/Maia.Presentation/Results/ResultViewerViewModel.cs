using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Results;

/// <summary>
/// A result file shown inside Maia: a photo to pinch and zoom, or a PDF's pages to scroll, tap one to zoom in. The
/// decrypted file stays in Maia's memory and is let go when the viewer closes; it goes to another app only if she
/// asks, for a file Maia can't draw. She can rename it, or remove it, from here.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="IDisposable"/>: the container keeps every disposable transient it resolves until its
/// scope ends, which for a page is the app's lifetime. <see cref="Close"/> lets everything go instead, and whatever is
/// still on its way after that (the file being read, the PDF being opened, a page being drawn) is dropped when it arrives.
/// </remarks>
public sealed partial class ResultViewerViewModel : ObservableObject
{
    /// <summary>Pages drawn at most; a longer PDF shows its first pages and offers another app for the rest.</summary>
    public const int MaxPages = 60;

    /// <summary>A zoomed page is drawn at this multiple of the screen's width, so small print stays sharp.</summary>
    public const int ZoomDetail = 2;

    /// <summary>Pages drawn after the last one in view, so the next ones are ready as she scrolls.</summary>
    public const int PagesAhead = 2;

    /// <summary>
    /// Pictures are kept for this many pages either side of those in view. Farther ones are let go, so a long PDF never
    /// holds every page in memory, and drawn again if she scrolls back.
    /// </summary>
    public const int PagesKept = 5;

    private const int MaxZoomWidth = 2600;

    private readonly PlanService _plans;
    private readonly ResultViewing _viewing;
    private readonly IResultRenderer _renderer;
    private readonly IResultViewer _external;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;

    // Pages that would not draw, by number: not tried again.
    private readonly HashSet<int> _wontDraw = [];

    private IPdfPages? _pdf;
    private byte[]? _content;
    private string _mediaType = string.Empty;
    private int _pageWidth;
    private bool _loaded;
    private bool _closed;

    // The result shown, once found in her plan: what "Remove" deletes.
    private AttachmentId? _id;

    // The pages in view, by index, and the drawing of pages under way.
    private int _firstInView;
    private int _lastInView;
    private Task? _drawing;

    public ResultViewerViewModel(
        PlanService plans,
        ResultViewing viewing,
        IResultRenderer renderer,
        IResultViewer external,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors)
    {
        _plans = plans;
        _viewing = viewing;
        _renderer = renderer;
        _external = external;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
    }

    /// <summary>The longest name she can give a result.</summary>
    public const int MaxNameLength = Domain.Plans.ResultAttachment.MaxNameLength;

    /// <summary>The file's name: "Anomaly scan report.pdf".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemovalQuestion), nameof(RemovalLabel), nameof(RenameLabel))]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>"PDF · 3 pages · 410 KB" or "Photo · 2.3 MB".</summary>
    [ObservableProperty]
    public partial string Meta { get; private set; } = string.Empty;

    /// <summary>How to zoom, or why not everything is shown.</summary>
    [ObservableProperty]
    public partial string Hint { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    /// <summary>A photo, shown whole, to pinch and zoom.</summary>
    [ObservableProperty]
    public partial byte[]? Photo { get; private set; }

    [ObservableProperty]
    public partial bool IsPhoto { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPages))]
    public partial bool IsPdf { get; private set; }

    /// <summary>All pages show, unless one is zoomed in.</summary>
    public bool ShowPages => IsPdf && !IsZoomed;

    /// <summary>A PDF's pages, each drawn as she scrolls to it (see <see cref="ShowPagesAsync"/>).</summary>
    public ObservableCollection<ViewerPage> Pages { get; } = [];

    /// <summary>The page she tapped, drawn larger to zoom into; null when she sees them all.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsZoomed), nameof(ShowPages))]
    public partial ViewerPage? ZoomedPage { get; private set; }

    /// <summary>The zoomed page, drawn at <see cref="ZoomDetail"/> times the screen's width.</summary>
    [ObservableProperty]
    public partial byte[]? ZoomedImage { get; private set; }

    public bool IsZoomed => ZoomedPage is not null;

    /// <summary>Maia can't draw this file: she can open it in another app instead.</summary>
    [ObservableProperty]
    public partial bool CanOpenOutside { get; private set; }

    /// <summary>A calm line when something could not be shown.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>Shows the result asked for with <see cref="ResultViewing.ShowAsync"/>.</summary>
    /// <param name="pageWidth">The screen's width in pixels, to draw PDF pages at.</param>
    public async Task LoadAsync(int pageWidth)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _pageWidth = Math.Max(pageWidth, 320);
        if (_viewing.Take() is not { } request)
        {
            await _navigator.GoBackAsync();
            return;
        }

        try
        {
            var plan = await _plans.GetPlanAsync();
            var attachment = plan.FindResult(request);
            _mediaType = attachment.MediaType ?? "application/octet-stream";
            Title = ShareFiles.WithoutEnding(attachment.DisplayName);
            _id = attachment.Id;
            CanRemove = true;
            var item = ResultItem.From(attachment);
            // The file of the result just found, without reading her plan again.
            var content = await _plans.ReadResultFileAsync(attachment);
            if (_closed)
            {
                // She left before the file was read: it is not kept.
                return;
            }

            _content = content;
            if (ResultViewing.IsPdf(_mediaType))
            {
                await ShowPdfAsync(item, attachment.SizeBytes);
            }
            else if (_viewing.CanShow(_mediaType))
            {
                Meta = item.Meta;
                Hint = Text.Get("Viewer_PhotoHint");
                Photo = _content;
                IsPhoto = true;
            }
            else
            {
                // A photo this phone can't draw (HEIC before Android 9): as for a PDF it can't draw, she reads that
                // another app may keep its own copy before she chooses to open it there.
                ShowOutsideOnly(item.Meta);
            }
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or InvalidDataException)
        {
            // The stored file is gone or damaged: say so calmly; the code alone is reported.
            _errors.Report(ex, "results.view");
            Notice = Text.Get("Viewer_Unreadable");
        }
#pragma warning disable CA1031 // Anything else is shown calmly too; details go to the error reporter.
        catch (Exception ex)
        {
            _errors.Report(ex, "results.view");
            Notice = Text.Get("Common_Error");
        }
#pragma warning restore CA1031
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>"Remove": offered once the result is found, even when its file no longer opens.</summary>
    [ObservableProperty]
    public partial bool CanRemove { get; private set; }

    /// <summary>She tapped "Remove" and is asked once more, in place, before anything is deleted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemovalQuestion))]
    public partial bool IsConfirmingRemoval { get; private set; }

    /// <summary>"Remove 20-week scan.jpg? It's deleted from this phone and can't be undone."</summary>
    public string RemovalQuestion => Text.Format("Results_RemoveConfirm", Title);

    /// <summary>What a screen reader says for "Remove": "Remove 20-week scan.jpg".</summary>
    public string RemovalLabel => Text.Format("Results_Remove", Title);

    /// <summary>Whether the name is open to change, in place under the buttons.</summary>
    [ObservableProperty]
    public partial bool IsRenaming { get; private set; }

    /// <summary>The new name as she types it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveNameCommand))]
    public partial string NewName { get; set; } = string.Empty;

    /// <summary>What a screen reader says for "Rename": "Rename 20-week scan.jpg".</summary>
    public string RenameLabel => Text.Format("Results_Rename", Title);

    /// <summary>"Rename": the name opens in place, with Save and Cancel.</summary>
    [RelayCommand]
    private void AskRename()
    {
        if (!CanRemove)
        {
            return;
        }

        IsConfirmingRemoval = false;
        NewName = Title;
        IsRenaming = true;
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;

    private bool CanSaveName() => !string.IsNullOrWhiteSpace(NewName);

    /// <summary>Saves the new name; the file itself stays as it is. Saving the same name just closes the field.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveName))]
    private async Task SaveNameAsync()
    {
        if (_id is not { } id || !IsRenaming || string.IsNullOrWhiteSpace(NewName))
        {
            return;
        }

        var name = NewName.Trim();
        if (name.Length > MaxNameLength)
        {
            name = name[..MaxNameLength];
        }

        // The name as shown (without its file ending), saved unchanged: nothing to rename.
        if (name == Title)
        {
            IsRenaming = false;
            return;
        }

        Notice = await PlanChange.RunAsync(() => _plans.RenameResultAsync(id, name), "results.rename", _errors);
        if (Notice is null)
        {
            var changed = name != Title;
            IsRenaming = false;
            Title = name;
            if (changed)
            {
                _notifier.Show(Text.Get("Toast_ResultRenamed"));
            }
        }
    }

    [RelayCommand]
    private void AskRemove()
    {
        IsRenaming = false;
        IsConfirmingRemoval = CanRemove;
    }

    [RelayCommand]
    private void KeepResult() => IsConfirmingRemoval = false;

    /// <summary>"Remove": deletes the result and its file from this phone, then goes back to where she opened it.</summary>
    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (_id is not { } id || !IsConfirmingRemoval)
        {
            return;
        }

        IsConfirmingRemoval = false;
        Notice = await PlanChange.RunAsync(() => _plans.RemoveResultAsync(id), "results.remove", _errors);
        if (Notice is null)
        {
            CanRemove = false;
            // Back first, so the toast shows on the screen she returns to, not on the one closing.
            var removed = Text.Format("Toast_ResultRemoved", Title);
            await _navigator.GoBackAsync();
            _notifier.Show(removed);
        }
    }

    /// <summary>Opens a page larger, to pinch and zoom into its small print.</summary>
    [RelayCommand]
    private async Task ZoomAsync(ViewerPage page)
    {
        if (_pdf is null || page is null)
        {
            return;
        }

        ZoomedPage = page;
        ZoomedImage = page.Image;
        try
        {
            var sharper = await _pdf.RenderAsync(page.Number - 1, Math.Min(_pageWidth * ZoomDetail, MaxZoomWidth));
            if (!_closed && ZoomedPage == page)
            {
                ZoomedImage = sharper;
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            if (_closed)
            {
                // The PDF was closed as she left: nothing went wrong.
                return;
            }

            // The page as already drawn is still there to zoom into.
            _errors.Report(ex, "results.view.zoom");
        }
    }

    /// <summary>Back from a zoomed page to all pages.</summary>
    [RelayCommand]
    private void CloseZoom()
    {
        ZoomedPage = null;
        ZoomedImage = null;
    }

    /// <summary>The phone's back button: a zoomed page goes back to all pages first.</summary>
    /// <returns>Whether the viewer handled it.</returns>
    public bool HandleBack()
    {
        if (!IsZoomed)
        {
            return false;
        }

        CloseZoom();
        return true;
    }

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();

    /// <summary>"Open in another app", for a file Maia can't draw.</summary>
    [RelayCommand]
    private async Task OpenOutsideAsync()
    {
        if (_content is null)
        {
            return;
        }

        try
        {
            await _external.OpenFileAsync(ShareFiles.FileName(Title, _mediaType), _mediaType, _content);
        }
#pragma warning disable CA1031 // A viewer failure is shown calmly; details go to the error reporter.
        catch (Exception ex)
        {
            _errors.Report(ex, "results.open");
            Notice = Text.Get("Common_Error");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Closes the viewer: stops drawing, closes the PDF and lets go of the decrypted content. Whatever is still being
    /// read or drawn is dropped when it arrives.
    /// </summary>
    public void Close()
    {
        _closed = true;
        _pdf?.Dispose();
        _pdf = null;
        _content = null;
        Photo = null;
        ZoomedPage = null;
        ZoomedImage = null;
        foreach (var page in Pages)
        {
            page.Image = null;
        }
    }

    /// <summary>A file Maia can't draw: a calm line, and "Open in another app" with its note about the other app's copies.</summary>
    private void ShowOutsideOnly(string meta)
    {
        Meta = meta;
        Notice = Text.Get("Viewer_CantShow");
        CanOpenOutside = true;
    }

    private async Task ShowPdfAsync(ResultItem item, long? size)
    {
        var opened = await _renderer.OpenPdfAsync(_content!);
        if (_closed)
        {
            // She left while it opened: it is closed again at once.
            opened?.Dispose();
            return;
        }

        _pdf = opened;
        if (_pdf is null)
        {
            ShowOutsideOnly(item.Meta);
            return;
        }

        var count = _pdf.Ratios.Count;
        var pages = DisplayFormat.Count("Viewer_PageOne", "Viewer_Pages", count);
        Meta = size is { } bytes ? $"{item.KindLabel} · {pages} · {ResultItem.FileSize(bytes)}" : $"{item.KindLabel} · {pages}";
        var shown = Math.Min(count, MaxPages);
        Hint = count > MaxPages ? Text.Format("Viewer_FirstPages", MaxPages) : Text.Get("Viewer_PdfHint");
        CanOpenOutside = count > MaxPages;
        for (var i = 0; i < shown; i++)
        {
            Pages.Add(new ViewerPage(i + 1, _pdf.Ratios[i], Text.Format("Viewer_PageOf", i + 1, count)));
        }

        IsPdf = true;
        IsLoading = false;

        // The first pages, so she can start reading at once; the rest as she scrolls to them.
        await ShowPagesAsync(0, 0);
    }

    /// <summary>
    /// The pages in view changed (she scrolled): those and the next <see cref="PagesAhead"/> are drawn, one at a time
    /// and in order, and pictures more than <see cref="PagesKept"/> pages away are let go.
    /// </summary>
    /// <param name="first">The first page in view, by index.</param>
    /// <param name="last">The last page in view, by index.</param>
    /// <returns>Completes once the pages wanted now are drawn (or the viewer closed).</returns>
    public Task ShowPagesAsync(int first, int last)
    {
        if (_closed || Pages.Count == 0)
        {
            return Task.CompletedTask;
        }

        _firstInView = Math.Clamp(first, 0, Pages.Count - 1);
        _lastInView = Math.Clamp(last, _firstInView, Pages.Count - 1);
        for (var i = 0; i < Pages.Count; i++)
        {
            if (i < _firstInView - PagesKept || i > _lastInView + PagesKept)
            {
                Pages[i].Image = null;
            }
        }

        // One drawing at a time: a drawing under way picks up the new pages as it goes.
        if (_drawing is not { IsCompleted: false })
        {
            _drawing = DrawWantedAsync();
        }

        return _drawing;
    }

    private async Task DrawWantedAsync()
    {
        // Started only while the PDF is open (its pages exist only once it is), and it stops as soon as a page arrives
        // after the viewer closed: so the PDF is open for as long as this runs.
        var pdf = _pdf!;
        while (NextToDraw() is { } page)
        {
            try
            {
                var image = await pdf.RenderAsync(page.Number - 1, _pageWidth);
                if (_closed)
                {
                    // A page finished drawing after she left: its picture is not kept.
                    return;
                }

                page.Image = image;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                if (_closed)
                {
                    return;
                }

                // One page that won't draw keeps its place; the others still show.
                _wontDraw.Add(page.Number);
                _errors.Report(ex, "results.view.page");
            }
        }
    }

    /// <summary>The first page from those in view to the ones just after that has no picture yet.</summary>
    private ViewerPage? NextToDraw()
    {
        var last = Math.Min(_lastInView + PagesAhead, Pages.Count - 1);
        for (var i = _firstInView; i <= last; i++)
        {
            if (Pages[i] is { Image: null } page && !_wontDraw.Contains(page.Number))
            {
                return page;
            }
        }

        return null;
    }
}

/// <summary>One page of a PDF in the viewer: its place, its shape (so the list keeps its height while it draws) and its picture.</summary>
public sealed partial class ViewerPage : ObservableObject
{
    public ViewerPage(int number, double ratio, string label)
    {
        Number = number;
        Ratio = ratio;
        Label = label;
    }

    public int Number { get; }

    /// <summary>Height divided by width.</summary>
    public double Ratio { get; }

    /// <summary>"Page 2 of 3", for screen readers and the zoomed page.</summary>
    public string Label { get; }

    [ObservableProperty]
    public partial byte[]? Image { get; internal set; }
}
