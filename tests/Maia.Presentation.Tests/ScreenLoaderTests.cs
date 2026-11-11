using Maia.Application.Common;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Startup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// A screen loaded from a page event never ends Maia: a failure is reported by code and said calmly, and a plan that
/// can't be opened, or a first Your week that can't load, hands over to the start-up screen. Today is 24+3.
/// </summary>
public sealed class ScreenLoaderTests : IDisposable
{
    private const string CouldNotShow = "Maia couldn't show this just now. Try again in a moment.";

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();
    private readonly StartupFlow _startup;
    private readonly ScreenLoader _loader;

    public ScreenLoaderTests()
    {
        _startup = _app.CreateStartupFlow();
        _loader = new ScreenLoader(_startup, _app.Navigator, _app.Notifier, _app.Errors);
    }

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Opened_straight_on_your_week_a_week_that_cannot_load_hands_over_to_start_up_once_without_a_toast(bool planUnreadable)
    {
        await _app.CreatePlanAsync();
        Assert.True(_startup.OpensOnWeek(returning: false));
        var week = _app.CreateWeek();
        if (planUnreadable)
        {
            _app.Repository.Unreadable = PlanUnreadableException.Damaged(new InvalidDataException("both copies"));
        }
        else
        {
            _app.Repository.FailNextLoad = new IOException("storage not ready");
        }

        Assert.False(await _loader.LoadAsync(week.LoadAsync, "week.load"));

        Assert.Equal(new[] { "startup" }, _app.Navigator.Visited);
        Assert.Empty(_app.Notifier.Shown);
        Assert.Equal("week.load", Assert.Single(_app.Errors.Reports).Operation);

        // Later, once start-up has taken over, a screen that can't load for a moment says so where she is.
        _app.Repository.Unreadable = null;
        _app.Repository.FailNextLoad = new IOException("storage not ready");

        Assert.False(await _loader.LoadAsync(week.LoadAsync, "week.load"));

        Assert.Equal(new[] { "startup" }, _app.Navigator.Visited);
        Assert.Equal(CouldNotShow, Assert.Single(_app.Notifier.Shown));
    }

    [Fact]
    public async Task A_screen_that_cannot_load_for_a_moment_says_so_calmly_and_stays_where_she_is()
    {
        await _app.CreatePlanAsync();
        _app.Repository.FailNextLoad = new IOException("Couldn't read Glucose test");

        Assert.False(await _loader.LoadAsync(_app.CreateWeek().LoadAsync, "week.load"));

        Assert.Empty(_app.Navigator.Visited);
        Assert.Equal(CouldNotShow, Assert.Single(_app.Notifier.Shown));
        var report = Assert.Single(_app.Errors.Reports);
        Assert.Equal("week.load", report.Operation);
    }

    [Fact]
    public async Task A_plan_that_cannot_be_opened_on_any_screen_hands_over_to_start_up()
    {
        await _app.CreatePlanAsync();
        _app.Repository.Unreadable = PlanUnreadableException.Damaged(new InvalidDataException("both copies"));

        Assert.False(await _loader.LoadAsync(_app.CreateWeek().LoadAsync, "plan.load"));

        Assert.Equal(new[] { "startup" }, _app.Navigator.Visited);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Opened_straight_on_your_week_in_another_language_it_loads_again_with_her_checks_renamed()
    {
        await _app.CreatePlanAsync();
        using var greek = new CultureScope("el-GR");
        _startup.OpensOnWeek(returning: false);
        var week = _app.CreateWeek();
        var loads = 0;

        Assert.True(await _loader.LoadAsync(
            () =>
            {
                loads++;
                return week.LoadAsync();
            },
            "week.load"));
        await _startup.Tidying;

        Assert.Equal(2, loads);
        Assert.Equal("el", _app.Repository.Stored!.Language);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Opened_straight_on_your_week_in_her_language_it_loads_once()
    {
        await _app.CreatePlanAsync();
        _startup.OpensOnWeek(returning: false);
        var week = _app.CreateWeek();
        var loads = 0;

        Assert.True(await _loader.LoadAsync(
            () =>
            {
                loads++;
                return week.LoadAsync();
            },
            "week.load"));
        await _startup.Tidying;

        Assert.Equal(1, loads);
        Assert.False(_startup.IsPending);
    }

    [Fact]
    public async Task Other_work_from_a_page_event_that_fails_is_only_reported_by_its_code()
    {
        await _loader.RunAsync(() => throw new InvalidOperationException("Glucose test"), "week.keyboard");

        Assert.Equal("week.keyboard", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Empty(_app.Notifier.Shown);
        Assert.Empty(_app.Navigator.Visited);
    }
}
