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
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Plan;

namespace Maia.Presentation.Add;

/// <summary>When an entry she adds happens, as the three choice pills on the Add screen say it.</summary>
public enum AddWhen
{
    /// <summary>"Booked": a day, past or to come, at a time. It gets its reminders as any booking does.</summary>
    Booked,

    /// <summary>"Already done": a day up to today.</summary>
    Done,

    /// <summary>"Not booked yet": it waits on her journey for a date.</summary>
    NotBookedYet,
}

/// <summary>
/// "Add to your journey" (the Add tab): what it is (a doctor visit, an exam or test, a hospital visit, something else,
/// a task or her blood pressure), its name, and when (booked, already done, or not booked yet). Nothing changes until
/// "Save", which adds one entry, with her note if she wrote one. A task is added as on Today (its words, and a date and
/// time if she turns them on, or else a trimester or a week). Her blood pressure is its two numbers and the day and time
/// she took it, starting at now; it shows on Journey at that time. Either way she stays on Add, which starts
/// afresh with a toast that says what was added, so she can add another. Coming back to the screen keeps what she chose
/// and typed.
/// </summary>
public sealed partial class AddEntryViewModel : ObservableObject
{
    /// <summary>
    /// Longest name she can give an entry: room for "Visit with " and the longest name a contact can have, well inside
    /// what the plan keeps (<see cref="PlannedCheck.MaxTitleLength"/>).
    /// </summary>
    public const int MaxNameLength = 150;

    /// <summary>Exams and tests her doctor often asks for that are not in her country's plan, as text keys.</summary>
    public static readonly IReadOnlyList<string> ExtraKeys =
        ["Extra_Ultrasound", "Extra_Iron", "Extra_Urine", "Extra_Doppler", "Extra_Thyroid", "Extra_BloodPressure"];

    /// <summary>The longest task, as on Your week.</summary>
    public const int MaxTaskLength = Week.WeekViewModel.MaxTaskLength;

    /// <summary>The longest note an entry can have.</summary>
    public const int MaxNoteLength = PlannedCheck.MaxNoteLength;

    private readonly PlanQueries _queries;
    private readonly PlanService _plans;
    private readonly IClock _clock;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;

    /// <summary>Her OB-GYN (the first she linked), whom a doctor visit is with while it keeps the name Maia gave it.</summary>
    private CareContact? _doctor;

    /// <summary>The pregnancy's dating, from the last read of her plan; null before the first.</summary>
    private PregnancyDating? _dating;

    /// <summary>"Visit with Dr. Eleni Papadaki" in every language: a doctor visit she names so is with her doctor.</summary>
    private LocalizedText? _visitName;

    /// <summary>The name a suggestion she tapped filled in. Choosing another kind clears it; a name she typed herself stays.</summary>
    private string _filledName = string.Empty;

    /// <summary>
    /// The chip she tapped, while the name is still its own: two checks can share a name (her check-ups), and the one she
    /// tapped is the one that comes back.
    /// </summary>
    private NameSuggestion? _chosen;

    public AddEntryViewModel(
        PlanQueries queries,
        PlanService plans,
        IClock clock,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors)
    {
        _queries = queries;
        _plans = plans;
        _clock = clock;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;

        // Dates and a time the pickers can show as they are, so binding them before the plan is read writes nothing back.
        var today = clock.Today;
        EarliestTaskDate = Week.TaskItem.EarliestDate(today).ToDateTime(TimeOnly.MinValue);
        TaskDate = today.ToDateTime(TimeOnly.MinValue);
        TaskTime = clock.Now.TimeOfDay;
        ReadingDate = TaskDate;
        LatestReadingDate = TaskDate;
        EarliestReadingDate = TaskDate;
        ReadingTime = TaskTime;

        // "Save" looks ready once she has said when, too: for an entry, or for a task without a date.
        Timing.PropertyChanged += OnTimingChanged;
        TaskPeriod.PropertyChanged += OnTimingChanged;
    }

    /// <summary>
    /// For an exam or test: the exams doctors often ask for that her plan does not have yet. Tapping one names the entry
    /// after it.
    /// </summary>
    public ObservableCollection<NameSuggestion> Suggestions { get; } = [];

    /// <summary>What she is adding; null until she chooses, and while she adds a task.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKind), nameof(IsDoctorVisit), nameof(IsExam), nameof(IsHospitalVisit), nameof(IsOther), nameof(ShowSuggestions), nameof(IsComplete))]
    public partial EntryKind? Kind { get; private set; }

    /// <summary>Whether she is adding a task rather than an entry: the task's words and date show then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    public partial bool IsTask { get; private set; }

    /// <summary>Whether she is logging her blood pressure: its two numbers and when she took it show then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    public partial bool IsBloodPressure { get; private set; }

    /// <summary>The upper number, as she types it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    public partial string Systolic { get; set; } = string.Empty;

    /// <summary>The lower number, as she types it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    public partial string Diastolic { get; set; } = string.Empty;

    /// <summary>The day she took it: from the first day of the pregnancy up to today.</summary>
    [ObservableProperty]
    public partial DateTime ReadingDate { get; set; }

    [ObservableProperty]
    public partial TimeSpan ReadingTime { get; set; }

    [ObservableProperty]
    public partial DateTime EarliestReadingDate { get; private set; }

    [ObservableProperty]
    public partial DateTime LatestReadingDate { get; private set; }

    /// <summary>The task's words.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    [NotifyPropertyChangedFor(nameof(TaskLengthHint))]
    public partial string TaskTitle { get; set; } = string.Empty;

    /// <summary>How much room the task's words have left, once she nears the limit; null until then.</summary>
    public string? TaskLengthHint => TextLimit.Hint(TaskTitle, MaxTaskLength);

    /// <summary>
    /// Whether the task gets a date and time; off until she turns it on, as on Today. Without, it is for a trimester or a
    /// week (<see cref="TaskPeriod"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    public partial bool TaskHasDate { get; set; }

    /// <summary>Without a date: the trimester or the week the task is for, none chosen until she chooses.</summary>
    public TimingDraft TaskPeriod { get; } = new();

    [ObservableProperty]
    public partial DateTime TaskDate { get; set; }

    [ObservableProperty]
    public partial TimeSpan TaskTime { get; set; }

    /// <summary>Whether she has chosen what it is: its name and when show then.</summary>
    public bool HasKind => Kind is not null;

    public bool IsDoctorVisit => Kind == EntryKind.DoctorVisit;

    public bool IsExam => Kind == EntryKind.Exam;

    public bool IsHospitalVisit => Kind == EntryKind.HospitalVisit;

    public bool IsOther => Kind == EntryKind.Other;

    /// <summary>Whether the suggestion chips show: for an exam or test, when there are some.</summary>
    public bool ShowSuggestions => IsExam && Suggestions.Count > 0;

    /// <summary>Its name: what she types, or a suggestion she taps. It starts empty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComplete))]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Her note for it, shown when its card is opened on Journey; empty for none (it is optional).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteLengthHint))]
    public partial string Note { get; set; } = string.Empty;

    /// <summary>How much room the note has left, once she nears the limit; null until then.</summary>
    public string? NoteLengthHint => TextLimit.Hint(Note, MaxNoteLength);

    /// <summary>When it happens; null until she chooses.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBooked), nameof(IsDone), nameof(IsNotBookedYet), nameof(IsComplete))]
    public partial AddWhen? When { get; private set; }

    public bool IsBooked => When == AddWhen.Booked;

    public bool IsDone => When == AddWhen.Done;

    public bool IsNotBookedYet => When == AddWhen.NotBookedYet;

    /// <summary>
    /// Whether what, name and when are all there (for a task, its words and when): "Save" looks ready only then (tapping
    /// it sooner says what is missing).
    /// </summary>
    public bool IsComplete => IsBloodPressure
        ? !string.IsNullOrWhiteSpace(Systolic) && !string.IsNullOrWhiteSpace(Diastolic)
        : IsTask
        ? !string.IsNullOrWhiteSpace(TaskTitle) && (TaskHasDate || TaskPeriod.Mode is not null)
        : Kind is not null && When is not null && Timing.Mode is not null && !string.IsNullOrWhiteSpace(Name);

    /// <summary>
    /// When it is: booked, a day and a time (from the first day of the pregnancy, past or to come); already done, a day
    /// and a time that has passed, a trimester or a week; not booked yet, a trimester or a week.
    /// </summary>
    public TimingDraft Timing { get; } = new();

    /// <summary>The earliest day a task can be set for, as on Your week: the past is fine.</summary>
    [ObservableProperty]
    public partial DateTime EarliestTaskDate { get; private set; }

    /// <summary>A calm line when something is missing, the plan can't be read, or saving failed.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsReady { get; private set; }

    /// <summary>
    /// Fills the screen from one load of the plan. Coming back to it keeps what she chose and typed. If the plan can't
    /// be read just now, it says so calmly and nothing can be saved until it can.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            await ShowAsync();
        }
#pragma warning disable CA1031 // Shown calmly; the reporter gets the type, never anything from the plan.
        catch (Exception ex)
        {
            _errors.Report(ex, "add.load");
            ErrorMessage = Text.Get("Common_Error");
            IsReady = false;
        }
#pragma warning restore CA1031
    }

    private async Task ShowAsync()
    {
        var snapshot = await _queries.GetSnapshotAsync();
        if (snapshot is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        var timeline = snapshot.Timeline;
        var obGyns = snapshot.CareTeam(CareRole.ObGyn);
        _doctor = obGyns.Count > 0 ? obGyns[0] : null;
        _visitName = EntryTitles.DoctorVisit(_doctor);

        // What she chose stays; the pickers start at the pregnancy as it is now when she next chooses "When".
        var today = _clock.Today.ToDateTime(TimeOnly.MinValue);
        _dating = timeline.Dating;
        EarliestTaskDate = Week.TaskItem.EarliestDate(_clock.Today).ToDateTime(TimeOnly.MinValue);
        if (TaskDate < EarliestTaskDate)
        {
            TaskDate = today;
        }

        // Her blood pressure: a day from the first of the pregnancy (never after today) up to today.
        LatestReadingDate = today;
        EarliestReadingDate = _dating.LastMenstrualPeriod.ToDateTime(TimeOnly.MinValue);

        OfferSuggestions(snapshot);

        ErrorMessage = null;
        IsReady = true;
    }

    /// <summary>"Doctor visit", "Exam or test", "Hospital visit", "Something else", "Task" or "Blood pressure".</summary>
    [RelayCommand]
    private void ChooseKind(string kind)
    {
        if (kind == BloodPressureChoice)
        {
            if (!IsBloodPressure)
            {
                // When she took it starts at now: she logs it as she reads her monitor.
                var now = _clock.Now;
                ReadingDate = now.Date;
                ReadingTime = new TimeSpan(now.Hour, now.Minute, 0);
                Kind = null;
                IsTask = false;
                IsBloodPressure = true;
                ErrorMessage = null;
            }

            return;
        }

        if (kind == TaskChoice)
        {
            if (!IsTask)
            {
                // As "Add a task" on Today: a date and time start at now, once she turns them on; without, no trimester or
                // week is chosen until she chooses.
                var now = _clock.Now;
                TaskDate = now.Date;
                TaskTime = now.TimeOfDay;
                TaskPeriod.StartPeriod(Dating.On(_clock.Today), now.Date);
                Kind = null;
                IsBloodPressure = false;
                IsTask = true;
                ErrorMessage = null;
            }

            return;
        }

        if (!Enum.TryParse<EntryKind>(kind, out var chosen) || !Enum.IsDefined(chosen) || chosen == Kind)
        {
            return;
        }

        IsTask = false;
        IsBloodPressure = false;
        Kind = chosen;

        // A chip she tapped for the kind before lets go; a name she typed stays.
        if (IsFilled(Name))
        {
            Fill(string.Empty);
        }

        ErrorMessage = null;
        MarkSuggestion();
    }

    /// <summary>
    /// "Booked" (a day and a time, starting at the next whole hour), "Already done" (a day and a time, a trimester or a week) or "Not
    /// booked yet" (a trimester or a week).
    /// </summary>
    [RelayCommand]
    private void ChooseWhen(string when)
    {
        if (!Enum.TryParse<AddWhen>(when, out var chosen) || !Enum.IsDefined(chosen))
        {
            return;
        }

        if (chosen != When)
        {
            var today = _clock.Today;
            var dating = Dating;
            var latest = dating.StartOfWeek(WeekWindow.MaxWeek).AddDays(GestationalAge.DaysPerWeek - 1);
            Timing.Start(
                offersDateTime: chosen != AddWhen.NotBookedYet,
                offersPeriod: chosen != AddWhen.Booked,
                chosen == AddWhen.Booked ? TimingMode.DateTime : null,
                chosen == AddWhen.Booked ? TimingDraft.NextHour(_clock.Now) : _clock.Now,
                dating.On(today),
                dating.LastMenstrualPeriod.ToDateTime(TimeOnly.MinValue),
                (latest < today ? today : latest).ToDateTime(TimeOnly.MinValue));
        }

        When = chosen;
        ErrorMessage = null;
    }

    /// <summary>
    /// Adds the entry (or task) in one save and stays on Add, which starts afresh for the next one. If it fails,
    /// everything she chose stays.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsReady))]
    private async Task SaveAsync()
    {
        if (IsBloodPressure)
        {
            await SaveReadingAsync();
            return;
        }

        if (IsTask)
        {
            await SaveTaskAsync();
            return;
        }

        ErrorMessage = Problem();
        if (ErrorMessage is not null)
        {
            return;
        }

        // Problem() has made sure she chose what it is and when, and how she says when (a trimester chosen is never
        // without one), so the timing reads as an entry's.
        var kind = Kind!.Value;
        var when = When!.Value;

        var name = Name.Trim();
        var suggestion = Matching(name);
        var named = NamesFor(kind, name, suggestion);

        // A visit that keeps the name Maia gave it after her doctor is with her doctor.
        var with = kind == EntryKind.DoctorVisit && named is not null && named.Equals(_visitName) ? _doctor?.Id : null;
        var entryWhen = Timing.ToWhen(done: when == AddWhen.Done, with: with)!;

        IsReady = false;
        ErrorMessage = await PlanChange.RunAsync(
            () => named is not null ? _plans.AddEntryAsync(named, kind, entryWhen, Note) : _plans.AddEntryAsync(name, kind, entryWhen, Note),
            "add.save",
            _errors);
        if (ErrorMessage is not null)
        {
            IsReady = true;
            return;
        }

        Reset();
        var shown = suggestion?.Title ?? name;
        var today = _clock.Today;
        _notifier.Show(entryWhen switch
        {
            { Appointment: { } booked } when PlannedCheck.HasPassed(booked, today, TimeOnly.FromDateTime(_clock.Now)) =>
                Text.Format("Toast_EntryDone", shown, DisplayFormat.Date(booked.Date, today)),
            { Appointment: { } booked } => Text.Format("Toast_EntryBooked", shown, DisplayFormat.Date(booked.Date, today), DisplayFormat.Time(booked.Time!.Value)),
            { IsDone: true } => Text.Format("Toast_EntryDoneNoDate", shown),
            _ => Text.Format("Toast_EntryWaiting", shown),
        });
    }

    /// <summary>The "Task" pill's parameter: a task is not a kind of entry.</summary>
    public const string TaskChoice = "Task";

    /// <summary>The "Blood pressure" pill's parameter: a reading is not a kind of entry either.</summary>
    public const string BloodPressureChoice = "BloodPressure";

    /// <summary>The most digits either number can have.</summary>
    public const int MaxReadingDigits = 3;

    /// <summary>
    /// Logs her blood pressure as her monitor shows it, at the day and time she took it (a time to come is refused), and
    /// says so. Numbers that can't be a monitor's (mistyped) are refused calmly; nothing is read into ones that can.
    /// </summary>
    private async Task SaveReadingAsync()
    {
        var hasUpper = int.TryParse(Systolic.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var systolic);
        var hasLower = int.TryParse(Diastolic.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var diastolic);
        var at = ReadingDate.Date + new TimeSpan(ReadingTime.Hours, ReadingTime.Minutes, 0);
        ErrorMessage = string.IsNullOrWhiteSpace(Systolic) || string.IsNullOrWhiteSpace(Diastolic) ? Text.Get("Error_ReadingMissing")
            : !hasUpper || !hasLower || !BloodPressureReading.IsValid(systolic, diastolic) ? Text.Get("Error_ReadingInvalid")
            : at > _clock.Now ? Text.Get("Add_DoneAfterNow")
            : null;
        if (ErrorMessage is not null)
        {
            return;
        }

        IsReady = false;
        ErrorMessage = await PlanChange.RunAsync(
            () => _plans.LogBloodPressureAsync(systolic, diastolic, DateOnly.FromDateTime(at), TimeOnly.FromDateTime(at)),
            "add.log_reading",
            _errors);
        IsReady = true;
        if (ErrorMessage is not null)
        {
            return;
        }

        Reset();
        _notifier.Show(Text.Format("Toast_ReadingLogged", Text.Format("Reading_Value", systolic, diastolic)));
    }

    /// <summary>Adds the task as "Add a task" on Your week does, and says so as it does.</summary>
    private async Task SaveTaskAsync()
    {
        var title = TaskTitle.Trim();
        ErrorMessage = title.Length == 0 ? Text.Get("Add_NameMissing")
            : title.Length > MaxTaskLength ? Text.Format("Error_TaskTooLong", MaxTaskLength)
            : null;
        if (ErrorMessage is not null)
        {
            return;
        }

        // Without a date it is for a trimester or a week, and she says which.
        var (trimester, week) = TaskHasDate ? (null, null) : TaskPeriod.ToPeriod();
        if (!TaskHasDate && trimester is null && week is null)
        {
            ErrorMessage = Text.Get("Add_ChooseWhen");
            return;
        }

        DateOnly? date = TaskHasDate ? DateOnly.FromDateTime(TaskDate) : null;
        TimeOnly? time = TaskHasDate ? TimeOnly.FromTimeSpan(TaskTime) : null;
        IsReady = false;
        ErrorMessage = await PlanChange.RunAsync(() => _plans.AddTaskAsync(title, date, time, trimester, week), "add.add_task", _errors);
        IsReady = true;
        if (ErrorMessage is not null)
        {
            return;
        }

        Reset();
        _notifier.Show(date is { } day && time is { } at
            ? Text.Format("Toast_TaskAdded", DisplayFormat.Date(day, _clock.Today), DisplayFormat.Time(at))
            : Text.Get("Toast_TaskAddedNoDate"));
    }

    /// <summary>After a save: nothing chosen or typed, as the screen first opens.</summary>
    private void Reset()
    {
        _chosen = null;
        _filledName = string.Empty;
        Kind = null;
        IsTask = false;
        IsBloodPressure = false;
        Systolic = string.Empty;
        Diastolic = string.Empty;
        When = null;
        Name = string.Empty;
        Note = string.Empty;
        TaskTitle = string.Empty;
        TaskHasDate = false;
        ErrorMessage = null;
        IsReady = true;
        MarkSuggestion();
    }

    /// <summary>The pregnancy's dating, from the last read of her plan (or as if it began today, before the first).</summary>
    private PregnancyDating Dating => _dating ?? PregnancyDating.FromLastMenstrualPeriod(_clock.Today);

    /// <summary>Saying when (an entry's, or a task's without a date) can make "Save" look ready.</summary>
    private void OnTimingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimingDraft.Mode))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    partial void OnNameChanged(string value)
    {
        if (_chosen is { } tapped && !Same(tapped.Title, value))
        {
            _chosen = null;
        }

        MarkSuggestion();
    }

    /// <summary>What stops the save, in the order she fills the screen in; null when nothing does.</summary>
    private string? Problem()
    {
        var name = Name.Trim();
        return Kind is null ? Text.Get("Add_ChooseWhat")
            : name.Length == 0 ? Text.Get("Add_NameMissing")
            : name.Length > MaxNameLength ? Text.Format("Error_EntryTooLong", MaxNameLength)
            : When is null || Timing.Mode is null ? Text.Get("Add_ChooseWhen")
            : When == AddWhen.Done && Timing.IsDateTime && Timing.At > _clock.Now ? Text.Get("Add_DoneAfterNow")
            : null;
    }

    /// <summary>
    /// The name in every language Maia has, when it is one Maia gave (her doctor's visit, a hospital visit, one of the
    /// usual exams), so it follows the plan when she switches language; null for a name she typed.
    /// </summary>
    private LocalizedText? NamesFor(EntryKind kind, string name, NameSuggestion? suggestion) => kind switch
    {
        EntryKind.DoctorVisit when _visitName is { } visit && Same(name, visit.In(Text.Language)) => visit,
        EntryKind.HospitalVisit when Same(name, Text.Get("Kind_HospitalVisit")) => InEveryLanguage("Kind_HospitalVisit"),
        EntryKind.Exam => suggestion?.Names,
        _ => null,
    };

    /// <summary>
    /// The suggestion the name comes from, only for an exam or test: the chip she tapped while the name is still its own,
    /// else the first whose name she typed, with its week or without.
    /// </summary>
    private NameSuggestion? Matching(string name) =>
        !IsExam || string.IsNullOrWhiteSpace(name) ? null
        : _chosen is { } tapped && Same(tapped.Title, name) ? tapped
        : Suggestions.FirstOrDefault(s => Same(s.Title, name)) ?? Suggestions.FirstOrDefault(s => Same(s.Plain, name));

    private void MarkSuggestion()
    {
        var chosen = Matching(Name.Trim());
        foreach (var suggestion in Suggestions)
        {
            suggestion.IsChosen = ReferenceEquals(suggestion, chosen);
        }
    }

    /// <summary>A suggestion tapped: the entry takes its name.</summary>
    private void Choose(NameSuggestion suggestion)
    {
        _chosen = suggestion;
        Fill(suggestion.Title);
        MarkSuggestion();
        ErrorMessage = null;
    }

    private void Fill(string name)
    {
        _filledName = name;
        Name = name;
    }

    /// <summary>Whether the name is one a chip filled in (or none), rather than one she typed.</summary>
    private bool IsFilled(string name) =>
        string.IsNullOrWhiteSpace(name) || string.Equals(name.Trim(), _filledName.Trim(), StringComparison.Ordinal);

    /// <summary>
    /// The usual extras her plan has in no language, each kept as the same chip so the one she chose stays chosen.
    /// </summary>
    private void OfferSuggestions(PlanSnapshot snapshot)
    {
        var wanted = new List<NameSuggestion>();

        // What the plan already has, in every language: an extra it has in Greek is not offered again in English.
        var taken = snapshot.Checks
            .SelectMany(c => c.Title.Values.Values)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in ExtraKeys)
        {
            var names = InEveryLanguage(key);
            if (!names.Values.Values.Any(taken.Contains))
            {
                var item = Suggestions.FirstOrDefault(s => s.ExtraKey == key) ?? new NameSuggestion(Choose, key, names);
                item.Title = item.Plain = Text.Get(key);
                wanted.Add(item);
            }
        }

        // A chip she tapped that is no longer offered (it is back on her journey, say) lets go; the name she has stays.
        if (_chosen is { } tapped && !wanted.Contains(tapped))
        {
            _chosen = null;
        }

        CollectionSync.Apply(Suggestions, wanted, ReferenceEquals);
        OnPropertyChanged(nameof(ShowSuggestions));
        MarkSuggestion();
    }

    private static LocalizedText InEveryLanguage(string key) => new(new Dictionary<string, string>
    {
        [LocalizedText.English] = Text.In(key, Text.English),
        [LocalizedText.Greek] = Text.In(key, Text.Greek),
    });

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A name chip under "Exam or test": one of the exams doctors often ask for (<see cref="ExtraKey"/>, named in every
/// language).
/// </summary>
public sealed partial class NameSuggestion : ObservableObject
{
    private readonly Action<NameSuggestion> _choose;

    internal NameSuggestion(Action<NameSuggestion> choose, string extraKey, LocalizedText names)
    {
        _choose = choose;
        ExtraKey = extraKey;
        Names = names;
    }

    /// <summary>The text key of the extra.</summary>
    public string ExtraKey { get; }

    /// <summary>An extra's name in every language, saved with it so the entry follows a language switch.</summary>
    public LocalizedText Names { get; }

    /// <summary>"Growth scan", in the language shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial string Title { get; internal set; } = string.Empty;

    /// <summary>Its name without the week that tells it apart, so typing "Check-up" finds it too.</summary>
    internal string Plain { get; set; } = string.Empty;

    /// <summary>"+ Growth scan", or "✓ Growth scan" once it names the entry.</summary>
    public string Label => (IsChosen ? "✓ " : "+ ") + Title;

    /// <summary>Whether the entry is named after it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    public partial bool IsChosen { get; internal set; }

    [RelayCommand]
    private void Choose() => _choose(this);
}
