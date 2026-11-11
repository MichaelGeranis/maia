using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Results kept, turned away or shared when the phone, the file or the store does something unexpected.</summary>
public sealed class ResultsFilingEdgeTests : IDisposable
{
    private static readonly PickedFile Report = new("Anomaly scan report.pdf", "application/pdf", new byte[410 * 1024]);

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task A_picker_that_fails_says_something_went_wrong_and_reports_the_code_only()
    {
        await _app.CreatePlanAsync();
        var filing = FilingWith(new FailingPicker(new InvalidOperationException("The file provider stopped.")));

        var added = await filing.UploadToResultsAsync();

        Assert.Empty(added.Ids);
        Assert.Equal("Something went wrong. Please try again.", added.Message);
        Assert.Equal("results.pick", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Empty(_app.Repository.Stored!.Results);
    }

    [Fact]
    public async Task A_camera_that_fails_says_something_went_wrong_and_reports_the_code_only()
    {
        await _app.CreatePlanAsync();
        var filing = FilingWith(new FailingPicker(new IOException("The camera app crashed.")));

        var added = await filing.TakePhotoToResultsAsync();

        Assert.Empty(added.Ids);
        Assert.Equal("Something went wrong. Please try again.", added.Message);
        Assert.Equal("results.camera", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_camera_switched_off_with_a_reason_is_explained_calmly_and_not_reported()
    {
        await _app.CreatePlanAsync();
        var reason = new UnauthorizedAccessException("Camera disabled by policy.");
        var unavailable = new CameraUnavailableException("The camera is switched off.", reason);
        var filing = FilingWith(new FailingPicker(unavailable));

        var added = await filing.TakePhotoToResultsAsync();

        Assert.Equal("The camera isn't available right now. You can add a photo from your gallery instead.", added.Message);
        Assert.Empty(_app.Errors.Reports);
        Assert.Same(reason, unavailable.InnerException);
        Assert.Equal("No camera app.", new CameraUnavailableException("No camera app.").Message);
    }

    [Fact]
    public async Task A_file_handed_over_without_content_is_turned_away_as_empty()
    {
        await _app.CreatePlanAsync();
        var nothing = new PickedFile("blank.pdf", "application/pdf", (byte[])null!);

        var added = await _app.Filing.KeepAsync([nothing]);

        Assert.Null(nothing.Size);
        Assert.Equal("That file is empty.", added.Message);
        Assert.Empty(_app.Repository.Stored!.Results);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Theory]
    [InlineData("too_large", "Files up to 25 MB can be added.")]
    [InlineData("empty", "That file is empty.")]
    [InlineData("type", "Maia keeps PDFs and photos. That file type can't be added.")]
    public async Task A_file_the_store_turns_away_is_explained_by_why_and_not_reported(string why, string message)
    {
        var store = new ScriptedAttachmentStore { RejectSaves = Rejection(why) };
        using var plans = PlansOver(store);
        await _app.CreatePlanAsync();
        var filing = new ResultFiling(plans, _app.Files, _app.Photos, _app.Viewer, _app.Viewing, _app.Notifier, _app.Clock, _app.Errors);

        var added = await filing.KeepAsync([Report]);

        Assert.Empty(added.Ids);
        Assert.Equal(message, added.Message);
        Assert.Empty(_app.Errors.Reports);
        Assert.Empty(_app.Repository.Stored!.Results);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task One_file_turned_away_among_several_is_named_on_its_own()
    {
        await _app.CreatePlanAsync();

        var added = await _app.Filing.KeepAsync([Report, new PickedFile("notes.docx", "application/msword", new byte[10])]);

        Assert.Single(added.Ids);
        Assert.Equal("One file couldn't be added: notes.docx (not a PDF or photo).", added.Message);
        Assert.Equal("1 file added to your results.", Assert.Single(_app.Notifier.Shown));
    }

    [Fact]
    public async Task A_file_that_can_be_measured_is_read_whole_only_up_to_the_limit()
    {
        var file = new PickedFile("scan.pdf", "application/pdf", new byte[100]);

        Assert.Null(await ResultFiling.ReadAsync(file, 50));
        Assert.Equal(100, (await ResultFiling.ReadAsync(file, 100))!.Length);
    }

    [Theory]
    [InlineData(null, 0L, 0)]
    [InlineData(null, 60L, -1)]
    [InlineData(0L, 10L, 10)]
    [InlineData(10L, 10L, 10)]
    [InlineData(20L, 10L, 10)]
    [InlineData(100L, 10L, 10)]
    public async Task A_file_read_forward_is_kept_whole_whatever_size_the_phone_said_unless_it_passes_the_limit(long? said, long actual, int expected)
    {
        var file = new PickedFile("portal.pdf", "application/pdf", said, () => Task.FromResult<Stream>(new ForwardOnlyStream(actual)));

        var content = await ResultFiling.ReadAsync(file, 50);

        Assert.Equal(expected, content?.Length ?? -1);
    }

    [Theory]
    [InlineData("too_large", "Files up to 25 MB can be added.")]
    [InlineData("empty", "That file is empty.")]
    [InlineData("type", "Maia keeps PDFs and photos. That file type can't be added.")]
    public async Task A_share_the_phone_turns_away_says_why_and_is_not_reported(string why, string message)
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new FileToAttach(Report.Name, Report.MediaType, new byte[1024]));
        var result = ResultItem.From(Assert.Single(_app.Repository.Stored!.Results));
        _app.Viewer.FailNextShare = Rejection(why);

        var said = await _app.Filing.ShareAsync([result]);

        Assert.Equal(message, said);
        Assert.Empty(_app.Errors.Reports);
        Assert.Empty(_app.Viewer.Shared);
    }

    private static AttachmentRejectedException Rejection(string why) => why switch
    {
        "too_large" => AttachmentRejectedException.TooLarge(AttachmentPolicy.MaxSizeBytes),
        "empty" => AttachmentRejectedException.Empty(),
        _ => AttachmentRejectedException.UnsupportedType("application/pdf"),
    };

    private ResultFiling FilingWith(IResultPicker picker) =>
        new(_app.Plans, picker, _app.Photos, _app.Viewer, _app.Viewing, _app.Notifier, _app.Clock, _app.Errors);

    private PlanService PlansOver(ScriptedAttachmentStore store) =>
        new(_app.Repository, _app.Templates, store, _app.DataKey, _app.Backups, _app.Clock, _app.Errors, _app.Reminders);
}
