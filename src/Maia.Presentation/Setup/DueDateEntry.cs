using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Setup;

/// <summary>
/// Her due date, typed in or worked out from the first day of her last period, with what it means today ("You're 24
/// weeks and 3 days today. Due Sun 17 Jan."). Only dates that fit a current pregnancy are valid, each picker offers
/// only those dates (a pregnancy between 0 and 44 weeks today), and switching between them carries the date across.
/// Setup step 1 and the due date in Settings share it, so both take the same dates.
/// </summary>
public sealed partial class DueDateEntry : ObservableObject
{
    private readonly IClock _clock;

    public DueDateEntry(IClock clock, DateOnly initialDue)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;

        var today = clock.Today;
        var earliestLastPeriod = today.AddDays(-PregnancyDating.MaxSupported.TotalDays);
        LastPeriodMinimumDate = earliestLastPeriod.ToDateTime(TimeOnly.MinValue);
        LastPeriodMaximumDate = today.ToDateTime(TimeOnly.MinValue);
        DueMinimumDate = earliestLastPeriod.AddDays(PregnancyDating.TermDays).ToDateTime(TimeOnly.MinValue);
        DueMaximumDate = today.AddDays(PregnancyDating.TermDays).ToDateTime(TimeOnly.MinValue);
        Reset(initialDue);
    }

    /// <summary>The due date picker's range: from 28 days ago (44 weeks today) to 40 weeks from today.</summary>
    public DateTime DueMinimumDate { get; }

    public DateTime DueMaximumDate { get; }

    /// <summary>The last-period picker's range: from 44 weeks ago up to today.</summary>
    public DateTime LastPeriodMinimumDate { get; }

    public DateTime LastPeriodMaximumDate { get; }

    [ObservableProperty]
    public partial DateTime DueDate { get; set; }

    [ObservableProperty]
    public partial DateTime LastPeriodDate { get; set; }

    [ObservableProperty]
    public partial bool UseLastPeriod { get; set; }

    [ObservableProperty]
    public partial string Summary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    /// <summary>The due date she means, whichever way it was entered.</summary>
    public DateOnly EffectiveDueDate => UseLastPeriod
        ? PregnancyDating.FromLastMenstrualPeriod(DateOnly.FromDateTime(LastPeriodDate)).DueDate
        : DateOnly.FromDateTime(DueDate);

    public bool UseDueDate => !UseLastPeriod;

    /// <summary>"When is your baby due?" or "First day of your last period".</summary>
    public string DateLabel => Text.Get(UseLastPeriod ? "Setup_LastPeriodLabel" : "Setup_DueDateLabel");

    /// <summary>"Not sure? Use your last period instead", or back: "I know my due date".</summary>
    public string DateModeLink => Text.Get(UseLastPeriod ? "Setup_UseDueDate" : "Setup_UseLastPeriod");

    /// <summary>Whether Maia can make a plan for this due date today: a pregnancy between 0 and 44 weeks.</summary>
    public static bool Accepts(DateOnly dueDate, DateOnly today)
    {
        var daysSinceLastPeriod = today.DayNumber - PregnancyDating.FromDueDate(dueDate).LastMenstrualPeriod.DayNumber;
        return daysSinceLastPeriod >= 0 && daysSinceLastPeriod <= PregnancyDating.MaxSupported.TotalDays;
    }

    /// <summary>Starts again from <paramref name="due"/>, entered as a due date, within the dates the picker offers.</summary>
    public void Reset(DateOnly due)
    {
        UseLastPeriod = false;
        DueDate = Clamp(due.ToDateTime(TimeOnly.MinValue), DueMinimumDate, DueMaximumDate);
        LastPeriodDate = DueDate.AddDays(-PregnancyDating.TermDays);
        Recalculate();
    }

    partial void OnDueDateChanged(DateTime value) => Recalculate();

    partial void OnLastPeriodDateChanged(DateTime value) => Recalculate();

    partial void OnUseLastPeriodChanged(bool value)
    {
        // The date she chose comes along, so the summary keeps the same due date.
        if (value)
        {
            LastPeriodDate = Clamp(DueDate.AddDays(-PregnancyDating.TermDays), LastPeriodMinimumDate, LastPeriodMaximumDate);
        }
        else
        {
            DueDate = Clamp(LastPeriodDate.AddDays(PregnancyDating.TermDays), DueMinimumDate, DueMaximumDate);
        }

        OnPropertyChanged(nameof(DateLabel));
        OnPropertyChanged(nameof(DateModeLink));
        OnPropertyChanged(nameof(UseDueDate));
        Recalculate();
    }

    [RelayCommand]
    private void ToggleDateMode() => UseLastPeriod = !UseLastPeriod;

    private static DateTime Clamp(DateTime value, DateTime min, DateTime max) =>
        value < min ? min : value > max ? max : value;

    private void Recalculate()
    {
        var due = EffectiveDueDate;
        var today = _clock.Today;
        IsValid = Accepts(due, today);
        Summary = IsValid
            ? Text.Format("Setup_Summary", DisplayFormat.WeeksAndDays(PregnancyDating.FromDueDate(due).On(today)), DisplayFormat.Date(due))
            : Text.Get("Setup_OutOfRange");
        OnPropertyChanged(nameof(EffectiveDueDate));
    }
}
