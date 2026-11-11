using System.Collections.Specialized;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// The Results screen, as the owner changed it: one list newest first, each tile saying what it is, the day it was
/// added and the entry it belongs to, and one search box instead of filters and tags. A file is removed where it opens.
/// </summary>
public sealed class ResultsScreenTests : IDisposable
{
    private static readonly PickedFile Report = new("Ferritin.pdf", "application/pdf", new byte[410 * 1024]);
    private static readonly PickedFile Photo = new("20-week scan.jpg", "image/jpeg", new byte[(int)(2.3 * 1024 * 1024)]);

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Results_are_one_list_newest_first_each_tile_saying_what_it_is_and_when_it_was_added()
    {
        await SomeResultsAsync();
        var vm = _app.CreateResults();

        await vm.LoadAsync();

        Assert.Equal(
            new[]
            {
                ("Ferritin", "PDF · Tue 29 Sep"),
                ("Iron", "PDF · Sun 27 Sep"),
                ("20-week scan", "Photo · Fri 25 Sep"),
                ("Booking bloods", "PDF · Sun 20 Sep"),
            },
            vm.Results.Select(r => (r.Name, r.Meta)));
        Assert.True(vm.CanSearch);
        Assert.True(vm.HasResults);
    }

    [Fact]
    public async Task Coming_back_or_searching_keeps_the_tiles_already_shown()
    {
        await SomeResultsAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();
        var report = vm.Results.Single(r => r.Name == "Ferritin");
        var changes = new List<NotifyCollectionChangedEventArgs>();
        vm.Results.CollectionChanged += (_, e) => changes.Add(e);

        await vm.LoadAsync();
        Assert.Empty(changes);

        vm.SearchText = "ferritin";
        vm.SearchText = string.Empty;

        Assert.Same(report, vm.Results[0]);
        Assert.Equal(4, vm.Results.Count);
    }

    [Fact]
    public async Task Upload_results_keeps_them_in_her_results()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();
        Assert.False(vm.HasResults);
        Assert.False(vm.CanSearch);
        Assert.Equal("No results yet. Add the report or a photo of it.", vm.EmptyText);
        _app.Files.Picks.Enqueue([Report, Photo]);

        await vm.UploadCommand.ExecuteAsync(null);

        var stored = _app.Repository.Stored!;
        Assert.Equal(new[] { "Ferritin.pdf", "20-week scan.jpg" }, stored.Results.Select(r => r.DisplayName));
        Assert.Equal("2 files added to your results.", _app.Notifier.Shown.Single());
        Assert.Equal(2, vm.Results.Count);
        Assert.True(vm.CanSearch);
        Assert.Empty(_app.Chooser.Asked);
    }

    [Fact]
    public async Task Search_finds_a_result_by_its_name_and_never_by_a_journey_entry()
    {
        await SomeResultsAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();

        vm.SearchText = "FERRI";
        Assert.Equal("Ferritin", Assert.Single(vm.Results).Name);

        // Every word must match.
        vm.SearchText = "bloods pdf";
        Assert.Equal("Booking bloods", Assert.Single(vm.Results).Name);

        // Results stand apart from her journey: an entry's name finds nothing.
        vm.SearchText = "anomaly";
        Assert.Empty(vm.Results);
        vm.SearchText = "booking visit";
        Assert.Empty(vm.Results);
    }

    [Fact]
    public async Task Search_finds_a_result_by_what_it_is_or_the_month_and_day_it_was_added()
    {
        await SomeResultsAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();

        vm.SearchText = "photo";
        Assert.Equal("20-week scan", Assert.Single(vm.Results).Name);

        vm.SearchText = "report";
        Assert.Equal(new[] { "Ferritin", "Iron", "Booking bloods" }, vm.Results.Select(r => r.Name));

        vm.SearchText = "sep";
        Assert.Equal(4, vm.Results.Count);

        vm.SearchText = "25 september";
        Assert.Equal("20-week scan", Assert.Single(vm.Results).Name);

        vm.SearchText = "σεπτεμβριου";
        Assert.Equal(4, vm.Results.Count);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_says_so()
    {
        await SomeResultsAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();

        vm.SearchText = "glucose";

        Assert.False(vm.HasResults);
        Assert.Equal("Nothing matches your search.", vm.EmptyText);
        Assert.True(vm.CanSearch);
    }

    [Theory]
    [InlineData("Υπερηχογράφημα β' επιπέδου", "υπερηχογραφημα", true)]
    [InlineData("anomaly scan", "Scan ANOMALY", true)]
    [InlineData("anomaly scan", "anomaly bloods", false)]
    [InlineData("σκάνς", "σκανσ", true)]
    [InlineData("anything", "   ", true)]
    public void Search_ignores_case_accents_and_word_order(string text, string query, bool matches) =>
        Assert.Equal(matches, ResultsViewModel.Matches(ResultsViewModel.Fold(text), query));

    [Fact]
    public async Task What_she_uploads_shows_with_the_search_cleared()
    {
        await SomeResultsAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();
        vm.SearchText = "anomaly";
        _app.Files.Picks.Enqueue([new PickedFile("TSH.pdf", "application/pdf", new byte[1024])]);

        await vm.UploadCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal("TSH", vm.Results[0].Name);
        Assert.Equal(5, vm.Results.Count);
    }

    [Fact]
    public async Task A_photo_from_results_is_kept_and_a_file_opens_in_maias_viewer_without_asking()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateResults();
        await vm.LoadAsync();
        _app.Files.Photos.Enqueue(new PickedFile("0b1f.jpg", "image/jpeg", new byte[900 * 1024]));

        await vm.TakePhotoCommand.ExecuteAsync(null);

        Assert.Single(_app.Repository.Stored!.Results);
        var photo = Assert.Single(vm.Results);
        Assert.Equal("Photo Wed 30 Sep, 12:00", photo.Name);
        await vm.OpenCommand.ExecuteAsync(photo);
        Assert.Equal(new[] { "viewer" }, _app.Navigator.Visited);
        Assert.Empty(_app.Chooser.Asked);
    }

    [Fact]
    public void Results_has_no_way_to_paste_a_link_no_filters_no_tags_and_one_search_box()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "ResultsPage.xaml"));
        var text = File.ReadAllText(Repo.App("Views", "ResultsPage.xaml"));
        var search = page.Descendants().Single(e => AccessibilityTests.Attr(e, "Text") == "{Binding SearchText}");

        Assert.Equal("Entry", search.Name.LocalName);
        Assert.Equal("{Binding CanSearch}", AccessibilityTests.Attr(search.Parent!.Parent!.Parent!, "IsVisible"));
        Assert.DoesNotContain("SaveLinkCommand", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LinkForm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Results_Link", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TagLine", text, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveResultCommand", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task In_greek_the_screen_reads_greek()
    {
        await SomeResultsAsync();
        using var greek = new CultureScope("el-GR");
        var vm = _app.CreateResults();

        await vm.LoadAsync();

        Assert.Equal("PDF · Τρί 29 Σεπ", vm.Results[0].Meta);

        vm.SearchText = "xyz";
        Assert.Equal("Τίποτα δεν ταιριάζει με την αναζήτηση.", vm.EmptyText);
    }

    [Fact]
    public void Results_opens_on_its_title_with_no_file_count_above_it()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "ResultsPage.xaml"));
        var heading = page.Descendants().First(e => AccessibilityTests.Attr(e, "Text") == "{loc:Translate Results_Title}");
        var keys = Localization.Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Empty(heading.ElementsBeforeSelf());
        Assert.DoesNotContain(page.Descendants(), e => AccessibilityTests.Attr(e, "Text") == "{Binding Eyebrow}");
        Assert.Null(typeof(ResultsViewModel).GetProperty("Eyebrow"));
        Assert.DoesNotContain(keys, key => key.StartsWith("Results_Eyebrow", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Each_tile_shows_a_small_picture_of_its_photo_or_its_first_page_drawn_once()
    {
        await SomeResultsAsync();
        var vm = _app.CreateResults();

        await vm.LoadAsync();
        await vm.ThumbnailsDrawn;

        Assert.All(vm.Results, card => Assert.True(card.HasThumbnail, card.Name));
        Assert.Equal(new[] { ("20-week scan.jpg", ResultThumbnails.Width, 80) }, _app.Photos.Reencoded);
        Assert.Equal(new[] { (0, ResultThumbnails.Width) }, _app.Renderer.Opened!.Rendered);
        Assert.True(_app.Renderer.Opened.Disposed);

        // Coming back: each tile keeps its picture, and none is drawn again.
        await vm.LoadAsync();
        await vm.ThumbnailsDrawn;
        Assert.Single(_app.Photos.Reencoded);
        Assert.All(vm.Results, card => Assert.True(card.HasThumbnail, card.Name));
    }

    [Fact]
    public async Task A_file_the_phone_cannot_draw_keeps_its_icon_without_a_word()
    {
        await SomeResultsAsync();
        _app.Renderer.NextPdf = null;
        var vm = _app.CreateResults();

        await vm.LoadAsync();
        await vm.ThumbnailsDrawn;

        Assert.All(vm.Results.Where(r => !r.IsPhoto), card => Assert.False(card.HasThumbnail, card.Name));
        Assert.True(vm.Results.Single(r => r.IsPhoto).HasThumbnail);
        Assert.Empty(_app.Errors.Reports);
        Assert.Null(vm.Notice);
    }

    /// <summary>
    /// A report (20 Sep), a scan photo (25 Sep), an iron report (27 Sep) and another report (29 Sep).
    /// </summary>
    private async Task SomeResultsAsync()
    {
        await _app.CreatePlanAsync();
        _app.Clock.SetToday(new DateOnly(2026, 9, 20));
        await _app.Plans.AttachFileAsync(new FileToAttach("Booking bloods.pdf", "application/pdf", new byte[240 * 1024]));
        _app.Clock.SetToday(new DateOnly(2026, 9, 25));
        await _app.Plans.AttachFileAsync(new FileToAttach(Photo.Name, Photo.MediaType, new byte[(int)Photo.Size!.Value]));
        _app.Clock.SetToday(new DateOnly(2026, 9, 27));
        await _app.Plans.AttachFileAsync(new FileToAttach("Iron.pdf", "application/pdf", new byte[1024]));
        _app.Clock.SetToday(new DateOnly(2026, 9, 29));
        await _app.Plans.AttachFileAsync(new FileToAttach(Report.Name, Report.MediaType, new byte[(int)Report.Size!.Value]));
        _app.Clock.SetToday(TestData.Today);
    }
}
