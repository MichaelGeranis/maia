using System.Security.Cryptography;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>The Results screen and its tiles' pictures, at the edges: no plan, stray taps, changes made elsewhere.</summary>
public sealed class ResultsScreenEdgeTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Results_without_a_plan_goes_to_setup()
    {
        var vm = _app.CreateResults();

        await vm.LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.Empty(vm.Results);
    }

    [Fact]
    public async Task A_tap_or_hold_on_no_tile_does_nothing()
    {
        var vm = await ResultsAsync("Iron.pdf", "Booking bloods.pdf");

        await vm.OpenCommand.ExecuteAsync(null);
        vm.SelectFromCommand.Execute(null);

        Assert.False(vm.IsSelecting);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Null(vm.Notice);
    }

    [Fact]
    public async Task Holding_a_tile_while_choosing_ticks_it_and_again_unticks_it()
    {
        var vm = await ResultsAsync("Iron.pdf", "Booking bloods.pdf");
        vm.StartSelectingCommand.Execute(null);
        var card = vm.Results[0];

        vm.SelectFromCommand.Execute(card);
        Assert.Equal((true, "1 selected"), (card.IsSelected, vm.SelectionText));

        vm.SelectFromCommand.Execute(card);
        Assert.Equal((false, "Choose results to share"), (card.IsSelected, vm.SelectionText));
        Assert.True(vm.IsSelecting);
    }

    [Fact]
    public async Task Select_all_with_nothing_in_view_ticks_nothing()
    {
        var vm = await ResultsAsync("Iron.pdf");
        vm.StartSelectingCommand.Execute(null);
        vm.SearchText = "ultrasound";

        vm.SelectAllCommand.Execute(null);

        Assert.Empty(vm.Results);
        Assert.Equal(0, vm.SelectedCount);
        Assert.Equal("Choose results to share", vm.SelectionText);
    }

    [Fact]
    public async Task Share_with_nothing_ticked_opens_no_share_sheet_and_choosing_goes_on()
    {
        var vm = await ResultsAsync("Iron.pdf");
        vm.StartSelectingCommand.Execute(null);

        await vm.ShareSelectedCommand.ExecuteAsync(null);

        Assert.Empty(_app.Viewer.Shared);
        Assert.True(vm.IsSelecting);
        Assert.Null(vm.Notice);
    }

    [Fact]
    public async Task Leaving_when_not_choosing_keeps_the_list_as_it_was()
    {
        var vm = await ResultsAsync("Iron.pdf");

        vm.Leave();

        Assert.False(vm.IsSelecting);
        Assert.Equal("Iron", Assert.Single(vm.Results).Name);
    }

    [Fact]
    public async Task A_result_removed_elsewhere_leaves_the_list_and_takes_its_tick_with_it()
    {
        var vm = await ResultsAsync("Iron.pdf", "Booking bloods.pdf");
        var gone = vm.Results.Single(c => c.Name == "Iron");
        vm.SelectFromCommand.Execute(gone);
        Assert.Equal(1, vm.SelectedCount);

        await _app.Plans.RemoveResultAsync(gone.Id);
        await vm.LoadAsync();

        Assert.Equal("Booking bloods", Assert.Single(vm.Results).Name);
        Assert.True(vm.IsSelecting);
        Assert.Equal((0, "Choose results to share"), (vm.SelectedCount, vm.SelectionText));
        Assert.False(vm.ShareSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_tile_says_what_kind_of_result_it_is()
    {
        var vm = await ResultsAsync("Iron.pdf");

        var card = Assert.Single(vm.Results);

        Assert.Equal(("PDF", AttachmentKind.Document, false), (card.KindLabel, card.Kind, card.IsPhoto));
    }

    [Fact]
    public async Task A_result_added_while_pictures_are_drawn_gets_its_picture_in_the_same_pass()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new FileToAttach("Iron.pdf", "application/pdf", new byte[1024]));
        var opening = new SlowStep();
        _app.Renderer.SlowOpen = opening;
        var vm = _app.CreateResults();
        await vm.LoadAsync();
        await opening.Started;
        var drawing = vm.ThumbnailsDrawn;

        await _app.Plans.AttachFileAsync(new FileToAttach("Booking bloods.pdf", "application/pdf", new byte[1024]));
        await vm.LoadAsync();

        Assert.Same(drawing, vm.ThumbnailsDrawn);
        opening.Finish();
        await vm.ThumbnailsDrawn;
        Assert.Equal(2, vm.Results.Count);
        Assert.All(vm.Results, card => Assert.True(card.HasThumbnail, card.Name));
    }

    [Fact]
    public async Task A_result_without_a_stored_file_or_a_known_type_keeps_its_icon_and_nothing_is_read()
    {
        using var thumbnails = Thumbnails(_app.Photos);
        var noFile = ResultAttachment.Rehydrate(AttachmentId.New(), AttachmentKind.Document, "Old.pdf", _app.Clock.UtcNow, null, "application/pdf", 10);
        var noType = ResultAttachment.Rehydrate(AttachmentId.New(), AttachmentKind.Document, "Export", _app.Clock.UtcNow, "0123456789abcdef0123456789abcdef", null, 10);

        Assert.Null(await thumbnails.MakeAsync(noFile));
        Assert.Null(await thumbnails.MakeAsync(noType));
        Assert.Null(_app.Renderer.Opened);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task A_stored_file_that_no_longer_decrypts_keeps_its_icon_and_is_reported_by_code_only()
    {
        var result = await KeptAsync("Iron.pdf", "application/pdf");
        _app.Attachments.Damaged.Add(result.BlobId!);
        using var thumbnails = Thumbnails(_app.Photos);

        Assert.Null(await thumbnails.MakeAsync(result));

        var report = Assert.Single(_app.Errors.Reports);
        Assert.Equal("results.thumbnail", report.Operation);
        Assert.IsType<CryptographicException>(report.Exception, exactMatch: false);
    }

    [Fact]
    public async Task A_photo_the_phone_cannot_make_small_keeps_its_icon_and_is_reported_by_code_only()
    {
        var result = await KeptAsync("20-week scan.jpg", "image/jpeg");
        using var thumbnails = Thumbnails(new FailingPhotoProcessor());

        Assert.Null(await thumbnails.MakeAsync(result));

        Assert.Equal("results.thumbnail", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_pdf_with_no_pages_keeps_its_icon()
    {
        var result = await KeptAsync("Iron.pdf", "application/pdf");
        _app.Renderer.NextPdf = [];
        using var thumbnails = Thumbnails(_app.Photos);

        Assert.Null(await thumbnails.MakeAsync(result));

        Assert.Empty(_app.Renderer.Opened!.Rendered);
        Assert.True(_app.Renderer.Opened.Disposed);
        Assert.Empty(_app.Errors.Reports);
    }

    private ResultThumbnails Thumbnails(IPhotoProcessor photos) => new(_app.Plans, photos, _app.Renderer, _app.Errors);

    private async Task<ResultAttachment> KeptAsync(string name, string mediaType)
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new FileToAttach(name, mediaType, new byte[1024]));
        return Assert.Single(_app.Repository.Stored!.Results);
    }

    private async Task<ResultsViewModel> ResultsAsync(params string[] names)
    {
        await _app.CreatePlanAsync();
        foreach (var name in names)
        {
            await _app.Plans.AttachFileAsync(new FileToAttach(name, "application/pdf", new byte[1024]));
        }

        var vm = _app.CreateResults();
        await vm.LoadAsync();
        return vm;
    }
}
