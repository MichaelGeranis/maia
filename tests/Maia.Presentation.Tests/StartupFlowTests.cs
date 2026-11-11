using Maia.Domain.Templates;
using Maia.Presentation.Localization;
using Maia.Presentation.Results;
using Maia.Presentation.Setup;
using Maia.Presentation.Startup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// With her plan on the phone and nothing else first, Maia opens straight on Your week; the rest of start-up follows
/// once it has loaded, and the start-up screen takes over only when it can't.
/// </summary>
public sealed class StartupFlowTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private StartupFlow Create() => _app.CreateStartupFlow();

    [Fact]
    public async Task Maia_opens_straight_on_your_week_when_her_plan_is_on_the_phone()
    {
        await _app.CreatePlanAsync();

        Assert.True(Create().OpensOnWeek(returning: false));
    }

    [Fact]
    public async Task No_plan_a_share_or_a_language_switch_starts_on_the_start_up_screen()
    {
        Assert.False(Create().OpensOnWeek(returning: false));

        await _app.CreatePlanAsync();
        Assert.False(Create().OpensOnWeek(returning: true));

        _app.Shares.Receive([new PickedFile("report.pdf", "application/pdf", new byte[16])]);
        Assert.False(Create().OpensOnWeek(returning: false));
    }

    [Fact]
    public async Task Once_your_week_has_loaded_her_plan_follows_the_app_language_and_old_files_are_tidied()
    {
        await _app.CreatePlanAsync();
        _app.Attachments.Now = () => _app.Clock.UtcNow.AddDays(-1);
        await _app.Attachments.SaveAsync(new byte[] { 1 }, CancellationToken.None);
        using var greek = new CultureScope("el-GR");
        var flow = Create();
        flow.OpensOnWeek(returning: false);

        var loadAgain = await flow.LoadedAsync();
        await flow.Tidying;

        Assert.True(loadAgain);
        Assert.Equal("el", _app.Repository.Stored!.Language);
        Assert.Empty(_app.Attachments.Blobs);
        Assert.False(await flow.LoadedAsync());
    }

    [Fact]
    public async Task A_plan_already_in_the_app_language_needs_no_second_look()
    {
        await _app.CreatePlanAsync();
        var flow = Create();
        flow.OpensOnWeek(returning: false);
        var saves = _app.Repository.SaveCount;

        Assert.False(await flow.LoadedAsync());
        Assert.Equal(saves, _app.Repository.SaveCount);
    }

    [Fact]
    public async Task If_your_week_cannot_load_the_start_up_screen_takes_over_once()
    {
        await _app.CreatePlanAsync();
        var flow = Create();
        flow.OpensOnWeek(returning: false);

        Assert.True(flow.Failed());
        Assert.False(flow.Failed());
        Assert.False(await flow.LoadedAsync());
        Assert.False(flow.IsPending);
    }

    [Fact]
    public void Later_screens_that_fail_never_send_her_to_start_up()
    {
        var flow = Create();
        flow.OpensOnWeek(returning: false);

        Assert.False(flow.Failed());
    }

    [Fact]
    public async Task Starting_locked_maia_shows_the_start_up_screen_which_opens_her_plan_once_she_has_unlocked()
    {
        await _app.CreatePlanAsync();
        var flow = Create();

        Assert.False(flow.OpensOnWeek(returning: false, locked: true));
        Assert.False(flow.IsPending);
    }

    [Fact]
    public async Task Opened_straight_on_your_week_her_plan_is_still_brought_up_to_date_once_it_has_loaded()
    {
        var current = TestData.Template();
        var renamed = current.Checks.Select(c => c.Id == TestData.Glucose
            ? new CheckTemplate(c.Id, TestData.Text("Glucose tolerance test", "Καμπύλη σακχάρου"), c.Description, c.Category, c.Window, c.IncludedByDefault)
            : c);
        using var app = new TestApp(new PlanTemplate(current.Id, current.CountryCode, 2, ReviewStatus.ClinicallyReviewed, renamed, "Dr. Maria Example, OB-GYN", new DateOnly(2026, 9, 1)));
        await app.Repository.SaveAsync(TestData.Plan(), CancellationToken.None);
        app.Setup.DueDate = TestData.DueDate;
        await app.Setup.SaveAsync(SetupStep.Care);
        var flow = app.CreateStartupFlow();
        Assert.True(flow.OpensOnWeek(returning: false));

        var loadAgain = await flow.LoadedAsync();

        Assert.True(loadAgain);
        var plan = app.Repository.Stored!;
        Assert.Equal("Glucose tolerance test", plan.GetCheck(TestData.Glucose).Title.In("en"));
        Assert.Equal("Europe/Athens", plan.TimeZoneId);
        Assert.Empty(app.Drafts.Drafts);
        Assert.Equal("Maia has updated how your checks are described. Your bookings and results stay as they are.", app.Notifier.Shown.Single());
    }
}
