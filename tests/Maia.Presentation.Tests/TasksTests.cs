using System.Xml.Linq;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Presentation.Week;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Your tasks: every task in its group, opened from Your week at the group she tapped, each row ticked, changed and
/// deleted as on Your week; tasks she ticked off are deleted as it loads.
/// </summary>
public sealed class TasksTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Every_open_task_is_in_its_group_earlier_this_week_later_no_date()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        await _app.Plans.AddTaskAsync("Order the pram", new DateOnly(2026, 11, 20), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Buy a pillow", new DateOnly(2026, 10, 6), new TimeOnly(18, 0));
        var ticked = await _app.Plans.AddTaskAsync("Book the antenatal class", new DateOnly(2026, 10, 2));
        await _app.Plans.SetTaskDoneAsync(ticked, true);
        var vm = _app.CreateTasks();

        await vm.LoadAsync();

        Assert.Equal(
            new[]
            {
                "[Past]", "Send the form",
                "[This week]", "Call clinic", "Buy a pillow",
                "[Later]", "Order the pram",
                "[No date]", "Pack the hospital bag",
            },
            vm.Rows.Select(Describe));
        Assert.False(vm.IsEmpty);
        Assert.DoesNotContain(_app.Repository.Stored!.Tasks, t => t.Title == "Book the antenatal class");
        Assert.Equal("Mon 28 Sep · 09:00", vm.Rows.OfType<TaskItem>().Single(t => t.Title == "Send the form").When);
    }

    [Fact]
    public async Task It_opens_at_the_group_it_was_asked_to_open_at()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        await _app.Plans.AddTaskAsync("Order the pram", new DateOnly(2026, 11, 20));
        _app.TasksFocus.Request(TaskGroup.Later);
        var vm = _app.CreateTasks();
        var scrolledTo = new List<object>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);

        await vm.LoadAsync();
        await vm.LoadAsync();

        // Once, as it opens: coming back to it (or a reload after a change) keeps her place.
        var heading = Assert.IsType<TaskGroupRow>(Assert.Single(scrolledTo));
        Assert.Equal((TaskGroup.Later, "Later"), (heading.Group, heading.Title));
    }

    [Fact]
    public async Task A_group_she_asked_for_that_is_gone_opens_at_the_top()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        _app.TasksFocus.Request(TaskGroup.Later);
        var vm = _app.CreateTasks();
        var scrolled = 0;
        vm.ScrollRequested += (_, _) => scrolled++;

        await vm.LoadAsync();

        Assert.Equal(0, scrolled);
        Assert.Null(_app.TasksFocus.Take());
    }

    [Fact]
    public async Task The_first_group_opens_under_the_title_without_scrolling_past_it()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28));
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        _app.TasksFocus.Request(TaskGroup.Earlier);
        var vm = _app.CreateTasks();
        var scrolled = 0;
        vm.ScrollRequested += (_, _) => scrolled++;

        await vm.LoadAsync();

        Assert.Equal(0, scrolled);
        Assert.Equal("[Past]", Describe(vm.Rows[0]));
    }

    [Fact]
    public async Task A_long_list_arrives_in_one_change_and_keeps_its_rows_after()
    {
        await _app.CreatePlanAsync();
        var many = Enumerable.Range(0, 1000).Select(i => ((DateOnly?)TestData.Today.AddDays((i % 200) - 100), (TimeOnly?)null, i % 3 == 0));
        await _app.Repository.SaveAsync(_app.Repository.Stored!.WithTasks(many), CancellationToken.None);
        var vm = _app.CreateTasks();
        var lists = 0;
        vm.PropertyChanged += (_, e) => lists += e.PropertyName == nameof(TasksViewModel.Rows) ? 1 : 0;

        await vm.LoadAsync();

        Assert.Equal(1, lists);
        // Every third was ticked off: those are deleted as it loads, and the other 666 are listed.
        Assert.Equal(666, vm.Rows.OfType<TaskItem>().Count());
        var rows = vm.Rows.ToList();
        var changes = 0;
        vm.Rows.CollectionChanged += (_, _) => changes++;

        await vm.LoadAsync();

        Assert.Equal(rows, vm.Rows);
        Assert.Equal(0, changes);
        Assert.Equal(1, lists);
    }

    [Fact]
    public async Task With_no_tasks_it_says_the_list_is_empty()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateTasks();

        await vm.LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task Without_a_plan_it_sends_her_to_setup()
    {
        await _app.CreateTasks().LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_row_ticks_changes_and_deletes_as_on_your_week()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Order the pram", new DateOnly(2026, 11, 20), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        var vm = _app.CreateTasks();
        await vm.LoadAsync();
        var pram = Task(vm, "Order the pram");
        var bag = Task(vm, "Pack the hospital bag");

        // Tick: it stays where it is, struck through, until the list is shown again; then it is deleted.
        await bag.ToggleCommand.ExecuteAsync(null);
        Assert.True(bag.IsDone);
        Assert.True(_app.Repository.Stored!.Tasks.Single(t => t.Title == "Pack the hospital bag").IsDone);
        Assert.Equal("[No date]", Describe(vm.Rows[vm.Rows.IndexOf(bag) - 1]));

        // Change: it moves to the group of its new day, and the ticked one is gone.
        pram.EditCommand.Execute(null);
        pram.EditDate = new DateTime(2026, 10, 2);
        await pram.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Task updated.", _app.Notifier.Shown[^1]);
        Assert.Equal(new[] { "[This week]", "Order the pram" }, vm.Rows.Select(Describe));
        Assert.DoesNotContain(_app.Repository.Stored!.Tasks, t => t.Title == "Pack the hospital bag");
        Assert.Same(pram, Task(vm, "Order the pram"));

        // Delete: asked once more, then gone from the list.
        pram.EditCommand.Execute(null);
        pram.AskDeleteCommand.Execute(null);
        await pram.DeleteCommand.ExecuteAsync(null);
        Assert.Equal("Task deleted.", _app.Notifier.Shown[^1]);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task Renaming_a_task_with_only_a_day_keeps_it_without_a_time_until_she_sets_one()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", new DateOnly(2026, 10, 20), null);
        var vm = _app.CreateTasks();
        await vm.LoadAsync();
        var bag = Task(vm, "Pack the hospital bag");

        bag.EditCommand.Execute(null);
        bag.EditTitle = "Pack the bag";
        await bag.SaveCommand.ExecuteAsync(null);
        var stored = _app.Repository.Stored!.Tasks.Single(t => t.Title == "Pack the bag");
        Assert.Equal((new DateOnly(2026, 10, 20), (TimeOnly?)null), (stored.DueDate, stored.DueTime));

        // A time she sets herself is kept.
        Task(vm, "Pack the bag").EditCommand.Execute(null);
        Task(vm, "Pack the bag").EditTime = new TimeSpan(18, 0, 0);
        await Task(vm, "Pack the bag").SaveCommand.ExecuteAsync(null);
        Assert.Equal(new TimeOnly(18, 0), _app.Repository.Stored!.Tasks.Single(t => t.Title == "Pack the bag").DueTime);
    }

    [Fact]
    public async Task An_overdue_task_keeps_its_day_when_opened()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28), new TimeOnly(9, 0));
        var vm = _app.CreateTasks();
        await vm.LoadAsync();
        var row = Task(vm, "Send the form");

        row.EditCommand.Execute(null);

        Assert.Equal(new DateTime(2026, 9, 28), row.EditDate);

        // The past is fine: as far back as a pregnancy goes.
        Assert.Equal(new DateTime(2025, 11, 26), row.EditMinimumDate);
    }

    [Fact]
    public async Task A_task_without_a_date_is_offered_now_to_the_minute_when_opened()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        _app.Clock.SetTime(TestData.Today, new TimeOnly(15, 42, 30));
        var vm = _app.CreateTasks();
        await vm.LoadAsync();
        var row = Task(vm, "Pack the hospital bag");

        row.EditCommand.Execute(null);

        Assert.Equal((false, new DateTime(2026, 9, 30), new TimeSpan(15, 42, 0)), (row.EditHasDate, row.EditDate, row.EditTime));
        Assert.Equal(new DateTime(2025, 11, 26), row.EditMinimumDate);
    }

    [Fact]
    public async Task Back_goes_back_to_your_week()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateTasks();
        await vm.LoadAsync();

        await vm.BackCommand.ExecuteAsync(null);

        Assert.Equal("back", _app.Navigator.Visited[^1]);
    }

    [Fact]
    public async Task Cancelling_a_change_keeps_the_task_as_it_was()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", week: 30);
        var vm = _app.CreateTasks();
        var closed = 0;
        vm.EditorClosed += (_, _) => closed++;
        await vm.LoadAsync();
        var row = Task(vm, "Call clinic");

        row.EditCommand.Execute(null);
        Assert.False(row.EditHasDate);
        Assert.Equal(new DateTime(2026, 9, 30), row.EditDate);
        row.EditTitle = "Something else";
        row.CancelCommand.Execute(null);

        Assert.False(row.IsEditing);
        Assert.Equal(1, closed);
        Assert.Equal("Call clinic", row.Title);
        Assert.Equal("Call clinic", _app.Repository.Stored!.Tasks.Single().Title);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Ticked_off_tasks_are_deleted_as_your_tasks_loads_and_never_listed()
    {
        await _app.CreatePlanAsync();
        var form = await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28));
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        await _app.Plans.SetTaskDoneAsync(form, true);
        var vm = _app.CreateTasks();

        await vm.LoadAsync();

        Assert.Equal(new[] { "[This week]", "Call clinic" }, vm.Rows.Select(Describe));
        Assert.Equal("Call clinic", Assert.Single(_app.Repository.Stored!.Tasks).Title);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task A_task_ticked_on_your_tasks_is_deleted_at_the_next_load()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28));
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        var vm = _app.CreateTasks();
        await vm.LoadAsync();

        await Task(vm, "Send the form").ToggleCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        Assert.Equal(new[] { "[This week]", "Call clinic" }, vm.Rows.Select(Describe));
        Assert.Single(_app.Repository.Stored!.Tasks);
    }

    [Fact]
    public async Task Done_tasks_that_cannot_be_deleted_are_said_calmly_and_still_not_listed()
    {
        await _app.CreatePlanAsync();
        var form = await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28));
        await _app.Plans.SetTaskDoneAsync(form, true);
        var vm = _app.CreateTasks();
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.LoadAsync();

        Assert.Equal("Something went wrong. Please try again.", Assert.Single(_app.Notifier.Notices));
        Assert.Single(_app.Repository.Stored!.Tasks);
        Assert.Equal("tasks.clear_done", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Empty(vm.Rows);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Your_tasks_in_greek()
    {
        await _app.CreatePlanAsync();
        var form = await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28));
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        await _app.Plans.AddTaskAsync("Order the pram", new DateOnly(2026, 11, 20));
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        await _app.Plans.SetTaskDoneAsync(form, true);
        await _app.Plans.SetTaskDoneAsync(await _app.Plans.AddTaskAsync("Book the class", new DateOnly(2026, 9, 29)), true);
        using var greek = new CultureScope("el-GR");
        var vm = _app.CreateTasks();

        await vm.LoadAsync();

        Assert.Equal(new[] { "Αυτή την εβδομάδα", "Αργότερα", "Χωρίς ημερομηνία" }, vm.Rows.Select(Title).OfType<string>());
    }

    [Fact]
    public void Your_tasks_is_a_virtualised_list_pushed_from_your_week_with_a_back_pill_and_no_bottom_bar()
    {
        var page = XDocument.Load(Repo.App("Views", "TasksPage.xaml"));
        var root = page.Root!;

        Assert.Null(AccessibilityTests.Attr(root, "Tab"));
        Assert.Equal("{Binding BackCommand}", AccessibilityTests.Attr(Assert.Single(page.Descendants(), e => e.Name.LocalName == "BackPill"), "Command"));
        var list = Assert.Single(page.Descendants(), e => e.Name.LocalName == "CollectionView");
        Assert.Equal("{Binding Rows}", AccessibilityTests.Attr(list, "ItemsSource"));
        Assert.DoesNotContain(page.Descendants(), e => e.Name.LocalName.EndsWith("BindableLayout.ItemTemplate", StringComparison.Ordinal));
        Assert.Contains("Routes.Tasks, typeof(TasksPage)", Repo.CodeOf(Repo.App("AppShell.xaml.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Your_week_and_your_tasks_draw_a_task_row_the_same_way()
    {
        // One row, one editor: Your week's card and Your tasks both use the TaskRowView control.
        var week = Repo.CodeOf(Repo.App("Views", "WeekPage.xaml"));
        var tasks = Repo.CodeOf(Repo.App("Views", "TasksPage.xaml"));

        Assert.Contains("<controls:TaskRowView", week, StringComparison.Ordinal);
        Assert.Contains("<controls:TaskRowView", tasks, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskEditorTemplate", week, StringComparison.Ordinal);
        Assert.Contains("TaskEditorTemplate", Repo.CodeOf(Repo.App("Controls", "TaskRowView.xaml")), StringComparison.Ordinal);
    }

    private static TaskItem Task(TasksViewModel vm, string title) => vm.Rows.OfType<TaskItem>().Single(t => t.Title == title);

    private static string? Title(object row) => row switch
    {
        TaskGroupRow group => group.Title,
        _ => null,
    };

    private static string Describe(object row) => row switch
    {
        TaskGroupRow group => $"[{group.Title}]",
        TaskItem task => task.Title,
        _ => row.ToString() ?? string.Empty,
    };
}
