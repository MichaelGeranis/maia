using System.Xml.Linq;
using Maia.Domain.Plans;
using Maia.Presentation.Add;
using Maia.Presentation.Plan;

namespace Maia.Presentation.Tests;

/// <summary>
/// Her blood pressure: logged on New (the sixth pill) as her monitor shows it, at the day and time she took it, and
/// shown small on Journey at that time, where sliding it aside deletes it. Today is 24+3 (Wed 30 Sep 2026), 12:00.
/// </summary>
public sealed class BloodPressureTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    // ---- New --------------------------------------------------------------------------------

    [Fact]
    public async Task The_blood_pressure_pill_starts_at_now_and_saving_logs_it_and_starts_afresh()
    {
        var vm = await OpenAddAsync();

        vm.ChooseKindCommand.Execute(AddEntryViewModel.BloodPressureChoice);
        Assert.Equal((true, false, false), (vm.IsBloodPressure, vm.IsTask, vm.HasKind));
        Assert.Equal((new DateTime(2026, 9, 30), new TimeSpan(12, 0, 0)), (vm.ReadingDate, vm.ReadingTime));
        Assert.False(vm.IsComplete);

        vm.Systolic = "118";
        Assert.False(vm.IsComplete);
        vm.Diastolic = " 76 ";
        Assert.True(vm.IsComplete);
        vm.ReadingTime = new TimeSpan(8, 30, 0);
        await vm.SaveCommand.ExecuteAsync(null);

        var reading = Assert.Single(_app.Repository.Stored!.BloodPressure);
        Assert.Equal((118, 76, new DateOnly(2026, 9, 30), new TimeOnly(8, 30)), (reading.Systolic, reading.Diastolic, reading.Date, reading.Time));
        Assert.Equal("Blood pressure 118/76 mmHg added to your journey.", _app.Notifier.Shown.Single());
        Assert.Empty(_app.Navigator.Visited);
        Assert.Equal((false, string.Empty, string.Empty, (string?)null), (vm.IsBloodPressure, vm.Systolic, vm.Diastolic, vm.ErrorMessage));
    }

    [Fact]
    public async Task A_day_can_be_chosen_from_the_first_of_the_pregnancy_up_to_today()
    {
        var vm = await OpenAddAsync();

        Assert.Equal(new DateTime(2026, 4, 12), vm.EarliestReadingDate);
        Assert.Equal(new DateTime(2026, 9, 30), vm.LatestReadingDate);
    }

    [Theory]
    [InlineData("", "76", "Write both numbers.")]
    [InlineData("118", " ", "Write both numbers.")]
    [InlineData("1l8", "76", "Check the two numbers as your monitor shows them.")]
    [InlineData("-90", "60", "Check the two numbers as your monitor shows them.")]
    [InlineData("76", "118", "Check the two numbers as your monitor shows them.")]
    [InlineData("400", "76", "Check the two numbers as your monitor shows them.")]
    public async Task Numbers_missing_or_mistyped_are_not_saved_and_she_is_told_calmly(string systolic, string diastolic, string message)
    {
        var vm = await OpenAddAsync();
        var saves = _app.Repository.SaveCount;
        vm.ChooseKindCommand.Execute(AddEntryViewModel.BloodPressureChoice);

        vm.Systolic = systolic;
        vm.Diastolic = diastolic;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.ErrorMessage);
        Assert.Equal(saves, _app.Repository.SaveCount);
        Assert.Equal((true, systolic), (vm.IsBloodPressure, vm.Systolic));
    }

    [Fact]
    public async Task A_time_still_to_come_is_not_saved()
    {
        var vm = await OpenAddAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.BloodPressureChoice);
        vm.Systolic = "118";
        vm.Diastolic = "76";

        vm.ReadingTime = new TimeSpan(12, 1, 0);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Choose a day and time that has passed.", vm.ErrorMessage);
        Assert.Empty(_app.Repository.Stored!.BloodPressure);
    }

    [Fact]
    public async Task A_save_that_fails_keeps_what_she_typed_and_lets_her_try_again()
    {
        var vm = await OpenAddAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.BloodPressureChoice);
        vm.Systolic = "118";
        vm.Diastolic = "76";
        _app.Repository.FailNextSave = new IOException("Disk full");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", vm.ErrorMessage);
        Assert.Equal(("118", "76", true), (vm.Systolic, vm.Diastolic, vm.SaveCommand.CanExecute(null)));
        Assert.Empty(_app.Notifier.Shown);
        Assert.Equal("add.log_reading", _app.Errors.Reports.Single().Operation);
    }

    [Fact]
    public async Task Tapping_the_pill_again_keeps_her_numbers_and_another_pill_leaves_blood_pressure()
    {
        var vm = await OpenAddAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.BloodPressureChoice);
        vm.Systolic = "118";
        vm.ReadingTime = new TimeSpan(7, 0, 0);

        vm.ChooseKindCommand.Execute(AddEntryViewModel.BloodPressureChoice);
        Assert.Equal(("118", new TimeSpan(7, 0, 0)), (vm.Systolic, vm.ReadingTime));

        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);
        Assert.Equal((false, true), (vm.IsBloodPressure, vm.IsTask));

        vm.ChooseKindCommand.Execute(AddEntryViewModel.BloodPressureChoice);
        vm.ChooseKindCommand.Execute("Exam");
        Assert.Equal((false, false, EntryKind.Exam), (vm.IsBloodPressure, vm.IsTask, vm.Kind!.Value));
    }

    [Fact]
    public void The_blood_pressure_pill_comes_after_task_with_two_number_fields_and_a_day_up_to_today()
    {
        var page = XDocument.Load(Repo.App("Views", "AddEntryPage.xaml"));
        var pills = page.Descendants().Where(e => AccessibilityTests.Attr(e, "Command") == "{Binding ChooseKindCommand}").ToList();

        Assert.Equal("{loc:Translate Kind_BloodPressure}", AccessibilityTests.Attr(pills[^1], "Text"));
        Assert.Equal("{x:Static vm:AddEntryViewModel.BloodPressureChoice}", AccessibilityTests.Attr(pills[^1], "CommandParameter"));
        var numbers = page.Descendants().Where(e => e.Name.LocalName == "Entry" && AccessibilityTests.Attr(e, "Keyboard") == "Numeric").ToList();
        Assert.Equal(["{Binding Systolic}", "{Binding Diastolic}"], numbers.Select(e => AccessibilityTests.Attr(e, "Text")));
        Assert.Contains(page.Descendants(), e => e.Name.LocalName == "DatePicker" && AccessibilityTests.Attr(e, "MaximumDate") == "{Binding LatestReadingDate}");
    }

    // ---- Journey ------------------------------------------------------------------------------

    [Fact]
    public async Task A_reading_stands_small_at_the_day_and_time_she_took_it_tasks_shown_or_not()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LogBloodPressureAsync(118, 76, new DateOnly(2026, 9, 30), new TimeOnly(8, 30));
        await _app.Plans.LogBloodPressureAsync(124, 80, new DateOnly(2026, 10, 14), new TimeOnly(19, 5));
        var vm = NewJourney();
        vm.ShowTasks = false;

        await vm.LoadAsync();

        var readings = vm.Rows.OfType<ReadingRow>().ToList();
        Assert.Equal(2, readings.Count);
        Assert.Equal(("118/76 mmHg", "Wed 30 Sep · 08:30"), (readings[0].Value, readings[0].When));
        Assert.Equal("Blood pressure 118 over 76, Wed 30 Sep · 08:30", readings[0].SpokenName);

        // Taken earlier today, it is before "You are here"; one in week 26 is first under that week's heading.
        Assert.Equal(vm.Rows.IndexOf(readings[0]) + 1, vm.Rows.IndexOf(vm.Rows.OfType<HereRow>().Single()));
        Assert.Equal("week:26", vm.Rows[vm.Rows.IndexOf(readings[1]) - 1].Key);
        Assert.Equal("reading:" + readings[1].Id, readings[1].Key);
    }

    [Fact]
    public async Task Sliding_a_reading_aside_and_tapping_delete_deletes_it_for_good()
    {
        await _app.CreatePlanAsync();
        var id = await _app.Plans.LogBloodPressureAsync(118, 76, new DateOnly(2026, 9, 29), new TimeOnly(8, 30));
        var vm = NewJourney();
        await vm.LoadAsync();
        var row = vm.Rows.OfType<ReadingRow>().Single();

        await row.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_app.Repository.Stored!.BloodPressure);
        Assert.DoesNotContain(vm.Rows, r => r.Key == "reading:" + id);
        Assert.Equal("Blood pressure 118/76 mmHg deleted from your journey.", _app.Notifier.Shown.Single());
        Assert.False(row.IsBusy);
    }

    [Fact]
    public async Task A_reading_already_gone_says_so_calmly_and_a_second_tap_while_deleting_does_nothing()
    {
        await _app.CreatePlanAsync();
        var id = await _app.Plans.LogBloodPressureAsync(118, 76, new DateOnly(2026, 9, 29), new TimeOnly(8, 30));
        var vm = NewJourney();
        await vm.LoadAsync();
        var row = vm.Rows.OfType<ReadingRow>().Single();
        await _app.Plans.RemoveBloodPressureAsync(id);
        var saves = _app.Repository.SaveCount;
        var slow = new SlowStep();
        _app.Repository.BeforeLoad = slow.WaitAsync;

        var first = row.DeleteCommand.ExecuteAsync(null);
        await slow.Started;
        Assert.True(row.IsBusy);
        await row.DeleteCommand.ExecuteAsync(null);
        _app.Repository.BeforeLoad = null;
        slow.Finish();
        await first;

        Assert.Equal("This reading isn't on your journey any more.", _app.Notifier.Notices.Single());
        Assert.Equal(saves, _app.Repository.SaveCount);
        Assert.False(row.IsBusy);
    }

    [Fact]
    public void A_reading_on_journey_slides_left_or_right_to_show_delete_and_has_no_card()
    {
        var page = XDocument.Load(Repo.App("Views", "PlanPage.xaml"));
        var template = page.Descendants().Single(e => e.Name.LocalName == "DataTemplate" && AccessibilityTests.Attr(e, "Key") == "ReadingTemplate");
        var swipe = Assert.Single(template.Elements(), e => e.Name.LocalName == "SwipeView");

        foreach (var side in new[] { "SwipeView.LeftItems", "SwipeView.RightItems" })
        {
            var item = swipe.Elements().Single(e => e.Name.LocalName == side).Descendants().Single(e => e.Name.LocalName == "SwipeItemView");
            Assert.Equal("{Binding DeleteCommand}", AccessibilityTests.Attr(item, "Command"));
        }

        Assert.DoesNotContain(template.Descendants(), e => AccessibilityTests.Attr(e, "StrokeShape") == "RoundRectangle 22");
        Assert.Contains(template.Descendants(), e => AccessibilityTests.Attr(e, "SemanticProperties.Hint") == "{loc:Translate Reading_SwipeHint}");
    }

    private PlanViewModel NewJourney() =>
        new(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);

    private async Task<AddEntryViewModel> OpenAddAsync()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();
        await vm.LoadAsync();
        return vm;
    }
}
