using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Add;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>"Add to your journey" at its edges, at 24+3 on 30 Sep with the test template.</summary>
public sealed class AddEntryEdgeTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task A_tasks_words_say_how_much_room_is_left_once_she_nears_the_limit()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);

        vm.TaskTitle = "Call the clinic";
        Assert.Null(vm.TaskLengthHint);

        vm.TaskTitle = new string('a', AddEntryViewModel.MaxTaskLength - 5);
        Assert.Equal("5 characters left", vm.TaskLengthHint);
    }

    [Fact]
    public async Task A_hospital_visit_is_what_she_chose()
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("HospitalVisit");

        Assert.Equal((true, false, false, false), (vm.IsHospitalVisit, vm.IsDoctorVisit, vm.IsExam, vm.IsOther));
    }

    [Fact]
    public async Task A_task_day_set_before_the_earliest_starts_at_today_when_the_screen_loads()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();
        vm.TaskDate = new DateTime(2020, 1, 1);

        await vm.LoadAsync();

        Assert.Equal(TestData.Today.ToDateTime(TimeOnly.MinValue), vm.TaskDate);
    }

    [Fact]
    public async Task Choosing_task_again_keeps_the_day_and_words_she_set()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);
        vm.TaskTitle = "Pack the bag";
        vm.TaskDate = new DateTime(2026, 10, 9);

        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);

        Assert.Equal((true, "Pack the bag", new DateTime(2026, 10, 9)), (vm.IsTask, vm.TaskTitle, vm.TaskDate));
    }

    [Theory]
    [InlineData("Exam")]
    [InlineData("Nonsense")]
    [InlineData("42")]
    public async Task Choosing_the_same_kind_again_or_one_maia_does_not_have_changes_nothing(string kind)
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Give it a name.", vm.ErrorMessage);

        vm.ChooseKindCommand.Execute(kind);

        Assert.Equal((EntryKind.Exam, "Give it a name."), (vm.Kind!.Value, vm.ErrorMessage));
    }

    [Fact]
    public async Task Choosing_when_before_her_plan_is_read_starts_the_pickers_at_today()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();

        vm.ChooseWhenCommand.Execute("Done");

        Assert.Equal(TestData.Today.ToDateTime(TimeOnly.MinValue), vm.Timing.MinimumDate);
    }

    [Fact]
    public async Task Long_after_the_due_date_an_entry_can_still_be_set_for_today()
    {
        await _app.CreatePlanAsync();
        var today = TestData.DueDate.AddDays(100);
        _app.Clock.SetToday(today);
        var vm = _app.CreateAdd();
        await vm.LoadAsync();

        vm.ChooseWhenCommand.Execute("Booked");

        Assert.Equal(today.ToDateTime(TimeOnly.MinValue), vm.Timing.MaximumDate);
    }

    [Fact]
    public async Task A_task_too_long_says_how_long_it_may_be_and_adds_nothing()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);
        vm.TaskTitle = new string('a', AddEntryViewModel.MaxTaskLength + 1);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("That's a little long. Keep it to 200 characters.", vm.ErrorMessage);
        Assert.Empty(_app.Repository.Stored!.Tasks);
    }

    [Fact]
    public async Task A_task_that_cannot_be_saved_says_so_and_keeps_her_words()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);
        vm.TaskTitle = "Pack the bag";
        vm.TaskPeriod.ChooseModeCommand.Execute("Week");
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", vm.ErrorMessage);
        Assert.Equal((true, "Pack the bag", true), (vm.IsTask, vm.TaskTitle, vm.IsReady));
        Assert.Equal("add.add_task", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Without_an_ob_gyn_a_doctor_visit_named_as_maia_names_it_follows_her_language_and_names_no_one()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("DoctorVisit");
        vm.Name = "Appointment";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");

        await vm.SaveCommand.ExecuteAsync(null);

        var visit = Assert.Single(_app.Repository.Stored!.DoctorVisits());
        Assert.Equal(("Appointment", "Ραντεβού"), (visit.Title.In(LocalizedText.English), visit.Title.In(LocalizedText.Greek)));
        Assert.Null(visit.Appointment);
    }

    [Fact]
    public async Task A_chip_she_tapped_is_no_longer_chosen_once_it_reads_in_another_language()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Suggestions.Single(s => s.Title == "Extra ultrasound").ChooseCommand.Execute(null);

        using var greek = new CultureScope("el-GR");
        await vm.LoadAsync();

        Assert.Equal("Extra ultrasound", vm.Name);
        Assert.All(vm.Suggestions, s => Assert.False(s.IsChosen));
    }

    [Fact]
    public async Task A_chip_she_tapped_that_her_journey_now_has_lets_go_and_her_name_stays()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Suggestions.Single(s => s.Title == "Extra ultrasound").ChooseCommand.Execute(null);
        await _app.Plans.AddEntryAsync(
            TestData.Text("Extra ultrasound", "Επιπλέον υπερηχογράφημα"), EntryKind.Exam, EntryWhen.NotBookedInWeek(26));

        await vm.LoadAsync();
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");
        await vm.SaveCommand.ExecuteAsync(null);

        // Saved as she typed it: in her language only, as no chip names it any more.
        Assert.DoesNotContain(vm.Suggestions, s => s.Title == "Extra ultrasound");
        var added = _app.Repository.Stored!.Checks.Where(c => c.Title.In(LocalizedText.English) == "Extra ultrasound").ToList();
        Assert.Equal(2, added.Count);
        Assert.Contains(added, c => c.Title.In(LocalizedText.Greek) == "Extra ultrasound");
    }

    private async Task<AddEntryViewModel> OpenAsync()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();
        await vm.LoadAsync();
        return vm;
    }
}
