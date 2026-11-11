using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>
/// A task is for a date, or with no date for a trimester or a week, as an entry on her journey can be; "After birth" is
/// a fourth choice beside the trimesters; and an entry she adds can carry a note of her own.
/// </summary>
public sealed class TaskPeriodAndNoteTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    [Fact]
    public void A_task_without_a_date_is_for_a_trimester_or_a_week_and_one_with_neither_is_refused()
    {
        var plan = TestData.Plan();

        var bag = plan.AddTask("Pack the hospital bag", trimester: Trimester.Third);
        var form = plan.AddTask("Register the birth", trimester: Trimester.AfterBirth);
        var classes = plan.AddTask("Book the antenatal class", week: 28);

        Assert.Equal((Trimester.Third, (int?)null), (plan.GetTask(bag).Trimester, plan.GetTask(bag).Week));
        Assert.Equal(Trimester.AfterBirth, plan.GetTask(form).Trimester);
        Assert.Equal(((Trimester?)null, 28), (plan.GetTask(classes).Trimester, plan.GetTask(classes).Week));
        Assert.All(plan.Tasks, t => Assert.False(t.IsScheduled));
        Assert.Equal("task.when.missing", Assert.Throws<DomainException>(() => plan.AddTask("Buy a pillow")).Code);
        Assert.Equal("task.when.missing", Assert.Throws<DomainException>(() => plan.EditTask(bag, "Pack the bag")).Code);
        Assert.Equal(3, plan.Tasks.Count);
    }

    public static TheoryData<DateOnly?, Trimester?, int?> NotOneWhen => new()
    {
        { Day, Trimester.Second, null },
        { Day, null, 30 },
        { null, Trimester.Second, 30 },
        { null, (Trimester)9, null },
        { null, null, -1 },
        { null, null, WeekWindow.MaxWeek + 1 },
    };

    [Theory]
    [MemberData(nameof(NotOneWhen))]
    public void A_task_is_for_one_of_a_date_a_trimester_or_a_week_that_maia_knows(DateOnly? date, Trimester? trimester, int? week)
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Call clinic", week: 30);

        Assert.Equal("task.when.invalid", Assert.Throws<DomainException>(() => plan.AddTask("Call", date, null, trimester, week)).Code);
        Assert.Equal("task.when.invalid", Assert.Throws<DomainException>(() => plan.EditTask(task, "Call", date, null, trimester, week)).Code);
        Assert.Equal(30, plan.GetTask(task).Week);
    }

    [Fact]
    public void Saving_a_task_for_the_same_trimester_changes_nothing()
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Pack the bag", trimester: Trimester.Third);
        var revision = plan.Revision;

        plan.EditTask(task, " Pack the bag ", trimester: Trimester.Third);

        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void A_task_saved_before_tasks_needed_a_when_still_loads_and_keeps_a_valid_one()
    {
        var repairs = new List<string>();

        var legacy = PregnancyPlan.RehydrateTask(TaskId.New(), "Buy a pillow", null, null, isDone: false, repairs);
        var bag = PregnancyPlan.RehydrateTask(TaskId.New(), "Pack the bag", null, null, isDone: false, repairs, Trimester.AfterBirth);
        var classes = PregnancyPlan.RehydrateTask(TaskId.New(), "Book the class", null, null, isDone: false, repairs, week: WeekWindow.MaxWeek);

        Assert.Equal((null, null, null), (legacy.DueDate, legacy.Trimester, legacy.Week));
        Assert.Equal(Trimester.AfterBirth, bag.Trimester);
        Assert.Equal(WeekWindow.MaxWeek, classes.Week);
        Assert.Empty(repairs);
    }

    [Theory]
    [MemberData(nameof(NotOneWhen))]
    public void A_stored_trimester_or_week_that_cannot_stand_is_dropped_and_the_task_kept(DateOnly? date, Trimester? trimester, int? week)
    {
        var repairs = new List<string>();

        var task = PregnancyPlan.RehydrateTask(TaskId.New(), "Call the clinic", date, null, isDone: false, repairs, trimester, week);

        Assert.Equal((date, null, null), (task.DueDate, task.Trimester, task.Week));
        Assert.Equal("task.period_invalid", Assert.Single(repairs));

        // Repaired the same way when no one is noting repairs.
        var unnoted = PregnancyPlan.RehydrateTask(TaskId.New(), "Call the clinic", date, null, isDone: false, trimester: trimester, week: week);
        Assert.Equal((date, null, null), (unnoted.DueDate, unnoted.Trimester, unnoted.Week));
    }

    [Fact]
    public void After_birth_is_the_weeks_after_the_due_date_and_an_entry_there_has_no_week_of_its_own()
    {
        var plan = TestData.Plan();

        var visit = plan.AddEntry(LocalizedText.Plain("Six-week check"), EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.AfterBirth));

        var entry = plan.GetCheck(visit);
        Assert.Equal(new WeekWindow(41, WeekWindow.MaxWeek), WeekWindow.OfTrimester(Trimester.AfterBirth));
        Assert.Equal(new WeekWindow(41, WeekWindow.MaxWeek), entry.Window);
        Assert.Equal(Trimester.AfterBirth, entry.TrimesterOnly);
        Assert.Equal(Trimester.AfterBirth, entry.Trimester);
        Assert.Equal(Trimester.Third, GestationalAge.FromWeeks(41).Trimester);
    }

    [Fact]
    public void An_entry_she_adds_keeps_her_note_and_one_without_has_none()
    {
        var plan = TestData.Plan();

        var scan = plan.AddEntry(LocalizedText.Plain("Extra scan"), EntryKind.Exam, EntryWhen.NotBookedInWeek(30), LocalizedText.Plain("Bring the referral"));
        var visit = plan.AddEntry(LocalizedText.Plain("Visit"), EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(30));

        Assert.Equal("Bring the referral", plan.GetCheck(scan).Description!.In(LocalizedText.Greek));
        Assert.Null(plan.GetCheck(visit).Description);
    }

    [Fact]
    public void A_note_holds_up_to_its_limit()
    {
        var plan = TestData.Plan();
        var visit = plan.AddEntry(LocalizedText.Plain("Visit"), EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(30));
        var longest = LocalizedText.Plain(new string('n', PlannedCheck.MaxNoteLength));
        var tooLong = LocalizedText.Plain(new string('n', PlannedCheck.MaxNoteLength + 1));

        plan.Describe(visit, longest);

        Assert.Equal(longest, plan.GetCheck(visit).Description);
        Assert.Equal("entry.note.too_long", Assert.Throws<DomainException>(() => plan.Describe(visit, tooLong)).Code);
        Assert.Equal("entry.note.too_long", Assert.Throws<DomainException>(() => plan.AddEntry(LocalizedText.Plain("Scan"), EntryKind.Exam, EntryWhen.NotBookedInWeek(30), tooLong)).Code);
    }

    [Fact]
    public void A_new_note_is_saved_and_makes_the_check_hers_the_same_one_changes_nothing_and_none_clears_it()
    {
        var plan = TestData.Plan();
        var anomaly = plan.GetCheck(TestData.Anomaly);
        var note = LocalizedText.Plain("Ask about the heart views");
        var revision = plan.Revision;

        plan.Describe(TestData.Anomaly, note);
        Assert.Equal((note, true, revision + 1), (anomaly.Description, anomaly.IsEdited, plan.Revision));

        plan.Describe(TestData.Anomaly, LocalizedText.Plain("Ask about the heart views"));
        Assert.Equal(revision + 1, plan.Revision);

        plan.Describe(TestData.Anomaly, null);
        Assert.Null(anomaly.Description);
        Assert.Equal(revision + 2, plan.Revision);
        Assert.Empty(plan.DequeueEvents());
    }

    [Fact]
    public void The_due_date_takes_no_note()
    {
        var plan = TestData.Plan();

        Assert.Equal("check.is_milestone", Assert.Throws<DomainException>(() => plan.Describe(TestData.DueDateMilestone, LocalizedText.Plain("Bag ready"))).Code);
    }
}
