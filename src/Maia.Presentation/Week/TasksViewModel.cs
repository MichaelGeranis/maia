using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Week;

/// <summary>
/// Your tasks (pushed from Your week, its slot still lit): every task she still has to do, in groups (Earlier · This
/// week · Later · No date, where a task for a trimester or a week stands, by its week), each row ticked, changed and
/// deleted as on Your week (<see cref="TaskRows"/>). Tasks she ticked off are not kept: they are deleted as the list
/// loads, and their reminders go with them.
/// The list is a virtualised one on the phone, so however long it is, only the rows on screen are built.
/// </summary>
public sealed partial class TasksViewModel : ObservableObject
{
    private readonly PlanQueries _queries;
    private readonly PlanService _plans;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private readonly TasksFocus _focus;
    private readonly TaskRows _rows;
    private readonly Dictionary<TaskGroup, TaskGroupRow> _headings = [];

    public TasksViewModel(
        PlanQueries queries,
        PlanService plans,
        IClock clock,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors,
        TasksFocus focus)
    {
        _queries = queries;
        _plans = plans;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
        _focus = focus;
        _rows = new TaskRows(plans, clock, notifier, errors, "tasks", LoadAsync);
        _rows.EditorClosed += (_, _) => EditorClosed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when a group should be brought to the top of the screen: the one asked for.</summary>
    public event EventHandler<object>? ScrollRequested;

    /// <summary>Raised when a task's editor closes, so the page can put the keyboard away.</summary>
    public event EventHandler? EditorClosed;

    /// <summary>The list: each group's heading (<see cref="TaskGroupRow"/>) followed by its tasks (<see cref="TaskItem"/>).</summary>
    [ObservableProperty]
    public partial ObservableCollection<object> Rows { get; private set; } = [];

    /// <summary>Whether she has no tasks at all.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>A group's heading, in the language Maia shows.</summary>
    internal static string TitleOf(TaskGroup group) => Text.Get(group switch
    {
        TaskGroup.Earlier => "Tasks_Earlier",
        TaskGroup.ThisWeek => "Tasks_ThisWeek",
        TaskGroup.Later => "Week_TasksLater",
        _ => "Week_TasksNoDate",
    });

    /// <summary>
    /// Deletes the tasks she ticked off, then shows every other task in its group, keeping the rows she sees (and one
    /// open for changes) in place. When a group was asked for, the list then opens at it.
    /// </summary>
    public async Task LoadAsync()
    {
        var overview = await _queries.GetTasksAsync();
        if (overview is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        if (overview.Tasks.Any(t => t.IsDone))
        {
            _rows.CloseEditors();
            var error = await PlanChange.RunAsync(() => _plans.ClearDoneTasksAsync(), "tasks.clear_done", _errors);
            if (error is not null)
            {
                _notifier.ShowNotice(error);
            }

            overview = overview with { Tasks = overview.Tasks.Where(t => !t.IsDone).ToList() };
        }

        Show(overview);

        // The first group needs no scrolling: it is under the title already, and the title stays in view.
        if (_focus.Take() is { } group && _headings.GetValueOrDefault(group) is { } heading && Rows.IndexOf(heading) > 0)
        {
            ScrollRequested?.Invoke(this, heading);
        }
    }

    private void Show(TasksOverview overview)
    {
        _rows.Age = overview.Age;
        var wanted = new List<object>(overview.Tasks.Count + 4);
        var tasks = new List<TaskItem>(overview.Tasks.Count);
        var groups = overview.Tasks.GroupBy(t => TaskGroups.Of(t, overview.Today)).OrderBy(g => g.Key);
        foreach (var group in groups)
        {
            wanted.Add(Heading(group.Key));
            foreach (var task in group)
            {
                var row = _rows.Row(task);
                wanted.Add(row);
                tasks.Add(row);
            }
        }

        _rows.KeepOnly(tasks);
        if (Rows.Count == 0)
        {
            // All at once the first time: one change for the list instead of one per row.
            Rows = new ObservableCollection<object>(wanted);
        }
        else
        {
            CollectionSync.Apply(Rows, wanted, ReferenceEquals);
        }

        IsEmpty = overview.Tasks.Count == 0;
    }

    /// <summary>A group's heading row, kept across loads so the list keeps its place.</summary>
    private TaskGroupRow Heading(TaskGroup group)
    {
        if (!_headings.TryGetValue(group, out var heading))
        {
            heading = new TaskGroupRow(group);
            _headings[group] = heading;
        }

        heading.Show(TitleOf(group));
        return heading;
    }

    /// <summary>"Back", to Your week.</summary>
    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();
}

/// <summary>A group's heading on Your tasks: "EARLIER".</summary>
public sealed partial class TaskGroupRow : ObservableObject
{
    internal TaskGroupRow(TaskGroup group)
    {
        Group = group;
    }

    public TaskGroup Group { get; }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    internal void Show(string title) => Title = title;
}
