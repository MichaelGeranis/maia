using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Maia.Presentation.Calendar;

/// <summary>
/// One day in Calendar's month grid. The grid keeps the same 42 days for as long as it shows, and each takes a new date
/// as she moves between months, so the phone never builds the grid twice.
/// </summary>
public sealed partial class CalendarDay : ObservableObject
{
    private readonly Action<CalendarDay> _select;

    internal CalendarDay(Action<CalendarDay> select)
    {
        _select = select;
    }

    /// <summary>The day this cell shows.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>The day's number: "6".</summary>
    [ObservableProperty]
    public partial string Number { get; private set; } = string.Empty;

    /// <summary>
    /// How the day's number is drawn: <see cref="CalendarLooks.Plain"/>, <see cref="CalendarLooks.Today"/>,
    /// <see cref="CalendarLooks.Selected"/>, <see cref="CalendarLooks.TodaySelected"/>, or
    /// <see cref="CalendarLooks.OtherMonth"/> for a day of the month before or after.
    /// </summary>
    [ObservableProperty]
    public partial string Look { get; private set; } = CalendarLooks.Plain;

    /// <summary>Something booked that day (a Sea dot).</summary>
    [ObservableProperty]
    public partial bool HasBooked { get; private set; }

    /// <summary>Something done that day (a Sage dot).</summary>
    [ObservableProperty]
    public partial bool HasDone { get; private set; }

    /// <summary>A task still to do that day (a Muted dot).</summary>
    [ObservableProperty]
    public partial bool HasTask { get; private set; }

    /// <summary>The due date (a Sun dot).</summary>
    [ObservableProperty]
    public partial bool IsDueDate { get; private set; }

    /// <summary>What a screen reader says: "Tue 6 Oct, today, Booked, Task".</summary>
    [ObservableProperty]
    public partial string SpokenName { get; private set; } = string.Empty;

    internal void Show(DateOnly date, string look, IReadOnlyList<CalendarEntry> entries, string spokenName)
    {
        Date = date;
        Number = date.Day.ToString(Localization.Text.Culture);
        Look = look;
        HasBooked = entries.Any(e => e.StatusKind == CalendarEntry.Booked);
        HasDone = entries.Any(e => e.StatusKind == CalendarEntry.Done);
        HasTask = entries.Any(e => e.StatusKind == CalendarEntry.Task);
        IsDueDate = entries.Any(e => e.StatusKind == CalendarEntry.DueDate);
        SpokenName = spokenName;
    }

    /// <summary>Tapped: its entries show under the grid (a day of another month brings that month).</summary>
    [RelayCommand]
    private void Select() => _select(this);
}

/// <summary>The ways a day's number is drawn in Calendar's grid (<see cref="CalendarDay.Look"/>).</summary>
public static class CalendarLooks
{
    public const string Plain = nameof(Plain);

    /// <summary>Today, not chosen: its number in Sea.</summary>
    public const string Today = nameof(Today);

    /// <summary>The day she chose: a Pine circle.</summary>
    public const string Selected = nameof(Selected);

    /// <summary>Today, chosen: a Sea circle.</summary>
    public const string TodaySelected = nameof(TodaySelected);

    /// <summary>A day of the month before or after, faded.</summary>
    public const string OtherMonth = nameof(OtherMonth);
}

/// <summary>One row of Calendar's grid: seven days, starting on the first day of the week where she lives.</summary>
public sealed class CalendarWeek
{
    internal CalendarWeek(IReadOnlyList<CalendarDay> days)
    {
        Days = days;
    }

    public IReadOnlyList<CalendarDay> Days { get; }
}

/// <summary>
/// Something on the chosen day, listed under the grid as it reads: the name, its time ("10:30"; none for a day without
/// one) and its status chip. Nothing here can be changed: that is on Journey and Today.
/// </summary>
public sealed class CalendarEntry
{
    /// <summary>Status kinds, as the status colours read them (docs/design/README.md).</summary>
    public const string Booked = "Booked";

    public const string Done = "Done";

    public const string DueDate = "Milestone";

    public const string Task = "Task";

    internal CalendarEntry(string title, TimeOnly? time, string statusKind, string statusText)
    {
        Title = title;
        Time = time;
        TimeText = time is { } at ? Formatting.DisplayFormat.Time(at) : string.Empty;
        StatusKind = statusKind;
        StatusText = statusText;
    }

    public string Title { get; }

    internal TimeOnly? Time { get; }

    /// <summary>"10:30", or empty for a day without a time.</summary>
    public string TimeText { get; }

    public bool HasTime => TimeText.Length > 0;

    /// <summary><see cref="Booked"/>, <see cref="Done"/>, <see cref="DueDate"/> or <see cref="Task"/>.</summary>
    public string StatusKind { get; }

    /// <summary>The chip's word: "Booked", "Done", "Due date", "Task".</summary>
    public string StatusText { get; }

    /// <summary>A done entry's name reads in Muted, as on Journey.</summary>
    public bool IsDone => StatusKind == Done;
}
