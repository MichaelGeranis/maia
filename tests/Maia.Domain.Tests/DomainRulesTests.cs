using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>The rules around tasks, questions, results, limits, ids, templates and dating that the plan relies on.</summary>
public sealed class DomainRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Removing_a_timed_task_unschedules_it_and_an_untimed_one_just_goes()
    {
        var plan = TestData.Plan();
        var timed = plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var untimed = plan.AddTask("Buy a pillow", week: 30);
        plan.DequeueEvents();

        plan.RemoveTask(timed);
        Assert.Equal(new TaskUnscheduled(timed), Assert.Single(plan.DequeueEvents()));

        var revision = plan.Revision;
        plan.RemoveTask(untimed);
        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision + 1, plan.Revision);
        Assert.Empty(plan.Tasks);
    }

    [Fact]
    public void Unticking_a_timed_task_schedules_it_again()
    {
        var plan = TestData.Plan();
        var timed = plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        plan.SetTaskDone(timed, done: true);
        plan.DequeueEvents();

        plan.SetTaskDone(timed, done: false);

        Assert.Equal(new TaskScheduled(timed), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void Setting_a_task_or_question_to_what_it_already_is_changes_nothing()
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Buy a pillow", week: 30);
        var question = plan.AddQuestion("Is swimming fine?");
        plan.DequeueEvents();
        var revision = plan.Revision;

        plan.SetTaskDone(task, done: false);
        plan.SetQuestionAnswered(question, answered: false);

        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void Moving_the_due_date_to_the_same_day_changes_nothing()
    {
        var plan = TestData.Plan();

        plan.ChangeDueDate(PregnancyDating.FromDueDate(TestData.DueDate));

        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(0, plan.Revision);
    }

    [Fact]
    public void A_plan_holds_at_most_its_limit_of_checks_tasks_and_questions()
    {
        var plan = TestData.Plan();
        while (plan.Checks.Count < PregnancyPlan.MaxChecks)
        {
            plan.AddEntry(LocalizedText.Plain("Extra"), EntryKind.Exam, EntryWhen.NotBookedInWeek(25));
        }

        for (var i = 0; i < PregnancyPlan.MaxTasks; i++)
        {
            plan.AddTask("Task", week: 30);
        }

        for (var i = 0; i < PregnancyPlan.MaxQuestions; i++)
        {
            plan.AddQuestion("Question?");
        }

        Assert.Equal("plan.checks.full", Assert.Throws<DomainException>(() => plan.AddEntry(LocalizedText.Plain("One more"), EntryKind.Exam, EntryWhen.NotBookedInWeek(25))).Code);
        Assert.Equal("plan.tasks.full", Assert.Throws<DomainException>(() => plan.AddTask("One more", week: 30)).Code);
        Assert.Equal("plan.questions.full", Assert.Throws<DomainException>(() => plan.AddQuestion("One more?")).Code);
    }

    [Fact]
    public void A_role_holds_at_most_its_limit_of_contacts()
    {
        var plan = TestData.Plan();
        for (var i = 0; i < CareTeam.MaxContactsPerRole; i++)
        {
            plan.LinkContact(new CareContact(CareContactId.New(), CareRole.ObGyn, "Clinic", $"+30 690 000 00{i:00}"));
        }

        var ex = Assert.Throws<DomainException>(() => plan.LinkContact(new CareContact(CareContactId.New(), CareRole.ObGyn, "One more", "+30 690 000 0099")));
        Assert.Equal("care_team.role_full", ex.Code);
    }

    [Fact]
    public void Unlinking_someone_who_is_not_linked_is_refused()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.Plan().UnlinkContact(CareContactId.New()));

        Assert.Equal("care_team.contact_not_found", ex.Code);
    }

    [Fact]
    public void Results_are_kept_once_and_removed_once()
    {
        var plan = TestData.Plan();
        var report = ResultAttachment.ForFile(AttachmentId.New(), "report.pdf", "application/pdf", 1000, "blob-1", Now);
        plan.AttachResult(report);
        plan.RemoveResult(report.Id);
        plan.AttachResult(report);

        Assert.Equal("attachment.duplicate", Assert.Throws<DomainException>(() => plan.AttachResult(report)).Code);
        plan.RemoveResult(report.Id);
        Assert.Equal("attachment.not_found", Assert.Throws<DomainException>(() => plan.RemoveResult(report.Id)).Code);
    }

    [Theory]
    [InlineData("ogtt")]
    [InlineData("check-up_25")]
    public void Check_ids_are_short_letters_digits_and_dashes(string value) =>
        Assert.Equal(value, new CheckId(value).Value);

    [Theory]
    [InlineData("ogtt test")]
    [InlineData("ογκος")]
    [InlineData("a/b")]
    public void Other_check_ids_are_refused(string value) =>
        Assert.Equal("check.id.invalid", Assert.Throws<DomainException>(() => new CheckId(value)).Code);

    [Fact]
    public void Check_ids_are_at_most_64_characters_and_custom_ones_carry_their_prefix()
    {
        Assert.Equal(64, new CheckId(new string('a', 64)).Value.Length);
        Assert.Throws<DomainException>(() => new CheckId(new string('a', 65)));
        Assert.True(CheckId.NewCustom().IsCustom);
        Assert.False(TestData.Glucose.IsCustom);
    }

    [Fact]
    public void Templates_keep_the_custom_prefix_free_and_need_distinct_checks()
    {
        var check = new CheckTemplate(new CheckId("a"), LocalizedText.Plain("A"), LocalizedText.Plain("A"), CheckCategory.Routine, new WeekWindow(10, 12), true);

        Assert.Equal("template.check.id_reserved", Assert.Throws<DomainException>(() =>
            new CheckTemplate(CheckId.NewCustom(), LocalizedText.Plain("A"), LocalizedText.Plain("A"), CheckCategory.Routine, new WeekWindow(10, 12), true)).Code);
        Assert.Equal("template.empty", Assert.Throws<DomainException>(() =>
            new PlanTemplate("t", "GR", 1, ReviewStatus.Draft, [])).Code);
        Assert.Equal("template.check.duplicate", Assert.Throws<DomainException>(() =>
            new PlanTemplate("t", "GR", 1, ReviewStatus.Draft, [check, check])).Code);
    }

    [Fact]
    public void Dating_accepts_a_pregnancy_from_its_first_day_up_to_44_weeks()
    {
        var today = TestData.Today;
        var lastPeriodToday = today.AddDays(PregnancyDating.TermDays);
        var at44 = today.AddDays(PregnancyDating.TermDays - PregnancyDating.MaxSupported.TotalDays);

        Assert.Equal(lastPeriodToday, PregnancyDating.ForToday(lastPeriodToday, today).DueDate);
        Assert.Equal(at44, PregnancyDating.ForToday(at44, today).DueDate);
        Assert.Equal("dating.out_of_range", Assert.Throws<DomainException>(() => PregnancyDating.ForToday(at44.AddDays(-1), today)).Code);
        Assert.Equal("dating.out_of_range", Assert.Throws<DomainException>(() => PregnancyDating.ForToday(lastPeriodToday.AddDays(1), today)).Code);
    }

    [Fact]
    public void Lists_are_read_only_views_not_the_plans_own_lists()
    {
        var plan = TestData.Plan();
        plan.AddTask("Buy a pillow", week: 30);
        plan.AddQuestion("Is swimming fine?");
        plan.LinkContact(new CareContact(CareContactId.New(), CareRole.ObGyn, "Clinic", "+30 210 000 2210"));
        plan.AttachResult(ResultAttachment.ForFile(AttachmentId.New(), "r.pdf", "application/pdf", 10, "blob", Now));

        AssertReadOnly(plan.Checks);
        AssertReadOnly(plan.Tasks);
        AssertReadOnly(plan.Questions);
        AssertReadOnly(plan.PendingEvents);
        AssertReadOnly(plan.CareTeam.Contacts);
        AssertReadOnly(plan.Results);
        AssertReadOnly(TestData.Template().Checks);
    }

    [Fact]
    public void Error_messages_name_no_check_date_or_week()
    {
        var plan = TestData.Plan();
        var messages = new[]
        {
            Assert.Throws<DomainException>(() => plan.GetCheck(new CheckId("ogtt-late"))).Message,
            Assert.Throws<DomainException>(() => plan.Book(TestData.DueDateMilestone, new Appointment(new DateOnly(2026, 10, 12)))).Message,
            Assert.Throws<DomainException>(() => plan.Delete(TestData.DueDateMilestone)).Message,
            Assert.Throws<DomainException>(() => plan.Rename(TestData.Glucose, LocalizedText.Plain(new string('a', PlannedCheck.MaxTitleLength + 1)))).Message,
            Assert.Throws<DomainException>(() => EntryWhen.DoneInWeek(WeekWindow.MaxWeek + 1)).Message,
            Assert.Throws<DomainException>(() => new CheckId("ogtt late")).Message,
            Assert.Throws<DomainException>(() => new WeekWindow(28, 24)).Message,
            Assert.Throws<DomainException>(() => PregnancyDating.ForToday(new DateOnly(2027, 6, 1), new DateOnly(2026, 8, 1))).Message,
        };

        foreach (var message in messages)
        {
            Assert.DoesNotContain("ogtt", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("nipt", message, StringComparison.OrdinalIgnoreCase);
            Assert.False(message.Any(char.IsAsciiDigit), message);
        }
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> list)
    {
        Assert.IsNotType<List<T>>(list);
        var asList = Assert.IsAssignableFrom<IList<T>>(list);
        Assert.True(asList.IsReadOnly);
    }
}
