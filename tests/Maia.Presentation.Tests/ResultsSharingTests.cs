using Maia.Application.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Choosing results on the Results screen and sending them on through the phone's share sheet (email, WhatsApp,
/// Viber…). Choosing starts from "Select" or by holding a tile; a tap then ticks or unticks.
/// </summary>
public sealed class ResultsSharingTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Select_starts_choosing_with_nothing_ticked_and_share_waiting_for_a_tick()
    {
        var vm = await ThreeResultsAsync();
        Assert.True(vm.CanStartSelecting);

        vm.StartSelectingCommand.Execute(null);

        Assert.True(vm.IsSelecting);
        Assert.False(vm.CanStartSelecting);
        Assert.All(vm.Results, c => Assert.False(c.IsSelected));
        Assert.Equal("Choose results to share", vm.SelectionText);
        Assert.False(vm.ShareSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Holding_a_tile_starts_choosing_with_it_ticked_and_a_tap_then_ticks_rather_than_opens()
    {
        var vm = await ThreeResultsAsync();

        vm.SelectFromCommand.Execute(vm.Results[1]);
        await vm.OpenCommand.ExecuteAsync(vm.Results[0]);

        Assert.Equal(new[] { true, true, false }, vm.Results.Select(c => c.IsSelected));
        Assert.Equal("2 selected", vm.SelectionText);
        Assert.True(vm.ShareSelectedCommand.CanExecute(null));
        Assert.Empty(_app.Navigator.Visited);

        await vm.OpenCommand.ExecuteAsync(vm.Results[0]);
        Assert.Equal(new[] { false, true, false }, vm.Results.Select(c => c.IsSelected));
    }

    [Fact]
    public async Task Share_sends_the_ticked_results_in_list_order_with_their_file_endings_and_ends_choosing()
    {
        var vm = await ThreeResultsAsync();
        vm.StartSelectingCommand.Execute(null);
        await vm.OpenCommand.ExecuteAsync(vm.Results[2]);
        await vm.OpenCommand.ExecuteAsync(vm.Results[0]);

        await vm.ShareSelectedCommand.ExecuteAsync(null);

        Assert.Equal(
            new[] { "TSH.pdf (application/pdf, 3072 bytes)", "Bloods.pdf (application/pdf, 1024 bytes)" },
            _app.Viewer.Shared.Single());
        Assert.False(vm.IsSelecting);
        Assert.All(vm.Results, c => Assert.False(c.IsSelected));
        Assert.Null(vm.Notice);
    }

    [Fact]
    public async Task Select_all_ticks_every_result_and_again_unticks_them()
    {
        var vm = await ThreeResultsAsync();
        vm.StartSelectingCommand.Execute(null);

        vm.SelectAllCommand.Execute(null);
        Assert.All(vm.Results, c => Assert.True(c.IsSelected));
        Assert.Equal("3 selected", vm.SelectionText);

        vm.SelectAllCommand.Execute(null);
        Assert.All(vm.Results, c => Assert.False(c.IsSelected));
    }

    [Fact]
    public async Task Select_all_ticks_only_what_her_search_shows()
    {
        var vm = await ThreeResultsAsync();
        vm.SearchText = "scan";
        vm.StartSelectingCommand.Execute(null);

        vm.SelectAllCommand.Execute(null);
        vm.SearchText = string.Empty;
        await vm.ShareSelectedCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Scan.jpg (image/jpeg, 2048 bytes)" }, _app.Viewer.Shared.Single());
    }

    [Fact]
    public async Task Stop_back_or_leaving_ends_choosing_and_clears_every_tick()
    {
        var vm = await ThreeResultsAsync();
        Assert.False(vm.HandleBack());

        vm.SelectFromCommand.Execute(vm.Results[0]);
        vm.StopSelectingCommand.Execute(null);
        Assert.False(vm.IsSelecting);
        Assert.False(vm.Results[0].IsSelected);

        vm.SelectFromCommand.Execute(vm.Results[0]);
        Assert.True(vm.HandleBack());
        Assert.False(vm.IsSelecting);

        vm.SelectFromCommand.Execute(vm.Results[0]);
        vm.Leave();
        Assert.False(vm.IsSelecting);
        Assert.False(vm.Results[0].IsSelected);
        Assert.Empty(_app.Viewer.Shared);
    }

    [Fact]
    public async Task A_share_that_fails_keeps_her_ticks_and_says_so_calmly()
    {
        var vm = await ThreeResultsAsync();
        vm.SelectFromCommand.Execute(vm.Results[0]);
        _app.Viewer.FailNextShare = new InvalidOperationException("no share sheet");

        await vm.ShareSelectedCommand.ExecuteAsync(null);

        Assert.True(vm.IsSelecting);
        Assert.True(vm.Results[0].IsSelected);
        Assert.Equal("Something went wrong. Please try again.", vm.Notice);
        Assert.Equal("results.share", _app.Errors.Reports.Single().Operation);
    }

    [Fact]
    public async Task With_no_results_there_is_nothing_to_select()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();

        Assert.False(vm.CanStartSelecting);
    }

    [Theory]
    [InlineData("20-week scan", "image/jpeg", "20-week scan.jpg")]
    [InlineData("20-week scan.JPEG", "image/jpeg", "20-week scan.JPEG")]
    [InlineData("Bloods.pdf", "application/pdf", "Bloods.pdf")]
    [InlineData("Glucose: 2/3", "application/pdf", "Glucose_ 2_3.pdf")]
    [InlineData("   ", "image/png", "result.png")]
    [InlineData("notes", "application/octet-stream", "notes")]
    public void A_shared_file_is_named_safely_with_its_ending(string name, string mediaType, string expected) =>
        Assert.Equal(expected, ShareFiles.FileName(name, mediaType));

    [Theory]
    [InlineData("Photo Sat 3 Oct, 21:52.jpg", "Photo Sat 3 Oct, 21:52")]
    [InlineData("Bloods.PDF", "Bloods")]
    [InlineData("scan.jpeg", "scan")]
    [InlineData("IMG_2041.heic", "IMG_2041")]
    [InlineData("Week 20.5", "Week 20.5")]
    [InlineData("notes.txt", "notes.txt")]
    [InlineData(".pdf", ".pdf")]
    public void A_result_reads_without_its_file_ending(string name, string shown)
    {
        Assert.Equal(shown, ShareFiles.WithoutEnding(name));

        // Sent out of Maia, it has its ending again.
        Assert.EndsWith(".jpg", ShareFiles.FileName(ShareFiles.WithoutEnding("Photo Sat 3 Oct, 21:52.jpg"), "image/jpeg"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("application/pdf", "application/pdf")]
    [InlineData("application/pdf,application/pdf", "application/pdf")]
    [InlineData("image/jpeg,image/png", "image/*")]
    [InlineData("image/jpeg,application/pdf", "*/*")]
    public void The_share_sheet_is_asked_for_the_type_every_file_has(string types, string expected) =>
        Assert.Equal(expected, ShareFiles.CommonType(types.Split(',')));

    [Fact]
    public void Results_offers_select_by_holding_a_tile_and_a_share_bar_that_says_the_app_keeps_a_copy()
    {
        var text = File.ReadAllText(Repo.App("Views", "ResultsPage.xaml"));
        var code = File.ReadAllText(Repo.App("Views", "ResultsPage.xaml.cs"));

        Assert.Contains("{loc:Translate Results_Select}", text, StringComparison.Ordinal);
        Assert.Contains("DragStarting=\"OnTileHeld\"", text, StringComparison.Ordinal);
        Assert.Contains("ShareSelectedCommand", text, StringComparison.Ordinal);
        Assert.Contains("{loc:Translate Results_ShareNote}", text, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", code, StringComparison.Ordinal);
        Assert.Contains("HandleBack()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Lab_links_are_gone_from_every_string()
    {
        var keys = Localization.Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture);

        Assert.DoesNotContain(keys, key => key.Contains("Link", StringComparison.Ordinal) && key.StartsWith("Results_", StringComparison.Ordinal));
        Assert.DoesNotContain("Error_ResultIsLink", keys);
    }

    /// <summary>A report (Bloods, oldest), a scan photo, then another report (TSH, newest): newest first on screen.</summary>
    private async Task<ResultsViewModel> ThreeResultsAsync()
    {
        await _app.CreatePlanAsync();
        _app.Clock.SetToday(new DateOnly(2026, 9, 20));
        await _app.Plans.AttachFileAsync(new FileToAttach("Bloods.pdf", "application/pdf", new byte[1024]));
        _app.Clock.SetToday(new DateOnly(2026, 9, 25));
        await _app.Plans.AttachFileAsync(new FileToAttach("Scan.jpg", "image/jpeg", new byte[2048]));
        _app.Clock.SetToday(new DateOnly(2026, 9, 29));
        await _app.Plans.AttachFileAsync(new FileToAttach("TSH.pdf", "application/pdf", new byte[3072]));
        _app.Clock.SetToday(TestData.Today);
        var vm = _app.CreateResults();
        await vm.LoadAsync();
        return vm;
    }
}
