using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Plan;

namespace Maia.Presentation.Week;

/// <summary>What a task row asks of the screen it is on (Your week, Your tasks): its changes go through the plan there.</summary>
internal interface ITaskHost
{
    /// <summary>Today, from which <see cref="TaskItem.EarliestDate"/> counts back.</summary>
    DateOnly Today { get; }

    /// <summary>Now, to the minute: where a task given a date starts.</summary>
    DateTime Now { get; }

    /// <summary>How far the pregnancy is today: where a trimester or a week chosen for a task starts.</summary>
    GestationalAge Age { get; }

    Task ToggleAsync(TaskItem task);

    /// <summary>Opens this row's editor and closes any other, and the "Add a task" form.</summary>
    void OpenEditor(TaskItem task);

    Task SaveAsync(TaskItem task);

    Task DeleteAsync(TaskItem task);

    /// <summary>A row's editor closed (saved, deleted or cancelled): the keyboard it used can go.</summary>
    void EditorClosed();
}

/// <summary>
/// A to-do as a row: "Call the clinic" with "Thu 1 Oct · 09:00" (or "Week 26", "3rd trimester") underneath and a round
/// checkbox, the same on Today and on All tasks. A ticked task stays where it is, struck through, until the list is
/// shown again, so a slip can be undone. Tapping the circle ticks it; tapping its words opens it in place to change its
/// title and when it is (a date and time, or a trimester or a week), or delete it (asked once more, in place).
/// </summary>
public sealed partial class TaskItem : ObservableObject
{
    private readonly ITaskHost _host;

    internal TaskItem(TaskId id, ITaskHost host)
    {
        Id = id;
        _host = host;
    }

    public TaskId Id { get; }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>"Thu 1 Oct · 09:00", "Week 26" or "3rd trimester"; null for a task with none of them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWhen))]
    public partial string? When { get; private set; }

    public bool HasWhen => When is not null;

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    /// <summary>Just added from Your week's card: the row glows briefly as it appears (not with reduced motion).</summary>
    [ObservableProperty]
    public partial bool IsNew { get; internal set; }

    /// <summary>When the task is due, as saved.</summary>
    public DateOnly? DueDate { get; private set; }

    public TimeOnly? DueTime { get; private set; }

    /// <summary>The trimester it is for, as saved, when it has no date.</summary>
    public Trimester? Trimester { get; private set; }

    /// <summary>The week it is for, as saved, when it has no date.</summary>
    public int? Week { get; private set; }

    /// <summary>Without a date: the trimester or the week it is for, as she chooses it in the editor.</summary>
    public TimingDraft EditPeriod { get; } = new();

    /// <summary>A calm line in the editor when something is missing ("Choose when it is.").</summary>
    [ObservableProperty]
    public partial string? EditError { get; internal set; }

    /// <summary>Whether the row is open to change it.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(EditLengthHint))]
    public partial string EditTitle { get; set; } = string.Empty;

    /// <summary>How much room the task's words have left, once she nears the limit; null until then.</summary>
    public string? EditLengthHint => TextLimit.Hint(EditTitle, WeekViewModel.MaxTaskLength);

    /// <summary>Whether it has a date and time; without, it is a plain to-do.</summary>
    [ObservableProperty]
    public partial bool EditHasDate { get; set; }

    [ObservableProperty]
    public partial DateTime EditDate { get; set; }

    [ObservableProperty]
    public partial TimeSpan EditTime { get; set; }

    /// <summary>
    /// A task with a day and no time keeps no time when she changes something else (its words, its day): the editor
    /// shows a time, but only one she sets herself is saved, so renaming it doesn't quietly give it this minute.
    /// </summary>
    private bool _keepsNoTime;

    /// <summary>The time to save: the one in the editor, or none for a task without a date or one that had only a day.</summary>
    internal TimeOnly? EditedTime => EditHasDate && !_keepsNoTime ? TimeOnly.FromTimeSpan(EditTime) : null;

    partial void OnEditTimeChanged(TimeSpan value) => _keepsNoTime = false;

    /// <summary>
    /// The earliest day the picker offers (<see cref="EarliestDate"/>), or the task's own day when that is earlier, so the
    /// picker never moves it on its own.
    /// </summary>
    [ObservableProperty]
    public partial DateTime EditMinimumDate { get; private set; }

    /// <summary>Whether "Delete this task?" is showing.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; private set; }

    /// <param name="task">The to-do as saved.</param>
    internal void Show(PlanTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        Title = task.Title;
        DueDate = task.DueDate;
        DueTime = task.DueTime;
        Trimester = task.Trimester;
        Week = task.Week;
        When = WhenText(task, _host.Today);
        IsDone = task.IsDone;
    }

    /// <summary>
    /// Opens the editor with what the task holds now. A task without a date is offered today at the time it is now,
    /// should she turn the date on, and its trimester or week (nothing chosen for one saved with neither).
    /// </summary>
    internal void BeginEdit()
    {
        var today = _host.Today;
        var now = _host.Now;
        var date = DueDate ?? DateOnly.FromDateTime(now);
        EditTitle = Title;
        EditHasDate = DueDate is not null;
        var earliest = EarliestDate(today);
        EditMinimumDate = (date < earliest ? date : earliest).ToDateTime(TimeOnly.MinValue);
        EditDate = date.ToDateTime(TimeOnly.MinValue);
        EditTime = (DueTime ?? TimeOnly.FromDateTime(now)).ToTimeSpan();
        _keepsNoTime = DueDate is not null && DueTime is null;
        EditPeriod.StartPeriod(_host.Age, today.ToDateTime(TimeOnly.MinValue), Trimester, Week);
        EditError = null;
        IsConfirmingDelete = false;
        IsEditing = true;
    }

    /// <summary>
    /// The earliest day a task can be set for: one in the past is fine, as far back as a pregnancy goes
    /// (<see cref="PregnancyDating.MaxSupported"/> before <paramref name="today"/>).
    /// </summary>
    internal static DateOnly EarliestDate(DateOnly today) => today.AddDays(-PregnancyDating.MaxSupported.TotalDays);

    internal void EndEdit()
    {
        IsConfirmingDelete = false;
        if (IsEditing)
        {
            IsEditing = false;
            _host.EditorClosed();
        }
    }

    /// <summary>
    /// "Thu 1 Oct · 09:00" (with the year when not <paramref name="today"/>'s), "Week 26" or "3rd trimester"; null for a
    /// task saved with none of them. Journey's task rows read the same.
    /// </summary>
    internal static string? WhenText(PlanTask task, DateOnly today)
    {
        if (task.DueDate is not { } day)
        {
            return DisplayFormat.TaskPeriod(task.Trimester, task.Week);
        }

        var when = DisplayFormat.Date(day, today);
        if (task.DueTime is { } at)
        {
            when += " · " + DisplayFormat.Time(at);
        }

        return when;
    }

    [RelayCommand]
    private Task ToggleAsync() => _host.ToggleAsync(this);

    /// <summary>Opens the row to change it; tapping its words again closes it.</summary>
    [RelayCommand]
    private void Edit()
    {
        if (IsEditing)
        {
            EndEdit();
        }
        else
        {
            _host.OpenEditor(this);
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(EditTitle) && EditTitle.Trim().Length <= WeekViewModel.MaxTaskLength;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => _host.SaveAsync(this);

    [RelayCommand]
    private void Cancel() => EndEdit();

    [RelayCommand]
    private void AskDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void KeepIt() => IsConfirmingDelete = false;

    [RelayCommand]
    private Task DeleteAsync() => _host.DeleteAsync(this);
}
