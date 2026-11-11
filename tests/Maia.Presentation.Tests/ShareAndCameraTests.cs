using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Presentation.Plan;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Results that arrive by camera or from another app: a photo of a paper report, a PDF from a lab's email.</summary>
public sealed class ShareAndCameraTests : IDisposable
{
    private static readonly PickedFile Report = new("NIPT result.pdf", "application/pdf", new byte[410 * 1024]);

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task A_photo_from_the_camera_is_kept_in_her_results_without_its_location()
    {
        await _app.CreatePlanAsync();
        var results = Results();
        await results.LoadAsync();
        var cleaned = 0;
        _app.Files.Photos.Enqueue(new PickedFile("0b1f.jpg", "image/jpeg", new byte[900 * 1024]) { Cleanup = () => cleaned++ });

        Assert.True(results.CanTakePhoto);
        await results.TakePhotoCommand.ExecuteAsync(null);

        Assert.Equal(("Photo Wed 30 Sep, 12:00.jpg", 900 * 1024L), (Assert.Single(_app.Repository.Stored!.Results).DisplayName, _app.Repository.Stored!.Results[0].SizeBytes));
        Assert.Equal("1 file added to your results.", _app.Notifier.Shown.Single());
        Assert.Equal(1, cleaned);
        Assert.Equal(1, _app.Photos.LocationsWiped);
    }

    [Fact]
    public async Task Closing_the_camera_without_a_photo_changes_nothing()
    {
        await _app.CreatePlanAsync();
        var results = Results();
        await results.LoadAsync();

        await results.TakePhotoCommand.ExecuteAsync(null);

        Assert.Empty(results.Results);
        Assert.Null(results.Notice);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Without_a_camera_she_is_told_calmly_and_nothing_is_reported()
    {
        await _app.CreatePlanAsync();
        var results = Results();
        await results.LoadAsync();
        _app.Files.CameraUnavailable = true;

        await results.TakePhotoCommand.ExecuteAsync(null);

        Assert.Equal("The camera isn't available right now. You can add a photo from your gallery instead.", results.Notice);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Results_can_take_a_photo_too()
    {
        await _app.CreatePlanAsync();
        var results = Results();
        await results.LoadAsync();
        _app.Files.Photos.Enqueue(new PickedFile("b.jpg", "image/jpeg", new byte[1024]));

        await results.TakePhotoCommand.ExecuteAsync(null);

        Assert.Single(_app.Repository.Stored!.Results);
        Assert.Equal("Photo Wed 30 Sep, 12:00.jpg", Assert.Single(_app.Repository.Stored!.Results).DisplayName);
        Assert.Equal("1 file added to your results.", _app.Notifier.Shown.Last());
        Assert.Equal("Photo Wed 30 Sep, 12:00", Assert.Single(results.Results).Name);
    }

    [Fact]
    public async Task A_file_shared_from_another_app_is_kept_as_results_opens_without_asking_anything()
    {
        await _app.CreatePlanAsync();
        var cleaned = 0;
        _app.Shares.Receive([Report with { Cleanup = () => cleaned++ }]);

        Assert.True(await _app.Shares.OpenAsync());
        Assert.Equal(new[] { "results" }, _app.Navigator.Visited);

        var vm = Results();
        await vm.LoadAsync();

        // Choosing Maia in the share sheet was her yes: nothing is asked before or after it is kept.
        Assert.Empty(_app.Shares.Pending);
        Assert.Equal(1, cleaned);
        Assert.Equal("NIPT result.pdf", Assert.Single(_app.Repository.Stored!.Results).DisplayName);
        Assert.Equal("1 file added to your results.", _app.Notifier.Shown.Single());
        Assert.Equal("NIPT result", Assert.Single(vm.Results).Name);
        Assert.Empty(_app.Chooser.Asked);
    }

    [Fact]
    public async Task Several_shared_files_are_kept_together()
    {
        await _app.CreatePlanAsync();
        _app.Shares.Receive([Report, new PickedFile("scan.jpg", "image/jpeg", new byte[1024])]);
        var vm = Results();
        await vm.LoadAsync();

        Assert.Equal("2 files added to your results.", _app.Notifier.Shown.Last());
        Assert.Equal(new[] { "NIPT result.pdf", "scan.jpg" }, _app.Repository.Stored!.Results.Select(r => r.DisplayName));
        Assert.Equal(2, vm.Results.Count);
    }

    [Fact]
    public async Task A_file_shared_while_results_shows_is_kept_once_and_shows_whatever_the_search_was()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new FileToAttach("Ferritin.pdf", "application/pdf", new byte[1024]));
        var vm = Results();
        await vm.LoadAsync();
        vm.SearchText = "ferritin";

        _app.Clock.SetTime(TestData.Today, new TimeOnly(13, 0));
        _app.Shares.Receive([Report]);
        await vm.KeepSharedAsync();

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(new[] { "NIPT result", "Ferritin" }, vm.Results.Select(r => r.Name));

        // The page hears of it as Results loads again too: it is kept only once.
        await vm.KeepSharedAsync();
        await vm.LoadAsync();

        Assert.Equal(2, _app.Repository.Stored!.Results.Count);
        Assert.Single(_app.Notifier.Shown);
    }

    [Fact]
    public async Task A_shared_file_maia_cannot_keep_is_explained()
    {
        await _app.CreatePlanAsync();
        var cleaned = 0;
        _app.Shares.Receive([new PickedFile("notes.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new byte[1024]) { Cleanup = () => cleaned++ }]);
        var vm = Results();

        await vm.LoadAsync();

        Assert.Equal("Maia keeps PDFs and photos. That file type can't be added.", vm.Notice);
        Assert.Empty(_app.Repository.Stored!.Results);
        Assert.Empty(_app.Shares.Pending);
        Assert.Equal(1, cleaned);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Sharing_before_there_is_a_plan_says_so_and_goes_to_setup()
    {
        var cleaned = 0;
        _app.Shares.Receive([Report with { Cleanup = () => cleaned++ }]);
        var toastsOnTheWay = -1;
        _app.Navigator.WhenVisiting = _ => toastsOnTheWay = _app.Notifier.Shown.Count;

        Assert.True(await _app.Shares.OpenAsync());

        // Said once setup shows, not on the screen that is closing.
        Assert.Equal(0, toastsOnTheWay);
        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.Equal("Set up your journey first, then share the file with Maia again.", _app.Notifier.Shown.Single());
        Assert.Empty(_app.Shares.Pending);
        Assert.Equal(1, cleaned);
    }

    [Fact]
    public async Task Nothing_shared_opens_nothing() =>
        Assert.False(await _app.Shares.OpenAsync());

    private ResultsViewModel Results() => _app.CreateResults();
}
