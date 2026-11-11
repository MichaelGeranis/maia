using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Plan;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Journey's cards and task rows at their edges: a card not filled in yet, saving with nothing chosen, names that are
/// missing or too long, two taps at once. Today is 24+3 (Wed 30 Sep 2026, 12:00).
/// </summary>
public sealed class JourneyCardTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task An_open_card_looks_expanded_and_a_closed_one_looks_like_its_status()
    {
        var vm = await OpenPlanAsync();
        var glucose = Row(vm, TestData.Glucose);
        Assert.Equal("NotBooked", glucose.CardKind);

        glucose.ToggleCommand.Execute(null);

        Assert.Equal("Expanded", glucose.CardKind);
    }

    [Fact]
    public async Task A_card_not_filled_in_yet_neither_opens_its_fields_nor_saves()
    {
        await _app.CreatePlanAsync();
        var row = new CheckRow(Host(), TestData.Glucose);
        var revision = _app.Repository.Stored!.Revision;

        row.StartBookCommand.Execute(null);
        row.StartEditCommand.Execute(null);
        await row.SaveCommand.ExecuteAsync(null);

        Assert.Equal((CheckPanel.Actions, (string?)null), (row.Panel, row.ErrorMessage));
        Assert.Equal(revision, _app.Repository.Stored!.Revision);
    }

    [Fact]
    public async Task A_task_row_without_a_day_says_no_when_and_without_a_way_to_tick_does_nothing()
    {
        await _app.CreatePlanAsync();
        var task = PregnancyPlan.RehydrateTask(TaskId.New(), "Pack the bag", null, null, false);
        var row = new TaskRow(Host(), task.Id);

        row.Show(task);
        await row.ToggleCommand.ExecuteAsync(null);

        Assert.Equal(("Pack the bag", string.Empty, false), (row.Title, row.When, row.IsDone));
    }

    [Fact]
    public async Task Save_on_a_card_before_book_or_edit_asks_when_it_is()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        var revision = _app.Repository.Stored!.Revision;

        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Choose when it is.", glucose.ErrorMessage);
        Assert.Equal(revision, _app.Repository.Stored!.Revision);
    }

    [Fact]
    public async Task Editing_a_booking_with_no_time_starts_at_the_start_of_its_day()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 7)));
        var vm = await OpenPlanAsync();
        var checkup = Open(vm, TestData.Checkup25);

        checkup.StartEditCommand.Execute(null);

        Assert.Equal((TimingMode.DateTime, new DateTime(2026, 10, 7), TimeSpan.Zero), (checkup.Timing.Mode, checkup.Timing.Date, checkup.Timing.Time));
    }

    [Fact]
    public async Task An_edit_with_no_name_cannot_be_saved_and_says_so()
    {
        var vm = await OpenPlanAsync();
        var checkup = await EditBookedCheckupAsync(vm);
        Assert.True(checkup.SaveCommand.CanExecute(null));

        checkup.EditName = "   ";
        Assert.False(checkup.SaveCommand.CanExecute(null));
        await checkup.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Give it a name.", checkup.ErrorMessage);
        Assert.Equal("Check-up", Stored(TestData.Checkup25).Title.In(LocalizedText.English));
    }

    [Fact]
    public async Task An_edit_with_a_name_too_long_says_how_long_it_may_be()
    {
        var vm = await OpenPlanAsync();
        var checkup = await EditBookedCheckupAsync(vm);

        checkup.EditName = new string('a', Add.AddEntryViewModel.MaxNameLength + 1);
        await checkup.SaveCommand.ExecuteAsync(null);

        Assert.Equal("That name is a little long. Keep it to 150 characters.", checkup.ErrorMessage);
        Assert.Equal("Check-up", Stored(TestData.Checkup25).Title.In(LocalizedText.English));
    }

    [Fact]
    public async Task A_past_booking_moved_to_a_week_is_updated_not_booked()
    {
        await _app.CreatePlanAsync();
        var thyroid = await _app.Plans.AddEntryAsync("Thyroid test", EntryKind.Exam, EntryWhen.Booked(new Appointment(new DateOnly(2026, 9, 28), new TimeOnly(9, 0))));
        var vm = await OpenPlanAsync();
        var row = Open(vm, thyroid);
        row.StartEditCommand.Execute(null);

        row.Timing.ChooseModeCommand.Execute("Week");
        await row.SaveCommand.ExecuteAsync(null);

        var stored = Stored(thyroid);
        Assert.Equal((CheckState.Done, (Appointment?)null, 24), (stored.State, stored.Appointment, stored.Week));
        Assert.Equal("Thyroid test updated.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Long_after_the_due_date_a_booking_can_still_be_set_for_today()
    {
        await _app.CreatePlanAsync();
        _app.Clock.SetToday(TestData.DueDate.AddDays(100));
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);

        glucose.StartBookCommand.Execute(null);

        Assert.Equal(TestData.DueDate.AddDays(100).ToDateTime(TimeOnly.MinValue), glucose.Timing.MaximumDate);
        Assert.True(glucose.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_second_tap_on_delete_while_the_first_saves_does_nothing_more()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.AskDeleteCommand.Execute(null);
        var hold = new TaskCompletionSource();
        _app.Repository.BeforeLoad = () => hold.Task;

        var first = glucose.DeleteCommand.ExecuteAsync(null);
        Assert.True(glucose.IsBusy);
        await glucose.DeleteCommand.ExecuteAsync(null);
        _app.Repository.BeforeLoad = null;
        hold.SetResult();
        await first;

        Assert.Equal("Glucose test deleted from your journey.", _app.Notifier.Shown.Single());
        Assert.DoesNotContain(_app.Repository.Stored!.Checks, c => c.Id == TestData.Glucose);
    }

    [Fact]
    public async Task An_entry_chosen_elsewhere_is_taken_even_with_no_screen_to_scroll()
    {
        await _app.CreatePlanAsync();
        _app.Focus.Request(TestData.Glucose);

        var vm = await OpenPlanAsync();

        Assert.False(_app.Focus.IsPending);
        Assert.Contains(vm.Rows, r => r.Key == "check:ogtt");
    }

    private async Task<CheckRow> EditBookedCheckupAsync(PlanViewModel vm)
    {
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 7), new TimeOnly(10, 0)));
        await vm.LoadAsync();
        var checkup = Open(vm, TestData.Checkup25);
        checkup.StartEditCommand.Execute(null);
        return checkup;
    }

    private CheckRowHost Host() => new(_app.Plans, _app.Clock, _app.Errors, _app.Notifier, () => Task.CompletedTask, _ => { });

    private async Task<PlanViewModel> OpenPlanAsync()
    {
        if (_app.Repository.Stored is null)
        {
            await _app.CreatePlanAsync();
        }

        var vm = new PlanViewModel(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);
        await vm.LoadAsync();
        return vm;
    }

    private static CheckRow Row(PlanViewModel vm, CheckId id) => vm.Rows.OfType<CheckRow>().Single(r => r.Id == id);

    private static CheckRow Open(PlanViewModel vm, CheckId id)
    {
        var row = Row(vm, id);
        if (!row.IsExpanded)
        {
            row.ToggleCommand.Execute(null);
        }

        return row;
    }

    private PlannedCheck Stored(CheckId id) => _app.Repository.Stored!.GetCheck(id);
}
