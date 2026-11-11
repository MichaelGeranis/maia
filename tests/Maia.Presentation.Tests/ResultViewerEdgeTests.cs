using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>The in-app viewer at the edges: files kept by older versions, long PDFs, pages that fail, and leaving early.</summary>
public sealed class ResultViewerEdgeTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Showing_the_viewer_again_reads_nothing_more()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        var vm = Viewer();
        await vm.LoadAsync(1080);
        var loads = _app.Repository.LoadCount;

        await vm.LoadAsync(1080);

        Assert.Equal(loads, _app.Repository.LoadCount);
        Assert.Equal(2, vm.Pages.Count);
        Assert.Equal(new[] { "viewer" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_result_of_unknown_type_opens_only_in_another_app_as_a_plain_file()
    {
        await StoredAsync(AttachmentKind.Document, "Lab export", mediaType: null, size: 2048);
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal(("Lab export", "PDF · 2 KB"), (vm.Title, vm.Meta));
        Assert.Equal("Maia can't show this file here, but another app on your phone may.", vm.Notice);
        Assert.True(vm.CanOpenOutside);
        Assert.False(vm.ShowPages);
        Assert.False(vm.IsPhoto);

        await vm.OpenOutsideCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Lab export (application/octet-stream, 2048 bytes)" }, _app.Viewer.Opened);
    }

    [Fact]
    public async Task A_pdf_kept_without_its_size_says_how_many_pages_it_has()
    {
        await StoredAsync(AttachmentKind.Document, "Old report.pdf", "application/pdf", size: null);
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal("PDF · 2 pages", vm.Meta);
        Assert.True(vm.ShowPages);
    }

    [Fact]
    public async Task A_pdf_of_more_than_sixty_pages_shows_the_first_sixty_and_offers_another_app_for_the_rest()
    {
        _app.Renderer.NextPdf = Enumerable.Repeat(1.414, 61).ToList();
        await FileAsync("Lab history.pdf", "application/pdf");
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal(ResultViewerViewModel.MaxPages, vm.Pages.Count);
        Assert.Equal("Page 60 of 61", vm.Pages[^1].Label);
        Assert.Equal("PDF · 61 pages · 1 KB", vm.Meta);
        Assert.Equal("Showing the first 60 pages. To see them all, open it in another app.", vm.Hint);
        Assert.True(vm.CanOpenOutside);
        Assert.Null(vm.Notice);
    }

    [Fact]
    public async Task A_result_no_longer_in_her_plan_says_something_went_wrong_and_offers_nothing()
    {
        await _app.CreatePlanAsync();
        await _app.Viewing.ShowAsync(AttachmentId.New());
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal("Something went wrong. Please try again.", vm.Notice);
        Assert.False(vm.IsLoading);
        Assert.False(vm.CanRemove);
        Assert.Equal("results.view", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_file_read_after_she_left_is_not_kept_or_drawn()
    {
        var store = new ScriptedAttachmentStore();
        using var plans = new PlanService(_app.Repository, _app.Templates, store, _app.DataKey, _app.Backups, _app.Clock, _app.Errors, _app.Reminders);
        await _app.CreatePlanAsync();
        var id = await plans.AttachFileAsync(new FileToAttach("Anomaly scan report.pdf", "application/pdf", new byte[1024]));
        await _app.Viewing.ShowAsync(id);
        var reading = new SlowStep();
        store.SlowRead = reading;
        var vm = new ResultViewerViewModel(plans, _app.Viewing, _app.Renderer, _app.Viewer, _app.Navigator, _app.Notifier, _app.Errors);

        var loading = vm.LoadAsync(1080);
        await reading.Started;
        vm.Close();
        reading.Finish();
        await loading;

        Assert.Null(_app.Renderer.Opened);
        Assert.Empty(vm.Pages);
        Assert.False(vm.IsLoading);
        Assert.Null(vm.Notice);

        await vm.OpenOutsideCommand.ExecuteAsync(null);
        Assert.Empty(_app.Viewer.Opened);
    }

    [Fact]
    public async Task A_pdf_the_phone_cannot_draw_that_answers_after_she_left_is_simply_dropped()
    {
        await FileAsync("Protected.pdf", "application/pdf");
        _app.Renderer.NextPdf = null;
        var opening = new SlowStep();
        _app.Renderer.SlowOpen = opening;
        var vm = Viewer();

        var loading = vm.LoadAsync(1080);
        await opening.Started;
        vm.Close();
        opening.Finish();
        await loading;

        Assert.False(vm.CanOpenOutside);
        Assert.Null(vm.Notice);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Without_a_result_rename_and_remove_do_nothing()
    {
        await _app.CreatePlanAsync();
        var vm = Viewer();
        await vm.LoadAsync(1080);

        vm.AskRenameCommand.Execute(null);
        Assert.False(vm.IsRenaming);
        vm.NewName = "Scan";
        await vm.SaveNameCommand.ExecuteAsync(null);
        await vm.RemoveCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.Title);
        Assert.Equal(new[] { "back" }, _app.Navigator.Visited);
        Assert.Empty(_app.Notifier.Shown);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task A_name_typed_without_rename_open_is_not_saved()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        var vm = Viewer();
        await vm.LoadAsync(1080);
        var saves = _app.Repository.SaveCount;

        vm.NewName = "Scan";
        await vm.SaveNameCommand.ExecuteAsync(null);

        Assert.Equal("Anomaly scan report", vm.Title);
        Assert.Equal(saves, _app.Repository.SaveCount);
    }

    [Fact]
    public async Task Saving_the_same_name_closes_the_field_without_saving_or_a_toast()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        var vm = Viewer();
        await vm.LoadAsync(1080);
        var saves = _app.Repository.SaveCount;
        vm.AskRenameCommand.Execute(null);

        vm.NewName = "  Anomaly scan report  ";
        await vm.SaveNameCommand.ExecuteAsync(null);

        Assert.False(vm.IsRenaming);
        Assert.Equal(saves, _app.Repository.SaveCount);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task A_name_longer_than_allowed_is_cut_to_the_longest_she_can_give()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        var vm = Viewer();
        await vm.LoadAsync(1080);
        vm.AskRenameCommand.Execute(null);

        vm.NewName = new string('a', ResultViewerViewModel.MaxNameLength + 50);
        await vm.SaveNameCommand.ExecuteAsync(null);

        Assert.Equal(new string('a', ResultViewerViewModel.MaxNameLength), vm.Title);
        Assert.Equal(vm.Title, Assert.Single(_app.Repository.Stored!.Results).DisplayName);
        Assert.Equal("Renamed.", Assert.Single(_app.Notifier.Shown));
    }

    [Fact]
    public async Task A_photo_has_no_pages_to_zoom_or_scroll()
    {
        await FileAsync("20-week scan.jpg", "image/jpeg");
        var vm = Viewer();
        await vm.LoadAsync(1080);

        await vm.ZoomCommand.ExecuteAsync(new ViewerPage(1, 1.414, "Page 1 of 1"));
        await vm.ShowPagesAsync(0, 3);

        Assert.True(vm.IsPhoto);
        Assert.False(vm.IsZoomed);
        Assert.Empty(vm.Pages);
    }

    [Fact]
    public async Task Zooming_into_no_page_does_nothing()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        var vm = Viewer();
        await vm.LoadAsync(1080);

        await vm.ZoomCommand.ExecuteAsync(null);

        Assert.False(vm.IsZoomed);
        Assert.Equal(2, _app.Renderer.Opened!.Rendered.Count);
    }

    [Fact]
    public async Task Back_goes_back()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        var vm = Viewer();
        await vm.LoadAsync(1080);

        await vm.BackCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "viewer", "back" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_file_that_no_longer_opens_is_not_offered_to_another_app()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        _app.Attachments.Damaged.UnionWith(_app.Attachments.Blobs.Keys);
        var vm = Viewer();
        await vm.LoadAsync(1080);

        await vm.OpenOutsideCommand.ExecuteAsync(null);

        Assert.Equal("This file doesn't open any more. It may have been damaged on this phone.", vm.Notice);
        Assert.Empty(_app.Viewer.Opened);
    }

    [Fact]
    public async Task Another_app_that_cannot_open_the_file_is_explained_calmly_and_reported_by_code_only()
    {
        _app.Renderer.DrawsHeic = false;
        await FileAsync("IMG_2041.heic", "image/heic");
        var vm = new ResultViewerViewModel(_app.Plans, _app.Viewing, _app.Renderer, new NoAppViewer(), _app.Navigator, _app.Notifier, _app.Errors);
        await vm.LoadAsync(1080);

        await vm.OpenOutsideCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", vm.Notice);
        Assert.Equal("results.open", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_page_that_wont_draw_keeps_its_place_is_reported_once_and_not_tried_again()
    {
        var pdf = new ScriptedPdf(3);
        pdf.Broken.Add(1);
        var vm = await ScriptedViewerAsync(pdf);

        await vm.ShowPagesAsync(0, 2);

        Assert.Equal(new[] { true, false, true }, vm.Pages.Select(p => p.Image is not null));
        Assert.Equal(1, pdf.Rendered.Count(r => r.Index == 1));
        Assert.Equal("results.view.page", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_zoom_that_wont_draw_sharper_keeps_the_page_as_drawn()
    {
        var pdf = new ScriptedPdf(2);
        var vm = await ScriptedViewerAsync(pdf);
        pdf.Broken.Add(1);

        await vm.ZoomCommand.ExecuteAsync(vm.Pages[1]);

        Assert.True(vm.IsZoomed);
        Assert.Same(vm.Pages[1].Image, vm.ZoomedImage);
        Assert.Equal("results.view.zoom", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task Leaving_while_a_zoom_draws_drops_it_without_a_word()
    {
        var pdf = new ScriptedPdf(2);
        var vm = await ScriptedViewerAsync(pdf);
        var drawing = new SlowStep();
        pdf.Hold = drawing;

        var zooming = vm.ZoomCommand.ExecuteAsync(vm.Pages[1]);
        await drawing.Started;
        vm.Close();
        drawing.Finish();
        await zooming;

        Assert.Null(vm.ZoomedImage);
        Assert.True(pdf.Disposed);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task A_sharper_page_that_arrives_after_she_left_is_not_kept()
    {
        await FileAsync("Anomaly scan report.pdf", "application/pdf");
        var drawing = new SlowStep();
        _app.Renderer.SlowDrawing = drawing;
        var vm = Viewer();

        var loading = vm.LoadAsync(1080);
        await drawing.Started;
        var zooming = vm.ZoomCommand.ExecuteAsync(vm.Pages[1]);
        vm.Close();
        drawing.Finish();
        await Task.WhenAll(loading, zooming);

        Assert.Null(vm.ZoomedImage);
        Assert.Contains((1, 2160), _app.Renderer.Opened!.Rendered);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Back_from_a_zoom_before_it_is_drawn_sharper_stays_on_all_pages()
    {
        var pdf = new ScriptedPdf(2);
        var vm = await ScriptedViewerAsync(pdf);
        var drawing = new SlowStep();
        pdf.Hold = drawing;

        var zooming = vm.ZoomCommand.ExecuteAsync(vm.Pages[1]);
        await drawing.Started;
        Assert.True(vm.HandleBack());
        drawing.Finish();
        await zooming;

        Assert.False(vm.IsZoomed);
        Assert.Null(vm.ZoomedImage);
        Assert.True(vm.ShowPages);
    }

    [Fact]
    public async Task Leaving_while_a_page_fails_to_draw_says_nothing()
    {
        var pdf = new ScriptedPdf(2);
        var drawing = new SlowStep();
        pdf.Hold = drawing;
        var vm = await ScriptedViewerAsync(pdf, load: false);

        var loading = vm.LoadAsync(1080);
        await drawing.Started;
        vm.Close();
        drawing.Finish();
        await loading;

        Assert.All(vm.Pages, p => Assert.Null(p.Image));
        Assert.Single(pdf.Rendered);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Scrolling_while_a_page_draws_is_picked_up_by_the_same_drawing()
    {
        var pdf = new ScriptedPdf(10);
        var drawing = new SlowStep();
        pdf.Hold = drawing;
        var vm = await ScriptedViewerAsync(pdf, load: false);

        var loading = vm.LoadAsync(1080);
        await drawing.Started;
        var scrolled = vm.ShowPagesAsync(5, 5);
        drawing.Finish();
        await loading;
        await scrolled;

        Assert.Equal(new[] { 0, 5, 6, 7 }, pdf.Rendered.Select(r => r.Index));
        Assert.Equal(new[] { 1, 6, 7, 8 }, vm.Pages.Where(p => p.Image is not null).Select(p => p.Number));
    }

    [Fact]
    public async Task Scrolling_after_leaving_draws_nothing()
    {
        var pdf = new ScriptedPdf(10);
        var vm = await ScriptedViewerAsync(pdf);
        var drawn = pdf.Rendered.Count;

        vm.Close();
        await vm.ShowPagesAsync(5, 6);

        Assert.Equal(drawn, pdf.Rendered.Count);
    }

    private ResultViewerViewModel Viewer() => new(_app.Plans, _app.Viewing, _app.Renderer, _app.Viewer, _app.Navigator, _app.Notifier, _app.Errors);

    /// <summary>A PDF filed and tapped, shown by a viewer drawing <paramref name="pdf"/>.</summary>
    private async Task<ResultViewerViewModel> ScriptedViewerAsync(ScriptedPdf pdf, bool load = true)
    {
        await FileAsync("Lab history.pdf", "application/pdf");
        var vm = new ResultViewerViewModel(_app.Plans, _app.Viewing, new ScriptedRenderer(pdf), _app.Viewer, _app.Navigator, _app.Notifier, _app.Errors);
        if (load)
        {
            await vm.LoadAsync(1080);
        }

        return vm;
    }

    /// <summary>Files a result and asks the viewer to show it, as a tap on Results does.</summary>
    private async Task FileAsync(string name, string mediaType)
    {
        await _app.CreatePlanAsync();
        var id = await _app.Plans.AttachFileAsync(new FileToAttach(name, mediaType, new byte[1024]));
        await _app.Viewing.ShowAsync(id);
    }

    /// <summary>A result as an older Maia kept it, perhaps without its type or size, asked for in the viewer.</summary>
    private async Task StoredAsync(AttachmentKind kind, string name, string? mediaType, long? size)
    {
        await _app.CreatePlanAsync();
        var blob = await _app.Attachments.SaveAsync(new byte[2048], CancellationToken.None);
        var id = AttachmentId.New();
        var plan = _app.Repository.Stored!;
        plan.AttachResult(ResultAttachment.Rehydrate(id, kind, name, _app.Clock.UtcNow, blob, mediaType, size));
        await _app.Repository.SaveAsync(plan, CancellationToken.None);
        await _app.Viewing.ShowAsync(id);
    }
}
