using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Presentation.Care;
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Plan;

/// <summary>
/// Journey (design screen 05): every check and entry on a timeline by trimester, with a "You are here" marker, her
/// blood pressure readings small at the day and time she took them, and, while its "Tasks" switch is on, her tasks with
/// a date on their day, so it reads as the calendar of what is coming.
/// Tapping a check expands it in place (screen 06); only one is open at a time. Each refresh loads the plan once, and
/// a refresh asked for while one is running (coming back to the screen as a card reloads, say) runs
/// once more after it rather than alongside it.
/// </summary>
public sealed partial class PlanViewModel : ObservableObject
{
    /// <summary>The week of the due date (40): weeks with nothing in them show up to it, not after birth.</summary>
    private const int DueWeek = PregnancyDating.TermDays / GestationalAge.DaysPerWeek;

    private readonly PlanQueries _queries;
    private readonly IClock _clock;
    private readonly INavigator _navigator;
    private readonly PlanFocus _focus;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private readonly IJourneySettings _settings;
    private readonly CheckRowHost _host;
    private readonly Dictionary<string, PlanRow> _rows = [];
    private Task _loading = Task.CompletedTask;
    private bool _loadAgain;
    private bool _todayWanted;
    private PlanTimeline? _shown;
    private object? _coveredBy;

    /// <summary>
    /// The "Tasks" switch is kept in <paramref name="settings"/>; without them it is on for as long as the screen lives.
    /// </summary>
    public PlanViewModel(
        PlanQueries queries,
        PlanService plans,
        IClock clock,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors,
        PlanFocus focus,
        IJourneySettings? settings = null)
    {
        _queries = queries;
        _clock = clock;
        _navigator = navigator;
        _focus = focus;
        _notifier = notifier;
        _errors = errors;
        _settings = settings ?? new SessionJourneySettings();
        _host = new CheckRowHost(plans, clock, errors, notifier, ReloadAfterChangeAsync, Toggle, ToggleTaskAsync);
        ShowTasks = _settings.ShowTasks;
    }

    public ObservableCollection<PlanRow> Rows { get; } = [];

    /// <summary>
    /// The "Tasks" switch: her tasks with a date stand on their day among her checks. Remembered on this phone, and
    /// the timeline changes at once, without a reload.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowTasks { get; set; }

    /// <summary>
    /// Raised when a row should be brought to the top of the screen: the entry Add has just saved, or where today
    /// is as Journey opens.
    /// </summary>
    public event EventHandler<PlanRow>? ScrollRequested;

    /// <summary>
    /// Shows the plan as it is today. While a load is running, another call waits for it and one more load follows, so
    /// every caller sees the plan as it stands after its own change.
    /// </summary>
    public Task LoadAsync()
    {
        if (!_loading.IsCompleted)
        {
            _loadAgain = true;
            return _loading;
        }

        return _loading = LoadUntilCurrentAsync();
    }

    /// <summary>
    /// Journey is being opened (not come back to from a screen opened over it): it shows today, with "You are here"
    /// near the top, every time, so in her third trimester she never scrolls to find it. Rows already on screen are
    /// scrolled to at once, before the screen shows; a load under way scrolls again once it is done, in case today
    /// moved. An entry chosen on another screen (the one Add has just saved) wins: Journey opens on that instead.
    /// </summary>
    public void ShowToday()
    {
        if (_focus.IsPending)
        {
            return;
        }

        if (Rows.Count == 0 || !_loading.IsCompleted)
        {
            _todayWanted = true;
        }

        ScrollToToday();
    }

    /// <summary>
    /// Journey is left: <paramref name="coveringScreen"/> is the screen opened over it (Add, a result), or null when
    /// she goes to another tab.
    /// </summary>
    public void Leave(object? coveringScreen) => _coveredBy = coveringScreen;

    /// <summary>
    /// Journey shows again, coming from <paramref name="from"/>. Straight back from the screen opened over it, she keeps
    /// her place. Any other way in opens at today (<see cref="ShowToday"/>): from the bar, from Your week, or after she
    /// left that screen for another tab first (Add, then Week, then Journey).
    /// </summary>
    public void Arrive(object? from)
    {
        var back = _coveredBy is not null && ReferenceEquals(from, _coveredBy);
        _coveredBy = null;
        if (!back)
        {
            ShowToday();
        }
    }

    private async Task LoadUntilCurrentAsync()
    {
        do
        {
            _loadAgain = false;
            await LoadOnceAsync();
        }
        while (_loadAgain);
    }

    /// <summary>After a change on a card: the timeline shows it, and a reload that fails says so calmly instead of ending Maia.</summary>
    private async Task ReloadAfterChangeAsync()
    {
        if (await ScreenLoad.ReloadAsync(LoadAsync, "plan.load", _errors) is { } failed)
        {
            _notifier.ShowNotice(failed);
        }
    }

    private async Task LoadOnceAsync()
    {
        var timeline = await _queries.GetTimelineAsync();
        if (timeline is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        Show(timeline);

        if (_focus.Take() is { } focus && _rows.GetValueOrDefault("check:" + focus) is CheckRow target)
        {
            _todayWanted = false;
            ScrollRequested?.Invoke(this, target);
        }
        else if (_todayWanted)
        {
            _todayWanted = false;
            ScrollToToday();
        }
    }

    /// <summary>
    /// Lays the timeline out like a calendar: checks and her entries each at their day (see <see cref="DayOf"/>), by time
    /// and then plan order within a day, "You are here" where now is (after everything up to today, but before today's
    /// bookings still to come), and her tasks at their day (or week, or first in their trimester) after the checks there
    /// (today, one whose time has come stands with the bookings that have begun, before "You are here"). Rows are grouped
    /// by trimester, by the day they stand at, and each week under its own heading (<see cref="WithWeeks"/>).
    /// </summary>
    private void Show(PlanTimeline timeline)
    {
        _shown = timeline;
        var today = _clock.Today;
        var now = TimeZoneInfo.ConvertTime(_clock.UtcNow, _clock.TimeZone);
        var minuteNow = (now.Hour * 60) + now.Minute;
        var age = timeline.Age;
        var dating = timeline.Dating;

        var views = timeline.Sections.SelectMany(s => s.Checks).ToList();
        var everyCheck = views.Select(v => v.Check).ToList();
        var language = Text.Language;
        var placed = new List<Placed>(views.Count + timeline.TimelineTasks.Count + timeline.Readings.Count + 1);
        foreach (var view in views)
        {
            var row = Row(() => new CheckRow(_host, view.Check.Id), "check:" + view.Check.Id);

            row.Show(new CheckRowData(view, DisplayFormat.CheckName(view.Check, everyCheck, language), dating, today));
            var day = DayOf(view.Check, dating);
            var minute = MinuteOf(view.Check);

            // A booking later today is still to come: it follows "You are here", so Journey opens with it in view.
            var rank = day == age.TotalDays && minute > minuteNow ? Rank.LaterToday : Rank.Check;
            placed.Add(new Placed(day, rank, minute, placed.Count, row, view, HasWeek: view.Check.TrimesterOnly is null));
        }

        var here = Row(() => new HereRow(), "here");
        here.Text = Text.Get("Plan_Here");
        placed.Add(new Placed(age.TotalDays, Rank.Here, 0, placed.Count, here, null, HasWeek: true));

        foreach (var reading in timeline.Readings)
        {
            var row = Row(() => new ReadingRow(_host, reading.Id), "reading:" + reading.Id);
            row.Show(reading);

            // A reading is something that happened: it stands with the bookings that have begun, by its time, so one
            // taken earlier today is before "You are here".
            var day = dating.On(reading.Date).TotalDays;
            placed.Add(new Placed(day, Rank.Check, Minutes(reading.Time), placed.Count, row, null, HasWeek: true));
        }

        if (ShowTasks)
        {
            foreach (var task in timeline.TimelineTasks)
            {
                var row = Row(() => new TaskRow(_host, task.Id), "task:" + task.Id);
                row.Show(task);
                var (day, minute) = PlaceOf(task, dating);

                // A task earlier today whose time has come stands before "You are here", among today's bookings that
                // have begun, by its time; one later today, or with no time, is still to do and follows it.
                var rank = day == age.TotalDays && task.DueTime is not null && minute <= minuteNow ? Rank.Check : Rank.Task;
                placed.Add(new Placed(day, rank, minute, placed.Count, row, null, HasWeek: task.Trimester is null));
            }
        }

        // On its day, a row with only a trimester comes first: it is first in its trimester, before the week's heading.
        var ordered = placed.OrderBy(p => p.Day).ThenBy(p => p.HasWeek).ThenBy(p => p.Rank).ThenBy(p => p.Minute).ThenBy(p => p.Order);
        var withWeeks = WithWeeks(ordered, dating);
        var wanted = new List<PlanRow>(withWeeks.Count + 4);

        // Sections follow the day, so each trimester shows once, in order, even for a task with no check near it.
        foreach (var section in withWeeks.GroupBy(p => PlanQueries.SectionKeyFor(p.Day / GestationalAge.DaysPerWeek)))
        {
            var header = Row(() => new SectionRow(section.Key, DisplayFormat.Section(section.Key)), "section:" + section.Key);
            wanted.Add(header);

            var items = section.Select(p => p.Row).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                items[i].LineAbove = i > 0;
                items[i].LineBelow = i < items.Count - 1;
            }

            wanted.AddRange(items);
        }

        // Update in place, so the timeline keeps its scroll position and open card after a change.
        CollectionSync.Apply(Rows, wanted, (a, b) => a.Key == b.Key);
        foreach (var key in _rows.Keys.Except(wanted.Select(r => r.Key)).ToList())
        {
            _rows.Remove(key);
        }
    }

    /// <summary>
    /// Where an entry stands, in days of pregnancy: a booked one (done or still to come) at the day of its booking, as a
    /// calendar would show it (so a visit booked for next week is after "You are here", not before it); any other at the
    /// start of its weeks, or of its trimester.
    /// </summary>
    private static int DayOf(PlannedCheck check, PregnancyDating dating) =>
        check.Appointment is { } booked ? dating.On(booked.Date).TotalDays : check.Window.FromWeek * GestationalAge.DaysPerWeek;

    /// <summary>
    /// Within a day, booked entries follow their time; anything without one comes first, and one with only a trimester
    /// before everything else in it.
    /// </summary>
    private static int MinuteOf(PlannedCheck check) =>
        check.Appointment is { } booked ? Minutes(booked.Time) : check.TrimesterOnly is not null ? -2 : -1;

    private static int Minutes(TimeOnly? time) => time is { } at ? (at.Hour * 60) + at.Minute : -1;

    /// <summary>
    /// Where a task stands, in days of pregnancy, and its time that day: on its day, at the start of its week, or at the
    /// start of its trimester before anything else there, as an entry with only a trimester does. Every task on the
    /// timeline has one of the three (<see cref="PlanTimeline.TimelineTasks"/>).
    /// </summary>
    private static (int Day, int Minute) PlaceOf(PlanTask task, PregnancyDating dating) =>
        task.DueDate is { } due ? (dating.On(due).TotalDays, Minutes(task.DueTime))
        : task.Week is { } week ? (week * GestationalAge.DaysPerWeek, -1)
        : (WeekWindow.OfTrimester(task.Trimester!.Value).FromWeek * GestationalAge.DaysPerWeek, -2);

    /// <summary>
    /// Puts each pregnancy week's heading (<see cref="WeekRow"/>) before its first row, in timeline order, so the weeks
    /// read down Journey like a calendar's. Every week between the first and the last on the timeline has one, and a week
    /// up to the due date with nothing in it says so; after birth only the weeks with something in them show. A row with
    /// only a trimester has no week: it stands before the heading of the week it falls in.
    /// </summary>
    private List<Placed> WithWeeks(IEnumerable<Placed> ordered, PregnancyDating dating)
    {
        var rows = new List<Placed>();
        int? last = null;
        foreach (var placed in ordered)
        {
            // The weeks passed over since the last heading had nothing in them (a row with only a trimester has no week).
            var week = placed.Day / GestationalAge.DaysPerWeek;
            for (var empty = (last ?? week) + 1; empty < week && empty <= DueWeek; empty++)
            {
                rows.Add(WeekHeading(empty, isEmpty: true, dating));
                last = empty;
            }

            if (placed.HasWeek && week != last)
            {
                rows.Add(WeekHeading(week, isEmpty: false, dating));
                last = week;
            }

            rows.Add(placed);
        }

        return rows;
    }

    private Placed WeekHeading(int week, bool isEmpty, PregnancyDating dating)
    {
        var row = Row(() => new WeekRow(week), "week:" + week.ToString(CultureInfo.InvariantCulture));
        row.Show(dating, isEmpty);
        return new Placed(week * GestationalAge.DaysPerWeek, Rank.Check, -1, 0, row, null, HasWeek: true);
    }

    /// <summary>
    /// Brings today near the top: the row just above "You are here" (its trimester's heading, or the check before it)
    /// starts the screen, so she sees where she is and what led up to it. An open card above it could fill the screen,
    /// so then "You are here" itself starts it.
    /// </summary>
    private void ScrollToToday()
    {
        var index = _rows.GetValueOrDefault("here") is { } here ? Rows.IndexOf(here) : -1;
        if (index < 0)
        {
            return;
        }

        // "You are here" always follows its trimester's heading, so there is a row above it.
        var target = Rows[index - 1] is not CheckRow { IsExpanded: true } ? Rows[index - 1] : Rows[index];
        ScrollRequested?.Invoke(this, target);
    }

    /// <summary>
    /// The "Tasks" switch turned on or off: remembered on this phone, and the timeline redrawn at once, without a reload.
    /// </summary>
    partial void OnShowTasksChanged(bool value)
    {
        _settings.ShowTasks = value;
        if (_shown is { } shown)
        {
            Show(shown);
        }
    }

    /// <summary>
    /// Ticks a task off, or back on, as Your week does. The row stays where it is, struck through once done. The
    /// timeline then reloads in place (she keeps her place), so hiding her tasks and showing them again draws the task
    /// as it now is rather than as it was first loaded.
    /// </summary>
    private async Task ToggleTaskAsync(TaskRow task)
    {
        var done = !task.IsDone;
        var error = await PlanChange.RunAsync(() => _host.Plans.SetTaskDoneAsync(task.Id, done), "plan.toggle_task", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        task.IsDone = done;
        await ReloadAfterChangeAsync();
    }

    /// <summary>Opens a card and closes any other; tapping the open card closes it.</summary>
    private void Toggle(CheckRow row) => Toggle(row, !row.IsExpanded);

    private void Toggle(CheckRow row, bool open)
    {
        var others = Rows.OfType<CheckRow>().Where(r => r != row && r.IsExpanded).ToList();
        foreach (var other in others)
        {
            other.SetExpanded(false);
        }

        row.SetExpanded(open);
    }

    private T Row<T>(Func<T> create, string key)
        where T : PlanRow
    {
        if (_rows.GetValueOrDefault(key) is T existing)
        {
            return existing;
        }

        var row = create();
        _rows[key] = row;
        return row;
    }

    /// <summary>
    /// On the same day, checks come first, then her tasks. Today, "You are here" stands at the time it is now: after the
    /// bookings and timed tasks that have begun (and checks without a time), before those later today and her other
    /// tasks.
    /// </summary>
    private enum Rank
    {
        Check,
        Here,
        LaterToday,
        Task,
    }

    /// <summary>
    /// A row with the day it stands at, its time that day (-1 for none), its place in plan or task order, and whether it
    /// has a week (one with only a trimester has none).
    /// </summary>
    private sealed record Placed(int Day, Rank Rank, int Minute, int Order, PlanRow Row, CheckView? View, bool HasWeek);
}
