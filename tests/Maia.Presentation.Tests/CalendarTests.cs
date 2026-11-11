using Maia.Domain.Plans;
using Maia.Presentation.Calendar;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Calendar: her journey as a month, opened from the week pill on Today. Every day of the month shows, with a dot for
/// each kind of thing on it and the chosen day's entries under the grid; nothing on it changes her journey.
/// </summary>
public sealed class CalendarTests : IDisposable
{
    // Today is Wed 30 Sep 2026; the due date Sun 17 Jan 2027, so the pregnancy began on Sun 12 Apr 2026.
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task It_opens_on_this_month_with_today_chosen_and_every_day_shown()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();

        await vm.LoadAsync();

        Assert.True(vm.IsLoaded);
        Assert.Equal("September 2026", vm.MonthTitle);
        Assert.Equal(new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" }, vm.DayNames);
        var days = Days(vm);
        Assert.Equal(42, days.Count);

        // Six rows from the Sunday before the 1st: the last days of August and the first of October, faded.
        Assert.Equal(new DateOnly(2026, 8, 30), days[0].Date);
        Assert.Equal(CalendarLooks.OtherMonth, days[0].Look);
        Assert.Equal(CalendarLooks.Plain, Day(vm, 1).Look);
        Assert.Equal(CalendarLooks.TodaySelected, Day(vm, 30).Look);
        Assert.Equal(CalendarLooks.OtherMonth, days[^1].Look);
        Assert.Equal("30", Day(vm, 30).Number);

        // A day with nothing on it is a day like any other: its number, no dots.
        Assert.False(Day(vm, 15).HasBooked || Day(vm, 15).HasDone || Day(vm, 15).HasTask || Day(vm, 15).IsDueDate);
        Assert.Equal("Wed 30 Sep", vm.SelectedTitle);
        Assert.True(vm.HasNothing);
        Assert.Empty(vm.Entries);
    }

    [Fact]
    public async Task The_week_starts_on_the_day_it_starts_where_she_lives()
    {
        using var uk = new CultureScope("en-GB");
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();

        await vm.LoadAsync();

        Assert.Equal(new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, vm.DayNames);
        Assert.Equal(new DateOnly(2026, 8, 31), Days(vm)[0].Date);
    }

    [Fact]
    public async Task Each_kind_of_thing_on_a_day_has_its_dot_and_the_day_lists_them_in_order()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 5), new TimeOnly(10, 30)));
        await _app.Plans.BookCheckAsync(TestData.Anomaly, new Appointment(new DateOnly(2026, 9, 28), new TimeOnly(9, 0)));
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 5), new TimeOnly(9, 0));
        var ticked = await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 10, 5));
        await _app.Plans.SetTaskDoneAsync(ticked, true);
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        var vm = _app.CreateCalendar();

        await vm.LoadAsync();

        // 28 Sep: the scan, done (its time has passed).
        var scanDay = Day(vm, 28);
        Assert.True(scanDay.HasDone);
        Assert.False(scanDay.HasBooked || scanDay.HasTask);
        Assert.Equal("Mon 28 Sep, Done", scanDay.SpokenName);

        // 5 Oct, in the faded last row of September: the glucose test, a task and a ticked one.
        var fifth = Days(vm).Single(d => d.Date == new DateOnly(2026, 10, 5));
        Assert.True(fifth.HasBooked && fifth.HasTask && fifth.HasDone);
        Assert.Equal("Mon 5 Oct, Booked, Done, Task", fifth.SpokenName);

        fifth.SelectCommand.Execute(null);

        Assert.Equal("October 2026", vm.MonthTitle);
        Assert.Equal("Mon 5 Oct", vm.SelectedTitle);
        Assert.Equal(CalendarLooks.Selected, Day(vm, 5).Look);
        Assert.Equal(
            new[]
            {
                ("Glucose test", "10:30", "Booked", "Booked"),
                ("Send the form", "", "Done", "Done"),
                ("Call clinic", "09:00", "Task", "Task"),
            },
            vm.Entries.Select(e => (e.Title, e.TimeText, e.StatusKind, e.StatusText)));
        Assert.False(vm.HasNothing);
        Assert.True(vm.Entries[0].HasTime);
        Assert.False(vm.Entries[1].HasTime);
        Assert.True(vm.Entries[1].IsDone);
        Assert.False(vm.Entries[2].IsDone);

        // Today, not chosen, keeps its own look; a task for a week has no day here.
        vm.PreviousMonthCommand.Execute(null);
        Assert.DoesNotContain(Days(vm), d => d.Date.Month == 11 && d.HasTask);
    }

    [Fact]
    public async Task Today_not_chosen_reads_as_today_and_says_so()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();

        Day(vm, 29).SelectCommand.Execute(null);

        Assert.Equal(CalendarLooks.Today, Day(vm, 30).Look);
        Assert.Equal("Wed 30 Sep, today", Day(vm, 30).SpokenName);
        Assert.Equal(CalendarLooks.Selected, Day(vm, 29).Look);
    }

    [Fact]
    public async Task The_due_date_has_its_own_dot_and_stands_first_on_its_day()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Rest", TestData.DueDate, new TimeOnly(8, 0));
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();

        for (var i = 0; i < 4; i++)
        {
            vm.NextMonthCommand.Execute(null);
        }

        Assert.Equal("January 2027", vm.MonthTitle);
        var due = Day(vm, 17);
        Assert.True(due.IsDueDate);
        due.SelectCommand.Execute(null);
        Assert.Equal(new[] { ("Due date", "Milestone"), ("Rest", "Task") }, vm.Entries.Select(e => (e.Title, e.StatusKind)));
    }

    [Fact]
    public async Task Another_month_opens_on_its_first_day_or_on_today_in_this_month()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();

        vm.NextMonthCommand.Execute(null);

        Assert.Equal("October 2026", vm.MonthTitle);
        Assert.Equal("Thu 1 Oct", vm.SelectedTitle);
        Assert.Equal(CalendarLooks.Selected, Day(vm, 1).Look);

        vm.PreviousMonthCommand.Execute(null);

        Assert.Equal("September 2026", vm.MonthTitle);
        Assert.Equal(CalendarLooks.TodaySelected, Day(vm, 30).Look);
    }

    [Fact]
    public async Task It_goes_from_the_month_her_pregnancy_began_to_week_52()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();

        var back = 0;
        while (vm.PreviousMonthCommand.CanExecute(null))
        {
            vm.PreviousMonthCommand.Execute(null);
            back++;
        }

        Assert.Equal(("April 2026", 5), (vm.MonthTitle, back));
        Assert.False(vm.CanGoBack);
        Assert.True(vm.CanGoForward);

        // A day before the pregnancy, faded in April's first row, chooses nothing.
        var before = Days(vm)[0];
        Assert.Equal(new DateOnly(2026, 3, 29), before.Date);
        before.SelectCommand.Execute(null);
        Assert.Equal("April 2026", vm.MonthTitle);
        Assert.Equal("Wed 1 Apr", vm.SelectedTitle);

        while (vm.NextMonthCommand.CanExecute(null))
        {
            vm.NextMonthCommand.Execute(null);
        }

        // Week 52 begins on Sun 11 Apr 2027.
        Assert.Equal("April 2027", vm.MonthTitle);
        Assert.False(vm.CanGoForward);
    }

    [Fact]
    public async Task Something_of_hers_dated_further_takes_the_calendar_there()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Plan the first birthday", new DateOnly(2027, 6, 2));
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();

        while (vm.NextMonthCommand.CanExecute(null))
        {
            vm.NextMonthCommand.Execute(null);
        }

        Assert.Equal("June 2027", vm.MonthTitle);
    }

    [Fact]
    public async Task Coming_back_keeps_the_month_and_the_day_she_was_looking_at()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();
        vm.NextMonthCommand.Execute(null);
        Day(vm, 20).SelectCommand.Execute(null);

        await vm.LoadAsync();

        Assert.Equal("October 2026", vm.MonthTitle);
        Assert.Equal("Tue 20 Oct", vm.SelectedTitle);
    }

    [Fact]
    public async Task A_day_no_longer_in_the_calendar_after_a_new_due_date_gives_way_to_today()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();
        while (vm.NextMonthCommand.CanExecute(null))
        {
            vm.NextMonthCommand.Execute(null);
        }

        await _app.Plans.ChangeDueDateAsync(new DateOnly(2026, 12, 10));
        await vm.LoadAsync();

        Assert.Equal("September 2026", vm.MonthTitle);
        Assert.Equal("Wed 30 Sep", vm.SelectedTitle);
    }

    [Fact]
    public async Task A_day_before_the_calendar_after_a_new_due_date_gives_way_to_today()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();
        await vm.LoadAsync();
        while (vm.PreviousMonthCommand.CanExecute(null))
        {
            vm.PreviousMonthCommand.Execute(null);
        }

        await _app.Plans.ChangeDueDateAsync(new DateOnly(2027, 3, 20));
        await vm.LoadAsync();

        Assert.Equal("September 2026", vm.MonthTitle);
    }

    [Fact]
    public async Task Greek_reads_the_month_and_the_days_in_greek()
    {
        using var greek = new CultureScope("el-GR");
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();

        await vm.LoadAsync();

        Assert.Equal("Σεπτέμβριος 2026", vm.MonthTitle);
        Assert.Equal("Δευ", vm.DayNames[0]);
        Assert.Equal("Τετ 30 Σεπ, σήμερα", Day(vm, 30).SpokenName);
    }

    [Fact]
    public async Task Without_a_journey_it_goes_to_setup()
    {
        var vm = _app.CreateCalendar();

        await vm.LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.False(vm.IsLoaded);
    }

    [Fact]
    public async Task Back_goes_back_to_today()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateCalendar();

        await vm.BackCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "back" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task The_week_pill_on_today_opens_the_calendar()
    {
        await _app.CreatePlanAsync();
        var week = _app.CreateWeek();
        await week.LoadAsync();

        await week.OpenCalendarCommand.ExecuteAsync(null);

        Assert.Equal("Calendar, Week 24 + 3", week.WeekPillSpoken);
        Assert.Equal("calendar", _app.Navigator.Visited[^1]);
    }

    private static List<CalendarDay> Days(CalendarViewModel vm) => vm.Weeks.SelectMany(w => w.Days).ToList();

    /// <summary>The day of the month shown (not a faded one of the month before or after).</summary>
    private static CalendarDay Day(CalendarViewModel vm, int day) =>
        Days(vm).Single(d => d.Date.Day == day && d.Look != CalendarLooks.OtherMonth);
}
