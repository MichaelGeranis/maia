using System.Xml.Linq;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Plan;
using Maia.Presentation.Week;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// A task without a date is for a trimester (or after birth) or a week, chosen as an entry's "When?" is, wherever a task
/// is written or changed: Today, New and All tasks. Today is 24+3 (Wed 30 Sep 2026).
/// </summary>
public sealed class TaskTimingTests : IDisposable
{
    private static readonly GestationalAge Age = GestationalAge.FromWeeks(24, 3);
    private static readonly DateTime Today = new(2026, 9, 30);

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public void For_a_task_the_choice_is_only_a_trimester_or_a_week_and_nothing_until_she_chooses()
    {
        var timing = new TimingDraft();

        timing.StartPeriod(Age, Today);

        Assert.Equal(((TimingMode?)null, false, true, true), (timing.Mode, timing.OffersDateTime, timing.OffersPeriod, timing.ShowModes));
        Assert.Equal(((Trimester?)null, (int?)null), timing.ToPeriod());
        timing.ChooseModeCommand.Execute("DateTime");
        Assert.Null(timing.Mode);
    }

    [Fact]
    public void A_task_for_a_trimester_or_a_week_starts_at_it_and_gives_it_back()
    {
        var timing = new TimingDraft();

        timing.StartPeriod(Age, Today, Trimester.AfterBirth);
        Assert.Equal((TimingMode.Trimester, true, false), (timing.Mode, timing.IsAfterBirth, timing.IsThirdTrimester));
        Assert.Equal(((Trimester?)Trimester.AfterBirth, (int?)null), timing.ToPeriod());

        timing.StartPeriod(Age, Today, week: 30);
        Assert.Equal((TimingMode.Week, "Week 30"), (timing.Mode!.Value, timing.WeekText));
        Assert.Equal(((Trimester?)null, (int?)30), timing.ToPeriod());
    }

    [Fact]
    public void After_birth_is_the_fourth_choice_beside_the_trimesters()
    {
        var timing = new TimingDraft();
        timing.StartPeriod(Age, Today);
        timing.ChooseModeCommand.Execute("Trimester");
        Assert.True(timing.IsSecondTrimester);

        timing.ChooseTrimesterCommand.Execute("AfterBirth");

        Assert.Equal((false, false, false, true), (timing.IsFirstTrimester, timing.IsSecondTrimester, timing.IsThirdTrimester, timing.IsAfterBirth));
        Assert.Equal(Trimester.AfterBirth, timing.ToPeriod().Trimester);
        Assert.Equal(EntryWhen.NotBookedIn(Trimester.AfterBirth), timing.ToWhen(done: false));
    }

    [Fact]
    public void Every_trimester_choice_has_after_birth_after_the_third()
    {
        var pills = XDocument.Load(Repo.App("Controls", "TimingFields.xaml")).Descendants()
            .Where(e => AccessibilityTests.Attr(e, "Command") == "{Binding ChooseTrimesterCommand}")
            .Select(e => (AccessibilityTests.Attr(e, "CommandParameter"), AccessibilityTests.Attr(e, "Text")));

        Assert.Equal(
            new (string?, string?)[]
            {
                ("First", "{loc:Translate Section_trimester1}"),
                ("Second", "{loc:Translate Section_trimester2}"),
                ("Third", "{loc:Translate Section_trimester3}"),
                ("AfterBirth", "{loc:Translate Section_after_birth}"),
            },
            pills);
    }

    [Fact]
    public void Every_task_form_asks_when_with_the_timing_fields_while_its_date_is_off()
    {
        var forms = new[]
        {
            (Repo.App("Views", "WeekPage.xaml"), "NewTaskHasDate", "NewTaskPeriod"),
            (Repo.App("Views", "AddEntryPage.xaml"), "TaskHasDate", "TaskPeriod"),
            (Repo.App("Controls", "TaskRowView.xaml"), "EditHasDate", "EditPeriod"),
        };

        foreach (var (file, hasDate, period) in forms)
        {
            var fields = XDocument.Load(file).Descendants()
                .Single(e => e.Name.LocalName == "TimingFields" && AccessibilityTests.Attr(e, "BindingContext") == $"{{Binding {period}}}");

            Assert.Equal($"{{Binding {hasDate}, Converter={{StaticResource InvertedBool}}}}", AccessibilityTests.Attr(fields.Parent!, "IsVisible"));
        }
    }

    [Fact]
    public async Task On_all_tasks_a_task_for_a_trimester_reads_it_and_opens_on_it()
    {
        await _app.CreatePlanAsync();
        var bag = await _app.Plans.AddTaskAsync("Pack the hospital bag", trimester: Trimester.Third);
        var vm = _app.CreateTasks();
        await vm.LoadAsync();
        var row = Row(vm, bag);

        Assert.Equal(("3rd trimester", true), (row.When, row.HasWhen));
        Assert.Equal(((Trimester?)Trimester.Third, (int?)null), (row.Trimester, row.Week));

        row.EditCommand.Execute(null);
        Assert.Equal((false, TimingMode.Trimester, true), (row.EditHasDate, row.EditPeriod.Mode!.Value, row.EditPeriod.IsThirdTrimester));

        // Moved to a week instead: the week starts at hers.
        row.EditPeriod.ChooseModeCommand.Execute("Week");
        Assert.Equal("Week 24", row.EditPeriod.WeekText);
        row.EditPeriod.LaterWeekCommand.Execute(null);
        row.EditPeriod.LaterWeekCommand.Execute(null);
        await row.SaveCommand.ExecuteAsync(null);

        var saved = _app.Repository.Stored!.GetTask(bag);
        Assert.Equal(((Trimester?)null, (int?)26), (saved.Trimester, saved.Week));
        Assert.Equal("Week 26", Row(vm, bag).When);
        Assert.Equal("Task updated.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_task_saved_before_tasks_needed_a_when_reads_none_and_asks_for_one_when_changed()
    {
        await _app.CreatePlanAsync();
        await _app.Repository.SaveAsync(_app.Repository.Stored!.WithTasks([(null, null, false)]), CancellationToken.None);
        var vm = _app.CreateTasks();
        await vm.LoadAsync();
        var row = vm.Rows.OfType<TaskItem>().Single();

        Assert.Equal(((string?)null, false), (row.When, row.HasWhen));
        row.EditCommand.Execute(null);
        Assert.Null(row.EditPeriod.Mode);

        await row.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Choose when it is.", row.EditError);

        row.EditHasDate = true;
        await row.SaveCommand.ExecuteAsync(null);

        Assert.Null(row.EditError);
        Assert.Equal(new DateOnly(2026, 9, 30), _app.Repository.Stored!.Tasks.Single().DueDate);
    }

    [Fact]
    public async Task On_new_a_task_for_a_week_is_saved_for_it()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();
        await vm.LoadAsync();
        vm.ChooseKindCommand.Execute(Add.AddEntryViewModel.TaskChoice);
        vm.TaskTitle = "Book the antenatal class";

        vm.TaskPeriod.ChooseModeCommand.Execute("Week");
        Assert.Equal("Week 24", vm.TaskPeriod.WeekText);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(24, _app.Repository.Stored!.Tasks.Single().Week);
        Assert.Equal("Task added.", _app.Notifier.Shown.Single());
    }

    private static TaskItem Row(TasksViewModel vm, TaskId id) => vm.Rows.OfType<TaskItem>().Single(r => r.Id == id);
}
