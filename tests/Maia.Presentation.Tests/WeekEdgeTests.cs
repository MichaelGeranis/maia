using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Week;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Your week and Your tasks at their edges: changes that can't be saved, limits, and lines that say nothing. Today is 24+3.</summary>
public sealed class WeekEdgeTests : IDisposable
{
    private const string WentWrong = "Something went wrong. Please try again.";

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Nothing_booked_shows_until_she_books_something()
    {
        var week = await OpenWeekAsync();
        Assert.True(week.HasNothingBooked);

        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        await week.LoadAsync();

        Assert.False(week.HasNothingBooked);
    }

    [Fact]
    public async Task A_new_task_says_how_much_room_is_left_and_can_be_added_only_while_it_fits()
    {
        var week = await OpenWeekAsync();

        week.NewTaskTitle = "Call the clinic";
        Assert.Null(week.NewTaskLengthHint);
        Assert.True(week.AddTaskCommand.CanExecute(null));

        week.NewTaskTitle = new string('a', WeekViewModel.MaxTaskLength - 1);
        Assert.Equal("1 character left", week.NewTaskLengthHint);

        week.NewTaskTitle = new string('a', WeekViewModel.MaxTaskLength + 1);
        Assert.False(week.AddTaskCommand.CanExecute(null));
    }

    [Fact]
    public async Task Leaving_your_week_with_the_short_list_showing_keeps_it_as_it_is()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var week = await OpenWeekAsync();
        var shown = week.Tasks.ToList();

        week.Leave();

        Assert.Equal(shown, week.Tasks);
    }

    [Fact]
    public async Task A_doctor_visit_not_booked_or_booked_with_someone_else_names_no_one()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync(
        [
            new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", null, null),
            new ContactToLink(CareRole.Dentist, "Dr. Maria Kosta", "+30 690 000 1234", null, null),
        ]);
        var team = _app.Repository.Stored!.CareTeam.Contacts;
        var dentist = team.Single(c => c.Role == CareRole.Dentist).Id;
        var unbooked = await _app.Plans.AddEntryAsync("Visit", EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(26));
        var withDentist = await _app.Plans.AddEntryAsync(
            "Visit", EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 7), new TimeOnly(10, 0), with: dentist)));
        var plan = _app.Repository.Stored!;
        var obGyns = team.Where(c => c.Role == CareRole.ObGyn).ToList();

        Assert.Null(WeekViewModel.WithLine(new CheckView(plan.GetCheck(unbooked), CheckStatus.NotBooked, "Visit"), obGyns));
        Assert.Null(WeekViewModel.WithLine(new CheckView(plan.GetCheck(withDentist), CheckStatus.Booked, "Visit"), obGyns));
    }

    [Fact]
    public async Task A_task_row_says_when_and_how_much_room_its_words_have_left()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var row = (await OpenWeekAsync()).Tasks.Single();
        Assert.True(row.HasWhen);

        row.EditCommand.Execute(null);
        Assert.Null(row.EditLengthHint);
        Assert.True(row.SaveCommand.CanExecute(null));

        row.EditTitle = new string('a', WeekViewModel.MaxTaskLength);
        Assert.Equal("That's all that fits: 200 characters.", row.EditLengthHint);

        row.EditTitle = new string('a', WeekViewModel.MaxTaskLength + 1);
        Assert.False(row.SaveCommand.CanExecute(null));
        row.EditTitle = " ";
        Assert.False(row.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_task_set_further_back_than_the_picker_goes_opens_on_its_own_day()
    {
        await _app.CreatePlanAsync();
        var longAgo = TaskItem.EarliestDate(TestData.Today).AddDays(-10);
        await _app.Repository.SaveAsync(_app.Repository.Stored!.WithTasks([(longAgo, null, false)]), CancellationToken.None);
        var tasks = _app.CreateTasks();
        await tasks.LoadAsync();
        var row = tasks.Rows.OfType<TaskItem>().Single();

        row.EditCommand.Execute(null);

        Assert.Equal(longAgo.ToDateTime(TimeOnly.MinValue), row.EditMinimumDate);
    }

    [Fact]
    public async Task Ticking_changing_or_deleting_a_task_that_cannot_be_saved_says_so_and_keeps_it()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var row = (await OpenWeekAsync()).Tasks.Single();

        _app.Repository.FailNextSave = new IOException("disk full");
        await row.ToggleCommand.ExecuteAsync(null);
        Assert.False(row.IsDone);

        row.EditCommand.Execute(null);
        row.EditTitle = "Call the clinic";
        _app.Repository.FailNextSave = new IOException("disk full");
        await row.SaveCommand.ExecuteAsync(null);
        Assert.True(row.IsEditing);

        _app.Repository.FailNextSave = new IOException("disk full");
        await row.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(new[] { WentWrong, WentWrong, WentWrong }, _app.Notifier.Notices);
        Assert.Equal(new[] { "week.toggle_task", "week.edit_task", "week.delete_task" }, _app.Errors.Reports.Select(r => r.Operation));
        var stored = Assert.Single(_app.Repository.Stored!.Tasks);
        Assert.Equal(("Call clinic", false), (stored.Title, stored.IsDone));
    }

    [Fact]
    public async Task Task_rows_with_no_one_listening_still_tick_open_and_close()
    {
        await _app.CreatePlanAsync();
        var id = await _app.Plans.AddTaskAsync("Call clinic", week: 30);
        var rows = new TaskRows(_app.Plans, _app.Clock, _app.Notifier, _app.Errors, "week", () => Task.CompletedTask);
        var row = rows.Row(_app.Repository.Stored!.Tasks.Single());

        await row.ToggleCommand.ExecuteAsync(null);
        row.EditCommand.Execute(null);
        Assert.True(row.IsEditing);
        row.CancelCommand.Execute(null);

        Assert.False(row.IsEditing);
        Assert.True(_app.Repository.Stored!.Tasks.Single(t => t.Id == id).IsDone);
    }

    [Fact]
    public async Task Ticking_a_task_tells_the_screen_to_count_again_and_only_shown_rows_are_kept()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", week: 30);
        await _app.Plans.AddTaskAsync("Pack the bag", week: 30);
        var rows = new TaskRows(_app.Plans, _app.Clock, _app.Notifier, _app.Errors, "week", () => Task.CompletedTask);
        var toggled = 0;
        rows.Toggled += (_, _) => toggled++;
        var all = _app.Repository.Stored!.Tasks.Select(t => rows.Row(t)).ToList();

        await all[0].ToggleCommand.ExecuteAsync(null);
        rows.KeepOnly([all[1]]);

        Assert.Equal(1, toggled);
        Assert.Equal(new[] { all[1] }, rows.Shown);
    }

    [Fact]
    public async Task Your_tasks_opened_at_a_group_takes_it_even_with_no_screen_to_scroll()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        _app.TasksFocus.Request(TaskGroup.NoDate);
        var tasks = _app.CreateTasks();

        await tasks.LoadAsync();

        Assert.Null(_app.TasksFocus.Take());
        Assert.Equal(2, tasks.Rows.OfType<TaskItem>().Count());
    }

    private async Task<WeekViewModel> OpenWeekAsync()
    {
        if (_app.Repository.Stored is null)
        {
            await _app.CreatePlanAsync();
        }

        var week = _app.CreateWeek();
        await week.LoadAsync();
        return week;
    }
}
