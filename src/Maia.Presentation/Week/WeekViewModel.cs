using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Plan;
using Maia.Presentation.Settings;

namespace Maia.Presentation.Week;

/// <summary>
/// "Your week" (design screen 04): where she is in the pregnancy, what's next (her next booking, which opens Journey
/// on it), this
/// week's to-dos (tick, add, change or delete, with or without a time), plus the ways into Questions and Breathe. The
/// task card is bounded whatever her list holds: this week's tasks only, at most <see cref="CollapsedRows"/> rows, then
/// "Show all" up to <see cref="ExpandedRows"/>; under them one "All tasks" line opens Your tasks. Every task row is a set of real views on the phone, and Your week is the first screen, so
/// a list of hundreds would hold it up, or stop Maia, before she sees anything. Each refresh reads the plan once and
/// updates the rows in place, so only a new row pops in.
/// </summary>
public sealed partial class WeekViewModel : ObservableObject
{
    public const int MaxTaskLength = PlanTask.MaxTitleLength;

    /// <summary>This week's task rows the card shows at first.</summary>
    public const int CollapsedRows = 5;

    /// <summary>The most task rows the card ever shows, after "Show all"; more are in Your tasks.</summary>
    public const int ExpandedRows = 20;

    private readonly PlanQueries _queries;
    private readonly PlanService _plans;
    private readonly IClock _clock;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private readonly IBackupHistory _backups;
    private readonly PlanFocus _planFocus;
    private readonly TaskRows _rows;
    private List<PlanTask> _thisWeek = [];
    private bool _expanded;
    private TaskId? _justAdded;
    private CheckId? _nextUpId;

    public WeekViewModel(
        PlanQueries queries,
        PlanService plans,
        IClock clock,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors,
        IBackupHistory backups,
        PlanFocus planFocus)
    {
        _queries = queries;
        _plans = plans;
        _clock = clock;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
        _backups = backups;
        _planFocus = planFocus;
        _rows = new TaskRows(plans, clock, notifier, errors, "week", LoadAsync);
        _rows.EditorOpening += (_, _) => IsTaskFormOpen = false;
        _rows.EditorClosed += (_, _) => EditorClosed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when a task's editor closes, so the page can put the keyboard away.</summary>
    public event EventHandler? EditorClosed;

    /// <summary>
    /// Whether the first load has finished. Until then the screen shows calm shapes where its cards go, never "Nothing
    /// booked yet" or "Nothing due this week" for a plan that is still being read.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// This week's open to-dos the card shows: the first <see cref="CollapsedRows"/>, or after "Show all" the first
    /// <see cref="ExpandedRows"/>, by day and time.
    /// </summary>
    public ObservableCollection<TaskItem> Tasks { get; } = [];

    [ObservableProperty]
    public partial string WeekLabel { get; private set; } = string.Empty;

    /// <summary>What a screen reader says for the week pill, which opens Calendar: "Calendar, Week 24 + 3".</summary>
    [ObservableProperty]
    public partial string WeekPillSpoken { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TrimesterText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DueText { get; private set; } = string.Empty;

    /// <summary>Share of the pregnancy elapsed, 0–1, for the journey curve.</summary>
    [ObservableProperty]
    public partial double PregnancyProgress { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNothingBooked))]
    public partial bool HasNextUp { get; private set; }

    public bool HasNothingBooked => !HasNextUp;

    [ObservableProperty]
    public partial string NextUpTitle { get; private set; } = string.Empty;

    /// <summary>"Tue 6 Oct at 10:30".</summary>
    [ObservableProperty]
    public partial string NextUpWhen { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string NextUpWith { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the next-up card says who it is with: a check from her plan once she has linked her OB-GYN, or a doctor
    /// visit with a doctor she linked whose name it doesn't already say. Never an exam, a hospital visit, something else
    /// or a task.
    /// </summary>
    [ObservableProperty]
    public partial bool HasNextUpWith { get; private set; }

    /// <summary>Whether anything is due this week (today and the six days after it).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNothingThisWeek))]
    public partial bool HasThisWeek { get; private set; }

    /// <summary>"Nothing due this week." shows instead of rows.</summary>
    public bool HasNothingThisWeek => !HasThisWeek;

    /// <summary>Whether "Show all" is offered: more this week than the card shows at first.</summary>
    [ObservableProperty]
    public partial bool CanShowAll { get; private set; }

    /// <summary>"Show all".</summary>
    [ObservableProperty]
    public partial string ShowAllText { get; private set; } = string.Empty;

    /// <summary>"All tasks": the one line under this week's, opening Your tasks.</summary>
    [ObservableProperty]
    public partial string AllTasksText { get; private set; } = string.Empty;

    /// <summary>"Ready when you need them" on the Questions tile.</summary>
    [ObservableProperty]
    public partial string QuestionsTileText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTaskFormOpen { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
    [NotifyPropertyChangedFor(nameof(NewTaskLengthHint))]
    public partial string NewTaskTitle { get; set; } = string.Empty;

    /// <summary>How much room the new task's words have left, once she nears the limit; null until then.</summary>
    public string? NewTaskLengthHint => TextLimit.Hint(NewTaskTitle, MaxTaskLength);

    /// <summary>
    /// Whether the new task gets a date and time; off until she turns it on, when it is for a trimester or a week
    /// (<see cref="NewTaskPeriod"/>) instead.
    /// </summary>
    [ObservableProperty]
    public partial bool NewTaskHasDate { get; set; }

    /// <summary>Without a date: the trimester or the week the new task is for, none chosen until she chooses.</summary>
    public TimingDraft NewTaskPeriod { get; } = new();

    /// <summary>A calm line in the form when something is missing ("Choose when it is.").</summary>
    [ObservableProperty]
    public partial string? TaskFormError { get; private set; }

    [ObservableProperty]
    public partial DateTime NewTaskDate { get; set; }

    [ObservableProperty]
    public partial TimeSpan NewTaskTime { get; set; }

    /// <summary>The earliest day a new task can be set for (see <see cref="TaskItem.EarliestDate"/>): the past is fine.</summary>
    [ObservableProperty]
    public partial DateTime MinimumDate { get; private set; }

    /// <summary>Whether the calm "Keep a copy of your plan" card shows (see <see cref="BackupReminder"/>).</summary>
    [ObservableProperty]
    public partial bool ShowBackupReminder { get; private set; }

    /// <summary>"It lives only on this phone…" or "Your last backup is from 12 Aug…".</summary>
    [ObservableProperty]
    public partial string BackupReminderText { get; private set; } = string.Empty;

    public async Task LoadAsync()
    {
        var week = await _queries.GetWeekAsync();
        if (week is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        var today = _clock.Today;

        WeekLabel = DisplayFormat.WeekLabel(week.Age);
        WeekPillSpoken = Text.Format("Week_PillSpoken", WeekLabel);
        TrimesterText = DisplayFormat.Section($"trimester{(int)week.Trimester}");
        DueText = Text.Format("Week_Due", DisplayFormat.Date(week.DueDate));
        PregnancyProgress = Math.Clamp(week.Age.TotalDays / (double)PregnancyDating.TermDays, 0, 1);

        var obGyns = week.ObGyns;
        ShowNextUp(week.NextUp);

        _rows.Age = week.Age;
        ShowTasks(week.OpenTasks, today);
        MinimumDate = TaskItem.EarliestDate(today).ToDateTime(TimeOnly.MinValue);

        var open = week.Questions.Count(q => !q.IsAnswered);
        QuestionsTileText = open == 0 ? Text.Get("Week_QuestionsTileNone") : Text.Get("Week_QuestionsTile");

        ShowBackup(week.WorthKeeping);
        IsLoaded = true;

        // The next booked entry: a check, a doctor visit, a hospital visit…
        void ShowNextUp(NextUp? next)
        {
            HasNextUp = next is not null;
            _nextUpId = next?.Entry.Check.Id;
            if (next is null)
            {
                NextUpTitle = Text.Get("Week_NothingBooked");
                NextUpWhen = string.Empty;
                NextUpWith = string.Empty;
                HasNextUpWith = false;
                return;
            }

            NextUpTitle = next.Entry.Title;
            NextUpWhen = DisplayFormat.Appointment(new Appointment(next.Date, next.Time), today);
            NextUpWith = WithLine(next.Entry, obGyns) ?? string.Empty;
            HasNextUpWith = NextUpWith.Length > 0;
        }
    }

    /// <summary>
    /// Your week is left (another tab, or a screen pushed over it): the card folds back to its first rows, so she comes
    /// back to the short list.
    /// </summary>
    public void Leave()
    {
        if (_expanded)
        {
            _expanded = false;
            ShowThisWeek(asShown: true);
        }
    }

    /// <summary>
    /// Who the next booking is with. A check from her plan is with her OB-GYN ("with Dr. Eleni Papadaki") once she has
    /// linked one; before that it says nothing rather than name someone Maia doesn't know. A doctor visit she added is
    /// with the doctor it was booked with, unless its name already says so ("Visit with Dr. Eleni Papadaki"). An exam,
    /// a hospital visit or anything else she added says nothing: Maia doesn't know who it is with.
    /// </summary>
    internal static string? WithLine(CheckView entry, IReadOnlyList<CareContact> obGyns)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(obGyns);
        var check = entry.Check;
        if (check.Kind is null)
        {
            return obGyns.Count > 0 ? Text.Format("Week_With", obGyns[0].DisplayName) : null;
        }

        return check.Kind == EntryKind.DoctorVisit
            && check.Appointment?.With is { } with
            && obGyns.FirstOrDefault(c => c.Id == with) is { } doctor
            && !entry.Title.Contains(doctor.DisplayName, StringComparison.OrdinalIgnoreCase)
                ? Text.Format("Week_With", doctor.DisplayName)
                : null;
    }

    /// <summary>Her plan lives only on this phone: once it holds something worth keeping, a calm card suggests a backup.</summary>
    private void ShowBackup(bool worthKeeping)
    {
        var last = _backups.LastBackup;
        ShowBackupReminder = BackupReminder.IsDue(worthKeeping, _clock.UtcNow, last, _backups.ReminderDismissed);
        BackupReminderText = last is { } at
            ? Text.Format("Week_BackupOld", DisplayFormat.DayMonth(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, _clock.TimeZone).DateTime)))
            : Text.Get("Week_BackupNever");
    }

    /// <summary>
    /// Her open tasks due this week (<see cref="TaskGroups.Of"/>) become rows; every other task is in Your tasks, behind
    /// the one "All tasks" line. A task just added for this week is brought into view, opening the card's full list if
    /// it falls past the first rows.
    /// </summary>
    private void ShowTasks(IReadOnlyList<PlanTask> openTasks, DateOnly today)
    {
        var thisWeek = openTasks.Where(t => TaskGroups.Of(t, today) == TaskGroup.ThisWeek).ToList();

        _thisWeek = thisWeek;
        if (_justAdded is { } added && thisWeek.FindIndex(t => t.Id == added) >= CollapsedRows)
        {
            _expanded = true;
        }

        AllTasksText = Text.Get("Week_AllTasks");

        ShowThisWeek();
        _justAdded = null;
    }

    /// <summary>
    /// Brings this week's rows in line with the plan, never more than <see cref="ExpandedRows"/>: a row she has open
    /// keeps its place and editor, a done or deleted task leaves, a new one pops in. Redrawn with no new load
    /// (<paramref name="asShown"/>: "Show all", or folding back), the rows already shown stay as she left them.
    /// </summary>
    private void ShowThisWeek(bool asShown = false)
    {
        var shown = Math.Min(_thisWeek.Count, _expanded ? ExpandedRows : CollapsedRows);
        var wanted = new List<TaskItem>(shown);
        for (var i = 0; i < shown; i++)
        {
            var row = _rows.Row(_thisWeek[i], asShown);
            row.IsNew = row.Id == _justAdded;
            wanted.Add(row);
        }

        CollectionSync.Apply(Tasks, wanted, (a, b) => a.Id == b.Id);
        _rows.KeepOnly(wanted);

        HasThisWeek = _thisWeek.Count > 0;
        CanShowAll = !_expanded && _thisWeek.Count > CollapsedRows;
        ShowAllText = Text.Get("Week_TasksShowAll");
    }

    /// <summary>
    /// Next up, tapped: Journey, scrolled to that booking (left closed). With nothing booked, Journey at today.
    /// </summary>
    [RelayCommand]
    private Task OpenNextUpAsync()
    {
        if (_nextUpId is { } id)
        {
            _planFocus.Request(id);
        }

        return _navigator.GoToPlanAsync();
    }

    /// <summary>Settings, where the backup is made.</summary>
    [RelayCommand]
    private Task BackUpAsync() => _navigator.GoToSettingsAsync();

    /// <summary>"Not now": the card goes, and comes back in a month if there is still no recent backup.</summary>
    [RelayCommand]
    private void DismissBackupReminder()
    {
        _backups.DismissReminder(_clock.UtcNow);
        ShowBackupReminder = false;
    }

    [RelayCommand]
    private Task OpenQuestionsAsync() => _navigator.GoToQuestionsAsync();

    [RelayCommand]
    private Task BreatheAsync() => _navigator.GoToBreatheAsync();

    /// <summary>The week pill, tapped: Calendar, her journey as a month.</summary>
    [RelayCommand]
    private Task OpenCalendarAsync() => _navigator.GoToCalendarAsync();

    /// <summary>"Show all": the card opens in place to this week's first <see cref="ExpandedRows"/>.</summary>
    [RelayCommand]
    private void ShowAll()
    {
        _expanded = true;
        ShowThisWeek(asShown: true);
    }

    /// <summary>"All tasks": Your tasks, from the top.</summary>
    [RelayCommand]
    private Task OpenTasksAsync() => _navigator.GoToTasksAsync();

    /// <summary>
    /// Opens "Add a task" at the top of the card, without a date and time until she turns them on (they then start at
    /// now) and with no trimester or week chosen, and closes any task open for changes.
    /// </summary>
    [RelayCommand]
    private void OpenTaskForm()
    {
        _rows.CloseEditors();
        NewTaskTitle = string.Empty;
        NewTaskHasDate = false;
        var now = _clock.Now;
        NewTaskDate = now.Date;
        NewTaskTime = now.TimeOfDay;
        NewTaskPeriod.StartPeriod(_rows.Age, now.Date);
        TaskFormError = null;
        IsTaskFormOpen = true;
    }

    /// <summary>Closes "Add a task" without adding anything.</summary>
    [RelayCommand]
    private void CloseTaskForm()
    {
        NewTaskTitle = string.Empty;
        IsTaskFormOpen = false;
    }

    private bool CanAddTask() => !string.IsNullOrWhiteSpace(NewTaskTitle) && NewTaskTitle.Trim().Length <= MaxTaskLength;

    /// <summary>
    /// Adds the task. One for this week shows in the card, glowing briefly; the toast says where any task went.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddTask))]
    private async Task AddTaskAsync()
    {
        // Without a date it is for a trimester or a week, and she says which.
        var (trimester, week) = NewTaskHasDate ? (null, null) : NewTaskPeriod.ToPeriod();
        if (!NewTaskHasDate && trimester is null && week is null)
        {
            TaskFormError = Text.Get("Add_ChooseWhen");
            return;
        }

        TaskFormError = null;
        DateOnly? date = NewTaskHasDate ? DateOnly.FromDateTime(NewTaskDate) : null;
        TimeOnly? time = NewTaskHasDate ? TimeOnly.FromTimeSpan(NewTaskTime) : null;
        TaskId added = default;
        var error = await PlanChange.RunAsync(
            async () => added = await _plans.AddTaskAsync(NewTaskTitle.Trim(), date, time, trimester, week),
            "week.add_task",
            _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        IsTaskFormOpen = false;
        _justAdded = added;
        _notifier.Show(date is { } day && time is { } at
            ? Text.Format("Toast_TaskAdded", DisplayFormat.Date(day, _clock.Today), DisplayFormat.Time(at))
            : Text.Get("Toast_TaskAddedNoDate"));
        await _rows.ReloadAsync();
    }
}
