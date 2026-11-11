using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Plan;

/// <summary>How she says when an entry is.</summary>
public enum TimingMode
{
    /// <summary>A day and a time: a booking (done once it has passed).</summary>
    DateTime,

    /// <summary>Only the trimester.</summary>
    Trimester,

    /// <summary>A pregnancy week.</summary>
    Week,
}

/// <summary>
/// When an entry is, as she fills it in on Add or in Edit on Journey: a day and a time, a trimester, or a pregnancy
/// week. Which of them she may choose depends on what she is doing (<see cref="Start"/>); with more than one, choice pills
/// say which, and the fields for it follow.
/// </summary>
public sealed partial class TimingDraft : ObservableObject
{
    /// <summary>The first and last pregnancy weeks she can choose.</summary>
    public const int FirstWeek = 1;

    public const int LastWeek = 42;

    private Trimester _currentTrimester = Domain.Pregnancy.Trimester.First;

    /// <summary>Which she has chosen; null until she chooses (or the only choice there is).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDateTime), nameof(IsTrimester), nameof(IsWeek))]
    public partial TimingMode? Mode { get; private set; }

    public bool IsDateTime => Mode == TimingMode.DateTime;

    public bool IsTrimester => Mode == TimingMode.Trimester;

    public bool IsWeek => Mode == TimingMode.Week;

    /// <summary>Whether "Date and time" is one of the choices.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowModes))]
    public partial bool OffersDateTime { get; private set; }

    /// <summary>Whether "Trimester" and "Week" are among the choices.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowModes))]
    public partial bool OffersPeriod { get; private set; }

    /// <summary>The choice pills show only when there is more than one thing to choose.</summary>
    public bool ShowModes => OffersPeriod;

    [ObservableProperty]
    public partial DateTime Date { get; set; }

    [ObservableProperty]
    public partial TimeSpan Time { get; set; }

    /// <summary>The first day of the pregnancy.</summary>
    [ObservableProperty]
    public partial DateTime MinimumDate { get; private set; }

    [ObservableProperty]
    public partial DateTime MaximumDate { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirstTrimester), nameof(IsSecondTrimester), nameof(IsThirdTrimester), nameof(IsAfterBirth))]
    public partial Trimester? Trimester { get; private set; }

    public bool IsFirstTrimester => Trimester == Domain.Pregnancy.Trimester.First;

    public bool IsSecondTrimester => Trimester == Domain.Pregnancy.Trimester.Second;

    public bool IsThirdTrimester => Trimester == Domain.Pregnancy.Trimester.Third;

    /// <summary>After birth, the fourth choice beside the three trimesters.</summary>
    public bool IsAfterBirth => Trimester == Domain.Pregnancy.Trimester.AfterBirth;

    /// <summary>The pregnancy week, from <see cref="FirstWeek"/> to <see cref="LastWeek"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekText))]
    [NotifyCanExecuteChangedFor(nameof(EarlierWeekCommand), nameof(LaterWeekCommand))]
    public partial int Week { get; private set; } = FirstWeek;

    /// <summary>"Week 24".</summary>
    public string WeekText => Text.Format("Plan_Week", Week);

    /// <summary>
    /// Where a new booking starts: the next whole hour after <paramref name="now"/>, so saving it straight away books a
    /// time still to come (she moves it back for one that has passed).
    /// </summary>
    public static DateTime NextHour(DateTime now) => now.Date.AddHours(now.Hour + 1);

    /// <summary>The day and time chosen, as one.</summary>
    public DateTime At => Date.Date + Time;

    /// <summary>
    /// Starts afresh: what she may choose, and what is chosen to begin with. The trimester and the week start at
    /// <paramref name="age"/>'s unless given.
    /// </summary>
    /// <param name="offersDateTime">Whether a day and time is a choice.</param>
    /// <param name="offersPeriod">Whether a trimester or a week is a choice.</param>
    /// <param name="mode">What is chosen to begin with (null: nothing yet).</param>
    /// <param name="at">The day and time to begin with.</param>
    /// <param name="age">How far the pregnancy is today.</param>
    /// <param name="firstDay">The first day of the pregnancy: the earliest day.</param>
    /// <param name="lastDay">The latest day.</param>
    /// <param name="trimester">The trimester to begin with.</param>
    /// <param name="week">The week to begin with.</param>
    internal void Start(
        bool offersDateTime,
        bool offersPeriod,
        TimingMode? mode,
        DateTime at,
        GestationalAge age,
        DateTime firstDay,
        DateTime lastDay,
        Trimester? trimester = null,
        int? week = null)
    {
        OffersDateTime = offersDateTime;
        OffersPeriod = offersPeriod;
        MinimumDate = firstDay;
        MaximumDate = lastDay < firstDay ? firstDay : lastDay;
        var day = at.Date < MinimumDate ? MinimumDate : at.Date > MaximumDate ? MaximumDate : at.Date;
        Date = day;
        Time = at.TimeOfDay;
        _currentTrimester = age.Trimester;
        Trimester = trimester ?? (mode == TimingMode.Trimester ? age.Trimester : null);
        Week = Math.Clamp(week ?? age.Weeks, FirstWeek, LastWeek);
        Mode = mode;
    }

    /// <summary>
    /// Starts afresh for a task without a date: a trimester or a week, the one given chosen to begin with, or nothing
    /// until she chooses. The trimester and the week start at <paramref name="age"/>'s when she does.
    /// </summary>
    /// <param name="age">How far the pregnancy is today.</param>
    /// <param name="today">Today, where the (hidden) day fields stand.</param>
    /// <param name="trimester">The trimester the task is for now, if any.</param>
    /// <param name="week">The week the task is for now, if any.</param>
    internal void StartPeriod(GestationalAge age, DateTime today, Trimester? trimester = null, int? week = null) =>
        Start(
            offersDateTime: false,
            offersPeriod: true,
            trimester is not null ? TimingMode.Trimester : week is not null ? TimingMode.Week : null,
            today,
            age,
            today,
            today,
            trimester,
            week);

    /// <summary>The trimester or the week chosen, for a task without a date; neither while nothing is chosen.</summary>
    internal (Trimester? Trimester, int? Week) ToPeriod() => Mode switch
    {
        TimingMode.Trimester => (Trimester, null),
        TimingMode.Week => (null, Week),
        _ => (null, null),
    };

    /// <summary>
    /// When the entry is, as chosen: booked for the day and time, or (not booked, or <paramref name="done"/>) in the
    /// trimester or the week; null while nothing is chosen.
    /// </summary>
    internal EntryWhen? ToWhen(bool done, string? location = null, CareContactId? with = null) => Mode switch
    {
        TimingMode.DateTime => EntryWhen.Booked(new Appointment(DateOnly.FromDateTime(Date), TimeOnly.FromTimeSpan(Time), location, with)),
        // Choosing "Trimester" (here or in Start) always chooses one too.
        TimingMode.Trimester => done ? EntryWhen.DoneIn(Trimester!.Value) : EntryWhen.NotBookedIn(Trimester!.Value),
        TimingMode.Week => done ? EntryWhen.DoneInWeek(Week) : EntryWhen.NotBookedInWeek(Week),
        _ => null,
    };

    /// <summary>"Date and time", "Trimester" or "Week". The trimester starts at today's.</summary>
    [RelayCommand]
    private void ChooseMode(string mode)
    {
        if (Enum.TryParse<TimingMode>(mode, out var chosen) && Enum.IsDefined(chosen)
            && (chosen == TimingMode.DateTime ? OffersDateTime : OffersPeriod))
        {
            if (chosen == TimingMode.Trimester && Trimester is null)
            {
                Trimester = _currentTrimester;
            }

            Mode = chosen;
        }
    }

    /// <summary>"1st", "2nd" or "3rd" trimester, or "After birth".</summary>
    [RelayCommand]
    private void ChooseTrimester(string trimester)
    {
        if (Enum.TryParse<Trimester>(trimester, out var chosen) && Enum.IsDefined(chosen))
        {
            Trimester = chosen;
        }
    }

    private bool CanGoEarlier() => Week > FirstWeek;

    private bool CanGoLater() => Week < LastWeek;

    [RelayCommand(CanExecute = nameof(CanGoEarlier))]
    private void EarlierWeek() => Week = Math.Max(FirstWeek, Week - 1);

    [RelayCommand(CanExecute = nameof(CanGoLater))]
    private void LaterWeek() => Week = Math.Min(LastWeek, Week + 1);
}
