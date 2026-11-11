using Maia.Domain.Plans;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Startup;
using Maia.Presentation.Week;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Your week and Your plan know when a new day has begun, so "tomorrow" becomes "today": they load again at midnight
/// while they show and when Maia comes back to the foreground. The fake clock stands at noon in Athens.
/// </summary>
public sealed class DayChangeTests : IDisposable
{
    private static readonly TimeSpan UntilMidnight = TimeSpan.FromHours(12) + DayWatch.Margin;

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();
    private readonly FakeWakeTimer _timer = new();
    private int _loads;

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public void A_screen_loaded_yesterday_knows_a_new_day_has_begun()
    {
        var day = new DayWatch(_app.Clock);
        Assert.False(day.IsNewDay);

        day.Loaded();
        Assert.False(day.IsNewDay);

        _app.Clock.SetToday(TestData.Today.AddDays(1));
        Assert.True(day.IsNewDay);

        day.Loaded();
        Assert.False(day.IsNewDay);
    }

    [Fact]
    public void The_wait_for_the_next_day_ends_just_after_midnight_on_her_phone()
    {
        var day = new DayWatch(_app.Clock);

        Assert.Equal(UntilMidnight, day.UntilNextDay);

        _app.Clock.TimeZone = TimeZoneInfo.Utc;
        Assert.Equal(TimeSpan.FromHours(15) + DayWatch.Margin, day.UntilNextDay);
    }

    [Fact]
    public async Task Your_week_loaded_again_after_midnight_reads_the_new_day()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(TestData.Today.AddDays(1), new TimeOnly(10, 30)));
        var week = _app.CreateWeek();
        await week.LoadAsync();
        Assert.Equal("Week 24 + 3", week.WeekLabel);

        _app.Clock.SetToday(TestData.Today.AddDays(1));
        await week.LoadAsync();

        Assert.Equal("Week 24 + 4", week.WeekLabel);
    }

    [Fact]
    public async Task Your_week_showing_at_midnight_loads_again_and_waits_for_the_next_day()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(TestData.Today.AddDays(1), new TimeOnly(10, 30)));
        var week = _app.CreateWeek();
        var live = Live(week);

        await live.AppearedAsync();

        Assert.Equal(1, _loads);
        Assert.Equal("Week 24 + 3", week.WeekLabel);
        Assert.Equal(UntilMidnight, _timer.Wait);

        _app.Clock.SetToday(TestData.Today.AddDays(1));
        await live.WokeAsync();

        Assert.Equal(2, _loads);
        Assert.Equal("Week 24 + 4", week.WeekLabel);
        Assert.Equal(UntilMidnight, _timer.Wait);
    }

    [Fact]
    public async Task Woken_before_the_new_day_it_waits_again_without_loading()
    {
        await _app.CreatePlanAsync();
        var live = Live(_app.CreateWeek());
        await live.AppearedAsync();

        // The clock or time zone changed on the way: it is still the same day on her phone.
        _app.Clock.TimeZone = TimeZoneInfo.Utc;
        await live.WokeAsync();

        Assert.Equal(1, _loads);
        Assert.Equal(TimeSpan.FromHours(15) + DayWatch.Margin, _timer.Wait);
    }

    [Fact]
    public async Task In_the_background_no_wait_runs_and_coming_back_loads_again()
    {
        await _app.CreatePlanAsync();
        var live = Live(_app.CreateWeek());
        await live.AppearedAsync();

        live.Stopped();
        Assert.Null(_timer.Wait);

        _app.Clock.SetToday(TestData.Today.AddDays(1));
        await live.WokeAsync();
        Assert.Equal(1, _loads);

        await live.ResumedAsync();
        Assert.Equal(2, _loads);
        Assert.Equal(UntilMidnight, _timer.Wait);
    }

    [Fact]
    public async Task A_screen_that_is_not_showing_neither_loads_on_return_nor_waits()
    {
        await _app.CreatePlanAsync();
        var live = Live(_app.CreateWeek());
        await live.AppearedAsync();

        live.Disappeared();
        Assert.Null(_timer.Wait);

        _app.Clock.SetToday(TestData.Today.AddDays(1));
        live.Stopped();
        await live.ResumedAsync();
        await live.WokeAsync();

        Assert.Equal(1, _loads);
        Assert.Null(_timer.Wait);

        await live.AppearedAsync();
        Assert.Equal(2, _loads);
        Assert.Equal(UntilMidnight, _timer.Wait);
    }

    [Fact]
    public async Task A_load_that_failed_does_not_count_as_today_and_is_tried_again_when_the_wait_ends()
    {
        await _app.CreatePlanAsync();
        var week = _app.CreateWeek();
        var live = Live(week);
        _app.Repository.FailNextLoad = new IOException("storage not ready");

        await live.AppearedAsync();

        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", Assert.Single(_app.Notifier.Shown));
        Assert.Equal(UntilMidnight, _timer.Wait);

        _app.Clock.SetToday(TestData.Today.AddDays(1));
        await live.WokeAsync();

        Assert.Equal(2, _loads);
        Assert.Equal("Week 24 + 4", week.WeekLabel);
        Assert.Equal(UntilMidnight, _timer.Wait);
    }

    [Fact]
    public async Task A_screen_she_leaves_while_it_loads_sets_no_wait()
    {
        await _app.CreatePlanAsync();
        var week = _app.CreateWeek();
        var loader = new ScreenLoader(_app.CreateStartupFlow(), _app.Navigator, _app.Notifier, _app.Errors);
        LiveScreen? live = null;
        live = new LiveScreen(
            loader,
            _app.Clock,
            _timer,
            async () =>
            {
                live!.Disappeared();
                await week.LoadAsync();
            },
            "week.load");

        await live.AppearedAsync();

        Assert.Equal("Week 24 + 3", week.WeekLabel);
        Assert.Null(_timer.Wait);
    }

    private LiveScreen Live(WeekViewModel week)
    {
        var startup = _app.CreateStartupFlow();
        var loader = new ScreenLoader(startup, _app.Navigator, _app.Notifier, _app.Errors);
        return new LiveScreen(
            loader,
            _app.Clock,
            _timer,
            () =>
            {
                _loads++;
                return week.LoadAsync();
            },
            "week.load");
    }

    /// <summary>The app's dispatcher timer: the test ends the wait itself, by calling <see cref="LiveScreen.WokeAsync"/>.</summary>
    private sealed class FakeWakeTimer : IWakeTimer
    {
        /// <summary>The wait set, or null when none runs.</summary>
        public TimeSpan? Wait { get; private set; }

        public void WakeAfter(TimeSpan wait) => Wait = wait;

        public void Cancel() => Wait = null;
    }
}
