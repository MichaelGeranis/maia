using Maia.Application.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Results open inside Maia, so a decrypted report or photo is never handed to another app.</summary>
public sealed class ResultViewerTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Opening_the_viewer_reads_the_plan_once()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        var vm = Viewer();
        var before = _app.Repository.LoadCount;

        await vm.LoadAsync(1080);

        Assert.Equal(before + 1, _app.Repository.LoadCount);
        Assert.Equal("Anomaly scan report", vm.Title);
        Assert.True(vm.IsPdf);
    }

    [Fact]
    public async Task A_pdf_opens_inside_maia_page_by_page_and_is_let_go_when_the_viewer_closes()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 410 * 1024);
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal(new[] { "viewer" }, _app.Navigator.Visited);
        Assert.Empty(_app.Viewer.Opened);
        Assert.Equal(("Anomaly scan report", "PDF · 2 pages · 410 KB", "Tap a page to zoom in."), (vm.Title, vm.Meta, vm.Hint));
        Assert.True(vm.IsPdf);
        Assert.False(vm.IsLoading);
        Assert.Equal(new[] { "Page 1 of 2", "Page 2 of 2" }, vm.Pages.Select(p => p.Label));
        Assert.All(vm.Pages, p => Assert.NotNull(p.Image));
        Assert.Equal(new[] { (0, 1080), (1, 1080) }, _app.Renderer.Opened!.Rendered);

        await vm.ZoomCommand.ExecuteAsync(vm.Pages[1]);
        Assert.True(vm.IsZoomed);
        Assert.False(vm.ShowPages);
        Assert.Equal((1, 2160), _app.Renderer.Opened.Rendered[^1]);
        Assert.True(vm.HandleBack());
        Assert.False(vm.IsZoomed);
        Assert.False(vm.HandleBack());

        vm.Close();
        Assert.True(_app.Renderer.Opened.Disposed);
        Assert.All(vm.Pages, p => Assert.Null(p.Image));
    }

    [Fact]
    public async Task A_long_pdf_draws_the_first_pages_then_each_as_she_scrolls_to_it()
    {
        _app.Renderer.NextPdf = Enumerable.Repeat(1.414, 20).ToList();
        await OpenAsync("Lab history.pdf", "application/pdf", 1024);
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal(20, vm.Pages.Count);
        Assert.Equal(new[] { 0, 1, 2 }, _app.Renderer.Opened!.Rendered.Select(r => r.Index));
        Assert.All(vm.Pages.Skip(3), p => Assert.Null(p.Image));

        await vm.ShowPagesAsync(9, 10);

        Assert.Equal(new[] { 0, 1, 2, 9, 10, 11, 12 }, _app.Renderer.Opened.Rendered.Select(r => r.Index));
        Assert.All(vm.Pages.Skip(9).Take(4), p => Assert.NotNull(p.Image));
    }

    [Fact]
    public async Task Pages_far_from_view_let_go_of_their_pictures_and_are_drawn_again_on_the_way_back()
    {
        _app.Renderer.NextPdf = Enumerable.Repeat(1.414, 20).ToList();
        await OpenAsync("Lab history.pdf", "application/pdf", 1024);
        var vm = Viewer();
        await vm.LoadAsync(1080);

        await vm.ShowPagesAsync(15, 16);

        Assert.Equal(new[] { 15, 16, 17, 18 }, vm.Pages.Where(p => p.Image is not null).Select(p => p.Number - 1));

        await vm.ShowPagesAsync(0, 1);

        Assert.Equal(new[] { 0, 1, 2, 3 }, vm.Pages.Where(p => p.Image is not null).Select(p => p.Number - 1));
        Assert.Equal(2, _app.Renderer.Opened!.Rendered.Count(r => r.Index == 0));
    }

    [Fact]
    public async Task A_page_that_finishes_drawing_after_the_viewer_closes_is_not_kept()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        var drawing = new SlowStep();
        _app.Renderer.SlowDrawing = drawing;
        var vm = Viewer();

        var loading = vm.LoadAsync(1080);
        await drawing.Started;
        vm.Close();
        drawing.Finish();
        await loading;

        Assert.All(vm.Pages, p => Assert.Null(p.Image));
        Assert.Equal(new[] { (0, 1080) }, _app.Renderer.Opened!.Rendered);
        Assert.True(_app.Renderer.Opened.Disposed);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task A_pdf_that_opens_after_she_left_is_closed_again_at_once()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        var opening = new SlowStep();
        _app.Renderer.SlowOpen = opening;
        var vm = Viewer();

        var loading = vm.LoadAsync(1080);
        await opening.Started;
        vm.Close();
        opening.Finish();
        await loading;

        Assert.True(_app.Renderer.Opened!.Disposed);
        Assert.Empty(_app.Renderer.Opened.Rendered);
        Assert.Empty(vm.Pages);
        Assert.False(vm.IsPdf);
    }

    [Fact]
    public void The_viewer_is_let_go_by_closing_it_so_the_app_does_not_keep_every_one_she_opened()
    {
        // The app's container keeps each disposable screen it makes until Maia ends, and with it the page and its
        // pictures. Close() lets go of the file instead.
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(ResultViewerViewModel)));
    }

    [Fact]
    public async Task A_photo_opens_inside_maia_to_pinch_and_zoom()
    {
        await OpenAsync("20-week scan.jpg", "image/jpeg", 2048);
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.True(vm.IsPhoto);
        Assert.Equal(2048, vm.Photo!.Length);
        Assert.Equal(("Photo · 2 KB", "Pinch or double-tap to zoom in."), (vm.Meta, vm.Hint));
        vm.Close();
        Assert.Null(vm.Photo);
    }

    [Fact]
    public async Task A_pdf_maia_cannot_draw_can_still_open_in_another_app_with_a_word_about_its_copies()
    {
        _app.Renderer.NextPdf = null;
        await OpenAsync("protected.pdf", "application/pdf", 1024);
        var vm = Viewer();

        await vm.LoadAsync(1080);
        await vm.OpenOutsideCommand.ExecuteAsync(null);

        Assert.Equal("Maia can't show this file here, but another app on your phone may.", vm.Notice);
        Assert.True(vm.CanOpenOutside);
        Assert.Equal("protected.pdf (application/pdf, 1024 bytes)", _app.Viewer.Opened.Single());
    }

    [Fact]
    public async Task A_heic_photo_this_phone_cannot_draw_goes_to_another_app_only_after_the_word_about_its_copies()
    {
        _app.Renderer.DrawsHeic = false;
        await OpenAsync("IMG_2041.heic", "image/heic", 1024);
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal(new[] { "viewer" }, _app.Navigator.Visited);
        Assert.Empty(_app.Viewer.Opened);
        Assert.False(vm.IsPhoto);
        Assert.Null(vm.Photo);
        Assert.Equal(("IMG_2041", "Photo · 1 KB"), (vm.Title, vm.Meta));
        Assert.Equal("Maia can't show this file here, but another app on your phone may.", vm.Notice);
        Assert.True(vm.CanOpenOutside);

        await vm.OpenOutsideCommand.ExecuteAsync(null);

        Assert.Equal("IMG_2041.heic (image/heic, 1024 bytes)", _app.Viewer.Opened.Single());
    }

    [Fact]
    public async Task A_heic_photo_this_phone_can_draw_shows_inside_maia()
    {
        await OpenAsync("IMG_2041.heic", "image/heic", 1024);
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.True(vm.IsPhoto);
        Assert.False(vm.CanOpenOutside);
        Assert.Null(vm.Notice);
        Assert.Empty(_app.Viewer.Opened);
    }

    [Fact]
    public async Task A_file_that_no_longer_opens_is_explained_and_reported_by_code_only()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        _app.Attachments.Blobs.Clear();
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal("This file doesn't open any more. It may have been damaged on this phone.", vm.Notice);
        Assert.False(vm.IsPdf);
        var report = Assert.Single(_app.Errors.Reports);
        Assert.Equal("results.view", report.Operation);
        Assert.DoesNotContain("Anomaly", report.Exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_viewer_opened_without_a_result_goes_back()
    {
        await _app.CreatePlanAsync();
        var vm = Viewer();

        await vm.LoadAsync(1080);

        Assert.Equal(new[] { "back" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Removing_a_result_asks_once_more_then_deletes_it_and_its_file_and_goes_back()
    {
        await OpenAsync("20-week scan.jpg", "image/jpeg", 2048);
        var vm = Viewer();
        await vm.LoadAsync(1080);
        Assert.True(vm.CanRemove);
        Assert.Equal("Remove 20-week scan", vm.RemovalLabel);

        vm.AskRemoveCommand.Execute(null);

        Assert.True(vm.IsConfirmingRemoval);
        Assert.Equal("Remove 20-week scan from this phone? This can't be undone.", vm.RemovalQuestion);
        Assert.Single(_app.Attachments.Blobs);

        await vm.RemoveCommand.ExecuteAsync(null);

        Assert.False(vm.IsConfirmingRemoval);
        Assert.False(vm.CanRemove);
        Assert.Empty(_app.Repository.Stored!.Results);
        Assert.Empty(_app.Attachments.Blobs);
        Assert.Equal("20-week scan removed.", _app.Notifier.Shown.Last());
        Assert.Equal(new[] { "viewer", "back" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Keep_leaves_the_result_as_it_was_and_remove_without_asking_does_nothing()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        var vm = Viewer();
        await vm.LoadAsync(1080);

        await vm.RemoveCommand.ExecuteAsync(null);
        vm.AskRemoveCommand.Execute(null);
        vm.KeepResultCommand.Execute(null);
        await vm.RemoveCommand.ExecuteAsync(null);

        Assert.False(vm.IsConfirmingRemoval);
        Assert.True(vm.CanRemove);
        Assert.Single(_app.Repository.Stored!.Results);
        Assert.Single(_app.Attachments.Blobs);
        Assert.Empty(_app.Notifier.Shown);
        Assert.Equal(new[] { "viewer" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_result_whose_file_no_longer_opens_can_still_be_removed()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        _app.Attachments.Blobs.Clear();
        var vm = Viewer();
        await vm.LoadAsync(1080);

        vm.AskRemoveCommand.Execute(null);
        await vm.RemoveCommand.ExecuteAsync(null);

        Assert.Empty(_app.Repository.Stored!.Results);
        Assert.Equal(new[] { "viewer", "back" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_removal_that_cannot_be_saved_says_so_and_stays_on_the_result()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        var vm = Viewer();
        await vm.LoadAsync(1080);
        _app.Repository.FailNextSave = new IOException("disk full");

        vm.AskRemoveCommand.Execute(null);
        await vm.RemoveCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", vm.Notice);
        Assert.True(vm.CanRemove);
        Assert.Single(_app.Repository.Stored!.Results);
        Assert.Equal("results.remove", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Equal(new[] { "viewer" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Rename_opens_the_name_in_place_and_saves_a_new_one_wherever_the_result_is()
    {
        await OpenAsync("IMG_2041.jpg", "image/jpeg", 2048);
        var vm = Viewer();
        await vm.LoadAsync(1080);
        Assert.Equal("Rename IMG_2041", vm.RenameLabel);

        vm.AskRenameCommand.Execute(null);
        Assert.True(vm.IsRenaming);
        Assert.Equal("IMG_2041", vm.NewName);

        vm.NewName = "  20-week scan  ";
        await vm.SaveNameCommand.ExecuteAsync(null);

        Assert.False(vm.IsRenaming);
        Assert.Equal("20-week scan", vm.Title);
        Assert.Equal("Remove 20-week scan", vm.RemovalLabel);
        Assert.Equal("20-week scan", Assert.Single(_app.Repository.Stored!.Results).DisplayName);
        Assert.Single(_app.Attachments.Blobs);
        Assert.Equal("Renamed.", _app.Notifier.Shown.Last());

        // Results shows the new name too.
        var results = _app.CreateResults();
        await results.LoadAsync();
        Assert.Equal("20-week scan", results.Results.Single().Name);
    }

    [Fact]
    public async Task A_pdf_can_be_renamed_too_and_cancel_or_a_blank_name_keeps_the_old_one()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        var vm = Viewer();
        await vm.LoadAsync(1080);
        var saves = _app.Repository.SaveCount;

        vm.AskRenameCommand.Execute(null);
        vm.NewName = "Something else";
        vm.CancelRenameCommand.Execute(null);
        Assert.False(vm.IsRenaming);

        vm.AskRenameCommand.Execute(null);
        vm.NewName = "   ";
        Assert.False(vm.SaveNameCommand.CanExecute(null));
        await vm.SaveNameCommand.ExecuteAsync(null);

        Assert.Equal("Anomaly scan report", vm.Title);
        Assert.Equal(saves, _app.Repository.SaveCount);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Rename_and_remove_never_ask_at_the_same_time()
    {
        await OpenAsync("Anomaly scan report.pdf", "application/pdf", 1024);
        var vm = Viewer();
        await vm.LoadAsync(1080);

        vm.AskRemoveCommand.Execute(null);
        vm.AskRenameCommand.Execute(null);
        Assert.Equal((true, false), (vm.IsRenaming, vm.IsConfirmingRemoval));

        vm.AskRemoveCommand.Execute(null);
        Assert.Equal((false, true), (vm.IsRenaming, vm.IsConfirmingRemoval));
    }

    [Fact]
    public void Rename_sits_next_to_remove_in_the_viewer()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "ResultViewerPage.xaml"));
        var rename = page.Descendants().Single(e => AccessibilityTests.Attr(e, "Command") == "{Binding AskRenameCommand}");
        var remove = page.Descendants().Single(e => AccessibilityTests.Attr(e, "Command") == "{Binding AskRemoveCommand}");

        Assert.Same(rename.Parent, remove.Parent);
        Assert.Equal("{loc:Translate Results_RenameButton}", AccessibilityTests.Attr(rename, "Text"));
        Assert.Equal(("Rename", "Μετονομασία"), (Localization.Text.In("Results_RenameButton", "en"), Localization.Text.In("Results_RenameButton", "el")));
    }

    [Fact]
    public async Task The_viewer_opened_without_a_result_offers_no_removal()
    {
        await _app.CreatePlanAsync();
        var vm = Viewer();

        await vm.LoadAsync(1080);
        vm.AskRemoveCommand.Execute(null);

        Assert.False(vm.CanRemove);
        Assert.False(vm.IsConfirmingRemoval);
    }

    private ResultViewerViewModel Viewer() => new(_app.Plans, _app.Viewing, _app.Renderer, _app.Viewer, _app.Navigator, _app.Notifier, _app.Errors);

    /// <summary>Files a result under the anomaly scan and taps it on Results.</summary>
    private async Task OpenAsync(string name, string mediaType, int size)
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new FileToAttach(name, mediaType, new byte[size]));
        var results = _app.CreateResults();
        await results.LoadAsync();
        await results.OpenCommand.ExecuteAsync(results.Results.Single());
    }
}
