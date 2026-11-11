using Maia.Application.Plans;
using Maia.Domain.Plans;
using Maia.Presentation.Results;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Results: one read of the plan per open, and a save failing part-way (review #44, #72).</summary>
public sealed class ResultFollowUpTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Theory]
    [InlineData("scan.pdf", "application/pdf", true)]
    [InlineData("scan.heic", "image/heic", false)]
    public async Task Opening_a_file_reads_the_plan_once_whether_maia_can_draw_it_or_not(string name, string mediaType, bool drawsHeic)
    {
        await _app.CreatePlanAsync();
        _app.Renderer.DrawsHeic = drawsHeic;
        await _app.Plans.AttachFileAsync(new FileToAttach(name, mediaType, new byte[2048]));
        var result = ResultItem.From(_app.Repository.Stored!.Results.Single());
        var loads = _app.Repository.LoadCount;

        var message = await _app.Filing.OpenAsync(result);
        await new ResultViewerViewModel(_app.Plans, _app.Viewing, _app.Renderer, _app.Viewer, _app.Navigator, _app.Notifier, _app.Errors).LoadAsync(1080);

        Assert.Null(message);
        Assert.Equal(new[] { "viewer" }, _app.Navigator.Visited);
        Assert.Empty(_app.Viewer.Opened);
        Assert.Equal(loads + 1, _app.Repository.LoadCount);
    }

    [Fact]
    public async Task A_save_failing_part_way_through_several_files_stops_calmly_and_keeps_what_was_filed()
    {
        await _app.CreatePlanAsync();
        var opened = new List<string>();
        var cleaned = new List<string>();
        PickedFile File(string name, Action? whenOpened = null) =>
            new(name, "application/pdf", 1024, () =>
            {
                opened.Add(name);
                whenOpened?.Invoke();
                return Task.FromResult<Stream>(new MemoryStream(new byte[1024]));
            })
            { Cleanup = () => cleaned.Add(name) };

        var added = await _app.Filing.KeepAsync(
            [File("one.pdf"), File("two.pdf", () => _app.Repository.FailNextSave = new IOException("disk full")), File("three.pdf")]);

        Assert.Single(added.Ids);
        Assert.Equal("Something went wrong. Please try again.", added.Message);
        Assert.Equal(new[] { "one.pdf", "two.pdf" }, opened);
        Assert.Equal(new[] { "one.pdf", "two.pdf", "three.pdf" }, cleaned);
        Assert.Equal("one.pdf", Assert.Single(_app.Repository.Stored!.Results).DisplayName);
        Assert.Single(_app.Attachments.Blobs);
        Assert.Equal("results.attach", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Equal("1 file added to your results.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Deleting_a_done_check_from_journey_leaves_her_results_as_they_are()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(Pdf("fasting.pdf"));
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 9, 28), new TimeOnly(8, 0)));
        var plan = new Plan.PlanViewModel(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);
        await plan.LoadAsync();
        var glucose = plan.Rows.OfType<Plan.CheckRow>().Single(r => r.Id == TestData.Glucose);
        glucose.ToggleCommand.Execute(null);
        Assert.Equal("Done", glucose.StatusText);

        glucose.AskDeleteCommand.Execute(null);
        await glucose.DeleteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(_app.Repository.Stored!.Checks, c => c.Id == TestData.Glucose);
        var results = _app.CreateResults();
        await results.LoadAsync();
        Assert.Equal("fasting", Assert.Single(results.Results).Name);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task A_result_opens_in_the_viewer_and_can_be_removed()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(Pdf("bloods.pdf"));
        var result = ResultItem.From(Assert.Single(_app.Repository.Stored!.Results));
        var viewer = new ResultViewerViewModel(_app.Plans, _app.Viewing, _app.Renderer, _app.Viewer, _app.Navigator, _app.Notifier, _app.Errors);

        Assert.Null(await _app.Filing.OpenAsync(result));
        await viewer.LoadAsync(1080);
        Assert.Equal("bloods", viewer.Title);
        Assert.Null(viewer.Notice);

        Assert.Null(await _app.Filing.RemoveAsync(result));
        Assert.Empty(_app.Repository.Stored!.Results);
        Assert.Empty(_app.Attachments.Blobs);
    }

    private static FileToAttach Pdf(string name) => new(name, "application/pdf", new byte[1024]);
}
