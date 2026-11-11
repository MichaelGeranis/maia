using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Week;

/// <summary>
/// What Your week and Your tasks do with their task rows, in one place: one row per task kept across loads (so a row
/// she has open keeps its place and editor), ticking, one editor open at a time, saving and deleting. Each change goes
/// through the plan, a failure is reported by operation code and said calmly, and the screen then shows the plan again.
/// </summary>
internal sealed class TaskRows : ITaskHost
{
    private readonly Dictionary<TaskId, TaskItem> _rows = [];
    private readonly PlanService _plans;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private readonly string _screen;
    private readonly Func<Task> _reload;

    /// <param name="plans">Where every change goes.</param>
    /// <param name="clock">Today, and now: where a task given a date starts.</param>
    /// <param name="notifier">Says how a change went.</param>
    /// <param name="errors">Takes a failure's operation code.</param>
    /// <param name="screen">The screen's code for reports: "week" gives "week.toggle_task", "week.load"…</param>
    /// <param name="reload">Shows the plan again after a change.</param>
    public TaskRows(PlanService plans, IClock clock, INotifier notifier, IErrorReporter errors, string screen, Func<Task> reload)
    {
        _plans = plans;
        _clock = clock;
        _notifier = notifier;
        _errors = errors;
        _screen = screen;
        _reload = reload;
    }

    /// <summary>Raised as a row opens to be changed, so the screen can close what else it has open (the add form).</summary>
    public event EventHandler? EditorOpening;

    /// <summary>Raised when a row's editor closes, so the page can put the keyboard away.</summary>
    public event EventHandler? EditorClosed;

    /// <summary>Raised when she ticks a task, or unticks it, so the screen can count again.</summary>
    public event EventHandler? Toggled;

    /// <summary>The rows shown since the last <see cref="KeepOnly"/>.</summary>
    public IEnumerable<TaskItem> Shown => _rows.Values;

    DateOnly ITaskHost.Today => _clock.Today;

    DateTime ITaskHost.Now => _clock.Now;

    /// <summary>How far the pregnancy is today, from the screen's last load: where a task's trimester or week starts.</summary>
    public GestationalAge Age { get; set; }

    /// <summary>The row for <paramref name="task"/>, kept from the last load if it was shown then, showing the task as saved.</summary>
    /// <param name="task">The task as loaded.</param>
    /// <param name="asShown">
    /// The list is only redrawn, with no new load (more rows, or fewer): a row already shown stays as it is, ticked or
    /// not, since <paramref name="task"/> may be older than what she did since.
    /// </param>
    public TaskItem Row(PlanTask task, bool asShown = false)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (_rows.TryGetValue(task.Id, out var row))
        {
            if (!asShown)
            {
                row.Show(task);
            }

            return row;
        }

        row = new TaskItem(task.Id, this);
        _rows[task.Id] = row;
        row.Show(task);
        return row;
    }

    /// <summary>Lets go of every row but <paramref name="shown"/>: a task that left the list starts afresh if it comes back.</summary>
    public void KeepOnly(IEnumerable<TaskItem> shown)
    {
        var keep = shown.Select(r => r.Id).ToHashSet();
        var gone = _rows.Keys.Where(id => !keep.Contains(id)).ToList();
        foreach (var task in gone)
        {
            _rows.Remove(task);
        }
    }

    /// <summary>Closes any row open for changes.</summary>
    public void CloseEditors()
    {
        foreach (var open in _rows.Values.Where(t => t.IsEditing).ToList())
        {
            open.EndEdit();
        }
    }

    /// <summary>Shows the plan again after a change; a reload that fails says so calmly instead of ending Maia.</summary>
    public async Task ReloadAsync()
    {
        if (await ScreenLoad.ReloadAsync(_reload, _screen + ".load", _errors) is { } failed)
        {
            _notifier.ShowNotice(failed);
        }
    }

    async Task ITaskHost.ToggleAsync(TaskItem task)
    {
        var done = !task.IsDone;
        var error = await PlanChange.RunAsync(() => _plans.SetTaskDoneAsync(task.Id, done), _screen + ".toggle_task", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        task.IsDone = done;
        Toggled?.Invoke(this, EventArgs.Empty);
    }

    void ITaskHost.OpenEditor(TaskItem task)
    {
        EditorOpening?.Invoke(this, EventArgs.Empty);
        CloseEditors();
        task.BeginEdit();
    }

    async Task ITaskHost.SaveAsync(TaskItem task)
    {
        // Without a date it is for a trimester or a week, and she says which.
        var (trimester, week) = task.EditHasDate ? (null, null) : task.EditPeriod.ToPeriod();
        if (!task.EditHasDate && trimester is null && week is null)
        {
            task.EditError = Text.Get("Add_ChooseWhen");
            return;
        }

        task.EditError = null;
        var title = task.EditTitle.Trim();
        DateOnly? date = task.EditHasDate ? DateOnly.FromDateTime(task.EditDate) : null;
        var time = task.EditedTime;
        var error = await PlanChange.RunAsync(() => _plans.EditTaskAsync(task.Id, title, date, time, trimester, week), _screen + ".edit_task", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        task.EndEdit();
        _notifier.Show(Text.Get("Toast_TaskSaved"));
        await ReloadAsync();
    }

    async Task ITaskHost.DeleteAsync(TaskItem task)
    {
        var error = await PlanChange.RunAsync(() => _plans.RemoveTaskAsync(task.Id), _screen + ".delete_task", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        task.EndEdit();
        _notifier.Show(Text.Get("Toast_TaskDeleted"));
        await ReloadAsync();
    }

    void ITaskHost.EditorClosed() => EditorClosed?.Invoke(this, EventArgs.Empty);
}
