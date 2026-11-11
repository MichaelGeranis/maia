using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Plans;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Calendar;

/// <summary>
/// Calendar (pushed from the week pill on Today): her journey as a month, to look at, never to change. Every day of the
/// month shows, with or without something on it; a dot under a day's number for each kind of thing that day (booked,
/// done, a task, the due date), and the chosen day's entries listed under the grid. It opens on this month with today
/// chosen; ‹ › or a swipe move a month, from the month her pregnancy began to week 52 (further when something of hers
/// is dated further). Entries with no day (a check not booked yet, one done in a week) are on Journey only.
/// </summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    /// <summary>Rows in the grid: enough for any month, and always the same, so the grid never changes height.</summary>
    public const int WeekRows = 6;

    /// <summary>The last week of pregnancy an entry can stand in (Journey's "After birth" ends there).</summary>
    private const int LastWeek = 52;

    private readonly PlanQueries _queries;
    private readonly IClock _clock;
    private readonly INavigator _navigator;
    private Dictionary<DateOnly, List<CalendarEntry>> _byDay = [];
    private DateOnly _today;
    private DateOnly _month;
    private DateOnly _firstMonth;
    private DateOnly _lastMonth;
    private DateOnly _selected;

    public CalendarViewModel(PlanQueries queries, IClock clock, INavigator navigator)
    {
        _queries = queries;
        _clock = clock;
        _navigator = navigator;
        Weeks = Enumerable.Range(0, WeekRows)
            .Select(_ => new CalendarWeek(Enumerable.Range(0, 7).Select(_ => new CalendarDay(Select)).ToList()))
            .ToList();
    }

    /// <summary>The grid: <see cref="WeekRows"/> rows of seven days.</summary>
    public IReadOnlyList<CalendarWeek> Weeks { get; }

    /// <summary>The weekday names over the grid, from the first day of the week: "Mon" … "Sun", «Δευ» … «Κυρ».</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> DayNames { get; private set; } = [];

    /// <summary>"October 2026", «Οκτώβριος 2026».</summary>
    [ObservableProperty]
    public partial string MonthTitle { get; private set; } = string.Empty;

    /// <summary>The chosen day, over its list: "Tue 6 Oct".</summary>
    [ObservableProperty]
    public partial string SelectedTitle { get; private set; } = string.Empty;

    /// <summary>What is on the chosen day: the due date, then entries by time, then tasks by time.</summary>
    public ObservableCollection<CalendarEntry> Entries { get; } = [];

    /// <summary>"Nothing on this day." shows instead of a list.</summary>
    [ObservableProperty]
    public partial bool HasNothing { get; private set; }

    /// <summary>Whether the first load has finished; until then the grid is not shown.</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousMonthCommand))]
    public partial bool CanGoBack { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextMonthCommand))]
    public partial bool CanGoForward { get; private set; }

    /// <summary>
    /// Reads her journey. The first time it opens on this month with today chosen; after that (back from the
    /// background, a new day) it keeps the month and the day she is looking at.
    /// </summary>
    public async Task LoadAsync()
    {
        var timeline = await _queries.GetTimelineAsync();
        if (timeline is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        _today = _clock.Today;
        _byDay = ByDay(timeline);
        var last = timeline.Dating.StartOfWeek(LastWeek);
        var days = _byDay.Keys.Append(_today).Append(timeline.Dating.LastMenstrualPeriod).Append(last).ToList();
        _firstMonth = MonthOf(days.Min());
        _lastMonth = MonthOf(days.Max());

        if (!IsLoaded || MonthOf(_selected) < _firstMonth || MonthOf(_selected) > _lastMonth)
        {
            _selected = _today;
            _month = MonthOf(_today);
        }

        Show();
        IsLoaded = true;
    }

    /// <summary>
    /// What stands on each day: a booked entry, or one done on a day, at its day; the due date; and her tasks with a
    /// date. Each day's list is in the order it reads.
    /// </summary>
    internal static Dictionary<DateOnly, List<CalendarEntry>> ByDay(PlanTimeline timeline)
    {
        var placed = new List<(DateOnly Day, int Rank, CalendarEntry Entry)>();
        foreach (var view in timeline.Sections.SelectMany(s => s.Checks))
        {
            if (view.Status == CheckStatus.Milestone)
            {
                placed.Add((timeline.Dating.DueDate, 0, new CalendarEntry(view.Title, null, CalendarEntry.DueDate, DisplayFormat.Status(view.Status))));
            }
            else if (view.Check.Appointment is { } booked)
            {
                var kind = view.Status == CheckStatus.Booked ? CalendarEntry.Booked : CalendarEntry.Done;
                placed.Add((booked.Date, 1, new CalendarEntry(view.Title, booked.Time, kind, DisplayFormat.Status(view.Status))));
            }
        }

        foreach (var task in timeline.TimelineTasks)
        {
            if (task.DueDate is { } due)
            {
                var entry = task.IsDone
                    ? new CalendarEntry(task.Title, task.DueTime, CalendarEntry.Done, DisplayFormat.Status(CheckStatus.Done))
                    : new CalendarEntry(task.Title, task.DueTime, CalendarEntry.Task, Text.Get("Week_TaskLabel"));
                placed.Add((due, 2, entry));
            }
        }

        return placed
            .GroupBy(p => p.Day)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.Rank).ThenBy(p => p.Entry.Time ?? TimeOnly.MinValue).Select(p => p.Entry).ToList());
    }

    /// <summary>‹: the month before, with today chosen if it is in it, otherwise its first day.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousMonth() => GoTo(_month.AddMonths(-1));

    /// <summary>›: the month after, with today chosen if it is in it, otherwise its first day.</summary>
    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void NextMonth() => GoTo(_month.AddMonths(1));

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();

    private void GoTo(DateOnly month)
    {
        _month = month;
        _selected = MonthOf(_today) == month ? _today : month;
        Show();
    }

    /// <summary>A day tapped: chosen. One of the month before or after brings its month, when the calendar goes there.</summary>
    private void Select(CalendarDay day)
    {
        var month = MonthOf(day.Date);
        if (month < _firstMonth || month > _lastMonth)
        {
            return;
        }

        _selected = day.Date;
        _month = month;
        Show();
    }

    private void Show()
    {
        var culture = Text.Culture;
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        DayNames = Enumerable.Range(0, 7)
            .Select(i => culture.DateTimeFormat.AbbreviatedDayNames[((int)firstDay + i) % 7])
            .ToList();
        var title = _month.ToString("MMMM yyyy", culture);
        MonthTitle = char.ToUpper(title[0], culture) + title[1..];
        CanGoBack = _month > _firstMonth;
        CanGoForward = _month < _lastMonth;

        var start = _month.AddDays(-(((int)_month.DayOfWeek - (int)firstDay + 7) % 7));
        for (var row = 0; row < WeekRows; row++)
        {
            for (var column = 0; column < 7; column++)
            {
                var date = start.AddDays((row * 7) + column);
                var entries = EntriesOn(date);
                Weeks[row].Days[column].Show(date, LookOf(date), entries, Spoken(date, entries));
            }
        }

        SelectedTitle = DisplayFormat.Date(_selected, _today);
        Entries.Clear();
        foreach (var entry in EntriesOn(_selected))
        {
            Entries.Add(entry);
        }

        HasNothing = Entries.Count == 0;
    }

    private List<CalendarEntry> EntriesOn(DateOnly date) => _byDay.GetValueOrDefault(date) ?? [];

    private string LookOf(DateOnly date) =>
        MonthOf(date) != _month ? CalendarLooks.OtherMonth
        : date == _selected && date == _today ? CalendarLooks.TodaySelected
        : date == _selected ? CalendarLooks.Selected
        : date == _today ? CalendarLooks.Today
        : CalendarLooks.Plain;

    /// <summary>"Tue 6 Oct, today, Booked, Task": the day, and a word for each kind of thing on it.</summary>
    private string Spoken(DateOnly date, IReadOnlyList<CalendarEntry> entries)
    {
        var parts = new List<string>(6) { DisplayFormat.Date(date, _today) };
        if (date == _today)
        {
            parts.Add(Text.Get("Calendar_TodaySpoken"));
        }

        parts.AddRange(entries.Select(e => e.StatusText).Distinct(StringComparer.Ordinal));
        return string.Join(", ", parts);
    }

    private static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);
}
