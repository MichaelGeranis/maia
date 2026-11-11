using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
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

/// <summary>A row on the plan timeline. Rows keep their instance across reloads (matched by <see cref="Key"/>).</summary>
public abstract partial class PlanRow : ObservableObject
{
    public abstract string Key { get; }

    /// <summary>Whether the timeline line runs above and below this row's node (not past its section's ends).</summary>
    [ObservableProperty]
    public partial bool LineAbove { get; internal set; }

    [ObservableProperty]
    public partial bool LineBelow { get; internal set; }
}

/// <summary>A trimester heading: "2ND TRIMESTER · 2 of 4 done".</summary>
public sealed partial class SectionRow : PlanRow
{
    public SectionRow(string sectionKey, string title)
    {
        SectionKey = sectionKey;
        Title = title;
    }

    public string SectionKey { get; }

    public override string Key => "section:" + SectionKey;

    public string Title { get; }
}

/// <summary>
/// A pregnancy week's heading across the timeline: "Week 25", a hairline, then its days ("11–17 Oct"), so the weeks read
/// down Journey as sections. Every week from the first on the timeline to the due date shows, and one with nothing in it
/// says so ("Nothing this week").
/// </summary>
public sealed partial class WeekRow : PlanRow
{
    public WeekRow(int week) => Week = week;

    public int Week { get; }

    public override string Key => "week:" + Week.ToString(CultureInfo.InvariantCulture);

    /// <summary>"Week 25".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>"11–17 Oct", or "27 Sep – 3 Oct" across two months.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial string Dates { get; private set; } = string.Empty;

    /// <summary>Nothing on the timeline in this week: "Nothing this week" under the heading.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>What a screen reader says for the heading: "Week 25, 11–17 Oct".</summary>
    public string SpokenName => Text.Format("Plan_WeekSpoken", Title, Dates);

    internal void Show(PregnancyDating dating, bool isEmpty)
    {
        var start = dating.StartOfWeek(Week);
        Title = Text.Format("Plan_Week", Week);
        Dates = DisplayFormat.DayRange(start, start.AddDays(GestationalAge.DaysPerWeek - 1));
        IsEmpty = isEmpty;
    }
}

/// <summary>The "You are here" marker between past and future checks (the week shows only on Your week).</summary>
public sealed partial class HereRow : PlanRow
{
    public override string Key => "here";

    [ObservableProperty]
    public partial string Text { get; internal set; } = string.Empty;
}

/// <summary>What check and task rows need from the screen that owns them.</summary>
internal sealed record CheckRowHost(
    PlanService Plans,
    IClock Clock,
    IErrorReporter Errors,
    INotifier Notifier,
    Func<Task> ReloadAsync,
    Action<CheckRow> Toggle,
    Func<TaskRow, Task>? ToggleTaskAsync = null);

/// <summary>
/// One of her tasks, on its day (or at its week, or first in its trimester) on the timeline while the "Tasks" switch is
/// on: "Call the clinic" with "Thu 1 Oct · 09:00" (or "Week 26", "3rd trimester") under it and a tick circle on the
/// line. Ticking it here does what ticking it on Today does; a ticked task stays where it is, struck through, as a
/// record (tap again to undo).
/// </summary>
public sealed partial class TaskRow : PlanRow
{
    private readonly CheckRowHost _host;

    internal TaskRow(CheckRowHost host, TaskId id)
    {
        _host = host;
        Id = id;
    }

    public TaskId Id { get; }

    public override string Key => "task:" + Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>"Thu 1 Oct · 09:00", just the day for a task with no time, or "Week 26" / "3rd trimester" for one with no day.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial string When { get; private set; } = string.Empty;

    /// <summary>What a screen reader says for the row, which is one tick box: "Call the clinic, Thu 1 Oct · 09:00".</summary>
    public string SpokenName => Text.Format("Plan_TaskSpoken", Title, When);

    [ObservableProperty]
    public partial bool IsDone { get; internal set; }

    internal void Show(PlanTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        Title = task.Title;
        When = Week.TaskItem.WhenText(task, _host.Clock.Today) ?? string.Empty;
        IsDone = task.IsDone;
    }

    /// <summary>Ticks it off, or back on: the same change as the circle on Your week.</summary>
    [RelayCommand]
    private Task ToggleAsync() => _host.ToggleTaskAsync?.Invoke(this) ?? Task.CompletedTask;
}

/// <summary>
/// A blood pressure reading she logged on New, small on the line at the day and time she took it, as a week's number
/// is: "118/76 mmHg" with "Sat 10 Oct · 08:30" beside it, and no card. It is never changed: sliding it left or right
/// shows "Delete", which deletes it for good. Maia only shows the numbers she wrote; it never reads anything into them.
/// </summary>
public sealed partial class ReadingRow : PlanRow
{
    private readonly CheckRowHost _host;
    private int _systolic;
    private int _diastolic;

    internal ReadingRow(CheckRowHost host, ReadingId id)
    {
        _host = host;
        Id = id;
    }

    public ReadingId Id { get; }

    public override string Key => "reading:" + Id;

    /// <summary>"118/76 mmHg".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial string Value { get; private set; } = string.Empty;

    /// <summary>"Sat 10 Oct · 08:30".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial string When { get; private set; } = string.Empty;

    /// <summary>What a screen reader says for the row: "Blood pressure 118 over 76, Sat 10 Oct · 08:30".</summary>
    public string SpokenName => Text.Format("Reading_Spoken", _systolic, _diastolic, When);

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    internal void Show(BloodPressureReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        _systolic = reading.Systolic;
        _diastolic = reading.Diastolic;
        Value = ValueOf(reading);
        When = DisplayFormat.Date(reading.Date, _host.Clock.Today) + " · " + DisplayFormat.Time(reading.Time);
    }

    /// <summary>"118/76 mmHg", as her monitor shows it.</summary>
    internal static string ValueOf(BloodPressureReading reading) => Text.Format("Reading_Value", reading.Systolic, reading.Diastolic);

    /// <summary>Deletes it for good (she slid it aside and tapped "Delete"), and says so.</summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        string? error;
        try
        {
            error = await PlanChange.RunAsync(() => _host.Plans.RemoveBloodPressureAsync(Id), "plan.delete_reading", _host.Errors);
        }
        finally
        {
            IsBusy = false;
        }

        if (error is not null)
        {
            _host.Notifier.ShowNotice(error);
            return;
        }

        _host.Notifier.Show(Text.Format("Toast_ReadingDeleted", Value));
        await _host.ReloadAsync();
    }
}

/// <summary>What a check row shows for one load of the plan.</summary>
/// <param name="View">The check, with its status today.</param>
/// <param name="Name">Its name where it stands alone (toasts, pickers).</param>
/// <param name="Dating">The pregnancy's dating, to turn weeks into dates.</param>
/// <param name="Today">Today.</param>
internal sealed record CheckRowData(
    CheckView View,
    string Name,
    PregnancyDating Dating,
    DateOnly Today);

/// <summary>
/// An entry on the timeline. Tapping it expands it in place (design screen 06): its note, then what can be done with
/// it. Not booked: "Book" (a day and a time) and "Delete". Booked or done: "Edit" (its name, its note and when; a done
/// one may also say only the trimester or the week) and "Delete". Delete asks once more first.
/// </summary>
public sealed partial class CheckRow : PlanRow
{
    private readonly CheckRowHost _host;
    private string _name = string.Empty;
    private CheckRowData? _data;

    internal CheckRow(CheckRowHost host, CheckId id)
    {
        _host = host;
        Id = id;
    }

    public CheckId Id { get; }

    public override string Key => "check:" + Id;

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>
    /// What an entry she added is, above its name: "Doctor visit", "Exam or test", "Hospital visit" or "Something else". Empty
    /// for a check from her country's plan.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKind))]
    public partial string KindText { get; private set; } = string.Empty;

    public bool HasKind => KindText.Length > 0;

    /// <summary>Its trimester, week and booking: "2nd trimester · Week 24 · Tue 6 Oct, 10:30".</summary>
    [ObservableProperty]
    public partial string Caption { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Culture-free status name the views map to colours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardKind), nameof(IsDone))]
    public partial string StatusKind { get; private set; } = string.Empty;

    /// <summary>What the check is for, from the template, or the note she gave it; empty when it has none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string Note { get; private set; } = string.Empty;

    public bool HasNote => Note.Length > 0;

    /// <summary>The due date is a marker, not an exam: it never expands.</summary>
    [ObservableProperty]
    public partial bool IsMilestone { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardKind))]
    public partial bool IsExpanded { get; internal set; }

    /// <summary>What the card looks like: its status, or "Expanded" while open (white, with a green border).</summary>
    public string CardKind => IsExpanded ? "Expanded" : StatusKind;

    /// <summary>Done or probably done: the timeline node shows a tick.</summary>
    public bool IsDone => StatusKind is nameof(CheckStatus.Done) or nameof(CheckStatus.ProbablyDone);

    /// <summary>Not booked: "Book" and "Delete". Otherwise "Edit" and "Delete".</summary>
    [ObservableProperty]
    public partial bool CanBook { get; private set; }

    /// <summary>What the open card shows: its two buttons, the booking or editing fields, or "Delete …?".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsActions), nameof(IsBooking), nameof(IsEditing), nameof(IsChanging), nameof(IsConfirmingDelete), nameof(SaveLabel))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial CheckPanel Panel { get; private set; }

    public bool ShowsActions => Panel == CheckPanel.Actions;

    /// <summary>"Book": a day and a time.</summary>
    public bool IsBooking => Panel == CheckPanel.Booking;

    /// <summary>"Edit": its name, and when.</summary>
    public bool IsEditing => Panel == CheckPanel.Editing;

    /// <summary>Booking or editing: the fields, with save and cancel.</summary>
    public bool IsChanging => IsBooking || IsEditing;

    public bool IsConfirmingDelete => Panel == CheckPanel.ConfirmDelete;

    /// <summary>"Book" while booking, "Save" while editing.</summary>
    public string SaveLabel => Text.Get(IsBooking ? "Check_Book" : "Common_Save");

    /// <summary>"Delete Glucose test from your journey?"</summary>
    public string DeleteQuestion => Text.Format("Check_DeleteConfirm", _name);

    /// <summary>When it is, as she fills it in.</summary>
    public TimingDraft Timing { get; } = new();

    /// <summary>Its name, while editing.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string EditName { get; set; } = string.Empty;

    /// <summary>Its note, while editing: empty for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditNoteLengthHint))]
    public partial string EditNote { get; set; } = string.Empty;

    /// <summary>How much room the note has left, once she nears the limit; null until then.</summary>
    public string? EditNoteLengthHint => TextLimit.Hint(EditNote, PlannedCheck.MaxNoteLength);

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    internal void Show(CheckRowData data)
    {
        _data = data;
        var (view, name, dating, today) = data;
        var check = view.Check;
        _name = name;

        Title = view.Title;
        KindText = check.Kind is { } kind ? DisplayFormat.Kind(kind) : string.Empty;
        Caption = DisplayFormat.PlanCaption(view, dating, today);
        StatusText = DisplayFormat.Status(view.Status);
        StatusKind = DisplayFormat.StatusKind(view.Status);
        Note = check.Description?.In(Text.Language) ?? string.Empty;
        IsMilestone = check.IsMilestone;
        CanBook = view.Status == CheckStatus.NotBooked;
        OnPropertyChanged(nameof(DeleteQuestion));

        // What she has open stays while the card is open; a closed card opens on its buttons.
        if (!IsExpanded)
        {
            Panel = CheckPanel.Actions;
        }
    }

    /// <summary>Opens the card on its buttons; closes it again when <paramref name="open"/> is false.</summary>
    /// <remarks>Only ever called to change it: Journey opens a closed card, or closes an open one.</remarks>
    internal void SetExpanded(bool open)
    {
        ErrorMessage = null;
        Panel = CheckPanel.Actions;
        IsExpanded = open;
    }

    [RelayCommand]
    private void Toggle()
    {
        if (!IsMilestone)
        {
            _host.Toggle(this);
        }
    }

    /// <summary>"Book": a day and a time, starting at the next whole hour. A time that has passed makes it done.</summary>
    [RelayCommand]
    private void StartBook()
    {
        if (_data is not { } data)
        {
            return;
        }

        StartTiming(data, offersPeriod: false, TimingMode.DateTime, TimingDraft.NextHour(_host.Clock.Now));
        ErrorMessage = null;
        Panel = CheckPanel.Booking;
    }

    /// <summary>
    /// "Edit": its name, its note and when. A booking keeps a day and a time; a done one may also say only the trimester
    /// or the week. It starts at what it has now.
    /// </summary>
    [RelayCommand]
    private void StartEdit()
    {
        if (_data is not { } data)
        {
            return;
        }

        var check = data.View.Check;
        var booked = check.Appointment;
        var at = booked is { } a ? a.Date.ToDateTime(a.Time ?? TimeOnly.MinValue) : _host.Clock.Now;
        var mode = booked is not null ? TimingMode.DateTime : check.TrimesterOnly is not null ? TimingMode.Trimester : TimingMode.Week;
        StartTiming(data, offersPeriod: data.View.Status != CheckStatus.Booked, mode, at, check.TrimesterOnly, check.Week);
        EditName = Title;
        EditNote = Note;
        ErrorMessage = null;
        Panel = CheckPanel.Editing;
    }

    /// <summary>Back to the card's buttons, with nothing saved.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        ErrorMessage = null;
        Panel = CheckPanel.Actions;
    }

    [RelayCommand]
    private void AskDelete()
    {
        ErrorMessage = null;
        Panel = CheckPanel.ConfirmDelete;
    }

    [RelayCommand]
    private void KeepIt() => Panel = CheckPanel.Actions;

    /// <summary>Deletes it from her journey for good.</summary>
    [RelayCommand]
    private Task DeleteAsync() =>
        RunAsync(() => _host.Plans.DeleteEntryAsync(Id), "plan.delete", Text.Format("Toast_EntryDeleted", _name));

    private bool CanSave() => !IsEditing || !string.IsNullOrWhiteSpace(EditName);

    /// <summary>Saves the booking, or what she edited, and folds the card.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync()
    {
        if (_data is not { } data)
        {
            return Task.CompletedTask;
        }

        var booked = data.View.Check.Appointment;
        var when = Timing.ToWhen(done: true, booked?.Location, booked?.With);
        if (when is null)
        {
            ErrorMessage = Text.Get("Add_ChooseWhen");
            return Task.CompletedTask;
        }

        var name = IsEditing ? EditName.Trim() : Title;
        if (name.Length == 0 || name.Length > Add.AddEntryViewModel.MaxNameLength)
        {
            ErrorMessage = name.Length == 0 ? Text.Get("Add_NameMissing") : Text.Format("Error_EntryTooLong", Add.AddEntryViewModel.MaxNameLength);
            return Task.CompletedTask;
        }

        // Editing tells her what changed: a new booking, or just that it is updated.
        var moved = IsBooking || when.Appointment != booked;
        var toast = moved && when.Appointment is { } appointment ? BookedToast(name, appointment) : Text.Format("Toast_EntryUpdated", name);
        return IsBooking
            ? RunAsync(() => _host.Plans.BookCheckAsync(Id, when.Appointment!), "plan.book", toast)
            : RunAsync(() => _host.Plans.EditEntryAsync(Id, name, when, EditNote), "plan.edit", toast);
    }

    /// <summary>"… booked for Tue 6 Oct at 10:30.", or "… done on Tue 29 Sep." for a time that has passed.</summary>
    private string BookedToast(string name, Appointment appointment)
    {
        var today = _host.Clock.Today;
        var now = TimeOnly.FromDateTime(_host.Clock.Now);
        return PlannedCheck.HasPassed(appointment, today, now)
            ? Text.Format("Toast_BookedDone", name, DisplayFormat.Date(appointment.Date, today))
            : Text.Format("Toast_Booked", name, DisplayFormat.Date(appointment.Date, today), DisplayFormat.Time(appointment.Time!.Value));
    }

    /// <summary>The timing fields, from the first day of the pregnancy to the last week Maia shows.</summary>
    private void StartTiming(CheckRowData data, bool offersPeriod, TimingMode mode, DateTime at, Trimester? trimester = null, int? week = null)
    {
        var (_, _, dating, today) = data;
        var latest = dating.StartOfWeek(WeekWindow.MaxWeek).AddDays(GestationalAge.DaysPerWeek - 1);
        latest = latest < today ? today : latest;
        Timing.Start(
            offersDateTime: true,
            offersPeriod,
            mode,
            at,
            dating.On(today),
            dating.LastMenstrualPeriod.ToDateTime(TimeOnly.MinValue),
            latest.ToDateTime(TimeOnly.MinValue),
            trimester,
            week);
    }

    private async Task RunAsync(Func<Task> change, string operation, string toast)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            ErrorMessage = await PlanChange.RunAsync(change, operation, _host.Errors);
        }
        finally
        {
            IsBusy = false;
        }

        if (ErrorMessage is not null)
        {
            return;
        }

        IsExpanded = false;
        Panel = CheckPanel.Actions;
        _host.Notifier.Show(toast);
        await _host.ReloadAsync();
    }
}

/// <summary>What an open entry card shows.</summary>
public enum CheckPanel
{
    /// <summary>Its two buttons: "Book" or "Edit", and "Delete".</summary>
    Actions,

    Booking,

    Editing,

    /// <summary>"Delete … from your journey?", with "Delete" and "Keep it".</summary>
    ConfirmDelete,
}
