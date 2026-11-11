using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>
/// A stored plan is rebuilt as it was saved. One record that breaks a rule the code relies on is repaired the safe way
/// and noted, so it never makes the whole plan unreadable, and a limit tightened later never locks her out of text she
/// saved before.
/// </summary>
public sealed class RehydrateTests
{
    private static readonly CareContact Doctor = new(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");

    private static PlannedCheck Check(
        CheckId id,
        CheckState state,
        Appointment? appointment = null,
        Trimester? trimesterOnly = null,
        CheckCategory category = CheckCategory.Routine,
        WeekWindow? window = null) =>
        PregnancyPlan.RehydrateCheck(
            id,
            CheckOrigin.Template,
            TestData.Text("Glucose test", "Καμπύλη σακχάρου"),
            description: null,
            category,
            window ?? new WeekWindow(24, 28),
            state,
            appointment,
            trimesterOnly);

    private static PregnancyPlan Rehydrate(
        IEnumerable<PlannedCheck>? checks = null,
        IEnumerable<PlanTask>? tasks = null,
        IEnumerable<DoctorQuestion>? questions = null,
        IEnumerable<ResultAttachment>? untagged = null,
        ICollection<string>? repairs = null) =>
        PregnancyPlan.Rehydrate(
            PlanId.New(),
            PregnancyDating.FromDueDate(TestData.DueDate),
            new TemplateReference("test-gr", 1),
            LocalizedText.English,
            DateTimeOffset.UnixEpoch,
            checks ?? [],
            tasks ?? [],
            new CareTeam([Doctor]),
            revision: 7,
            questions,
            untagged,
            repairs);

    [Fact]
    public void A_plan_that_keeps_every_rule_loads_as_it_was_saved()
    {
        var booked = new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30), "Clinic", Doctor.Id);
        var bookedWeek = WeekWindow.OfWeek(26);
        var untagged = ResultAttachment.ForFile(AttachmentId.New(), "lab.pdf", "application/pdf", 2048, "0123456789abcdef0123456789abcdef", DateTimeOffset.UnixEpoch);
        var repairs = new List<string>();

        var plan = Rehydrate(
            [
                Check(TestData.Glucose, CheckState.Booked, booked, window: bookedWeek),
                Check(TestData.Anomaly, CheckState.Done, trimesterOnly: Trimester.Second, window: WeekWindow.OfTrimester(Trimester.Second)),
            ],
            untagged: [untagged],
            repairs: repairs);

        Assert.Empty(repairs);
        Assert.Equal(7, plan.Revision);
        Assert.Equal(booked, plan.GetCheck(TestData.Glucose).Appointment);
        Assert.Equal(bookedWeek, plan.GetCheck(TestData.Glucose).Window);
        Assert.Equal((CheckState.Done, (Trimester?)Trimester.Second), (plan.GetCheck(TestData.Anomaly).State, plan.GetCheck(TestData.Anomaly).TrimesterOnly));
        Assert.Equal(new[] { TestData.Anomaly, TestData.Glucose }, plan.Checks.Select(c => c.Id));
        Assert.Equal(untagged, Assert.Single(plan.Results));
    }

    [Fact]
    public void A_stored_booking_without_its_appointment_goes_back_to_needing_a_booking()
    {
        var repairs = new List<string>();

        var plan = Rehydrate([Check(TestData.Glucose, CheckState.Booked)], repairs: repairs);

        Assert.Equal(CheckState.NotBooked, plan.GetCheck(TestData.Glucose).State);
        Assert.Null(plan.NextAppointment(TestData.Today));
        Assert.Equal(new[] { "check.booked_without_appointment" }, repairs);
    }

    [Fact]
    public void A_stored_milestone_is_never_booked_or_done()
    {
        var repairs = new List<string>();

        var plan = Rehydrate(
            [
                Check(TestData.DueDateMilestone, CheckState.Booked, new Appointment(new DateOnly(2027, 1, 17)), category: CheckCategory.Milestone),
                Check(TestData.Glucose, CheckState.Done, trimesterOnly: Trimester.Third, category: CheckCategory.Milestone),
            ],
            repairs: repairs);

        Assert.All(plan.Checks, c =>
        {
            Assert.Equal(CheckState.NotBooked, c.State);
            Assert.Null(c.Appointment);
            Assert.Null(c.TrimesterOnly);
        });
        Assert.Equal(new[] { "check.milestone_state", "check.milestone_state" }, repairs);
    }

    [Fact]
    public void A_stored_milestone_not_booked_still_drops_a_stray_date_or_trimester()
    {
        var repairs = new List<string>();

        var plan = Rehydrate(
            [
                Check(TestData.DueDateMilestone, CheckState.NotBooked, new Appointment(new DateOnly(2027, 1, 17)), category: CheckCategory.Milestone),
                Check(TestData.Glucose, CheckState.NotBooked, trimesterOnly: Trimester.Third, category: CheckCategory.Milestone),
                Check(TestData.Anomaly, CheckState.NotBooked, category: CheckCategory.Milestone),
            ],
            repairs: repairs);

        Assert.All(plan.Checks, c =>
        {
            Assert.Equal(CheckState.NotBooked, c.State);
            Assert.Null(c.Appointment);
            Assert.Null(c.TrimesterOnly);
        });
        Assert.Equal(new[] { "check.milestone_state", "check.milestone_state" }, repairs);
    }

    [Fact]
    public void An_appointment_with_someone_no_longer_in_the_care_team_keeps_its_date_and_forgets_the_contact()
    {
        var stranger = CareContactId.New();
        var slot = new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30), "Clinic", stranger);
        var repairs = new List<string>();

        var plan = Rehydrate([Check(TestData.Glucose, CheckState.Booked, slot)], repairs: repairs);

        var expected = new Appointment(slot.Date, slot.Time, slot.Location, with: null);
        Assert.Equal(expected, plan.GetCheck(TestData.Glucose).Appointment);
        Assert.Equal(CheckState.Booked, plan.GetCheck(TestData.Glucose).State);
        Assert.Equal(new[] { "appointment.contact_unknown" }, repairs);
    }

    [Fact]
    public void A_booking_saved_at_its_usual_weeks_stands_in_the_week_of_its_day_in_timeline_order()
    {
        // 5 Jan 2027 is week 38; a plan saved before a booking set the week kept the check's usual weeks (24-28).
        var january5 = new Appointment(new DateOnly(2027, 1, 5), new TimeOnly(9, 30));
        var repairs = new List<string>();

        var plan = Rehydrate(
            [
                Check(TestData.Glucose, CheckState.Booked, january5),
                Check(TestData.Growth, CheckState.NotBooked, window: new WeekWindow(32, 34)),
            ],
            repairs: repairs);

        Assert.Equal(WeekWindow.OfWeek(38), plan.GetCheck(TestData.Glucose).Window);
        Assert.Equal(new[] { TestData.Growth, TestData.Glucose }, plan.Checks.Select(c => c.Id));
        Assert.Equal(january5, plan.GetCheck(TestData.Glucose).Appointment);
        Assert.Empty(repairs);
    }

    [Fact]
    public void A_stored_appointment_on_a_check_that_is_not_booked_is_dropped()
    {
        var repairs = new List<string>();

        var plan = Rehydrate([Check(TestData.Glucose, CheckState.Done, new Appointment(new DateOnly(2026, 10, 12)))], repairs: repairs);

        Assert.Equal(CheckState.Done, plan.GetCheck(TestData.Glucose).State);
        Assert.Null(plan.GetCheck(TestData.Glucose).Appointment);
        Assert.Null(plan.NextAppointment(TestData.Today));
        Assert.Equal(new[] { "check.appointment_without_booking" }, repairs);
    }

    [Fact]
    public void A_stored_booking_with_a_trimester_keeps_its_booking_and_forgets_the_trimester()
    {
        var slot = new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));
        var repairs = new List<string>();

        var plan = Rehydrate([Check(TestData.Glucose, CheckState.Booked, slot, Trimester.Third, window: WeekWindow.OfWeek(26))], repairs: repairs);

        var glucose = plan.GetCheck(TestData.Glucose);
        Assert.Equal((CheckState.Booked, slot, (Trimester?)null), (glucose.State, glucose.Appointment, glucose.TrimesterOnly));
        Assert.Equal(Trimester.Second, glucose.Trimester);
        Assert.Equal(new[] { "check.booked_with_trimester" }, repairs);
    }

    [Fact]
    public void A_stored_task_with_a_time_but_no_date_keeps_the_task_and_drops_the_time()
    {
        var repairs = new List<string>();

        var task = PregnancyPlan.RehydrateTask(TaskId.New(), "Call the clinic", dueDate: null, new TimeOnly(9, 0), isDone: false, repairs);

        Assert.Equal("Call the clinic", task.Title);
        Assert.Null(task.DueTime);
        Assert.False(task.IsScheduled);
        Assert.Equal(new[] { "task.time_without_date" }, repairs);
    }

    [Fact]
    public void Repairs_happen_even_when_nobody_asks_what_was_repaired()
    {
        var task = PregnancyPlan.RehydrateTask(TaskId.New(), "Call the clinic", dueDate: null, new TimeOnly(9, 0), isDone: false);
        var plan = PregnancyPlan.Rehydrate(
            PlanId.New(),
            PregnancyDating.FromDueDate(TestData.DueDate),
            new TemplateReference("test-gr", 1),
            LocalizedText.English,
            DateTimeOffset.UnixEpoch,
            [],
            [task],
            new CareTeam([Doctor]),
            revision: 7,
            timeZoneId: " Europe/Athens");

        Assert.Null(task.DueTime);
        Assert.Null(plan.TimeZoneId);
    }

    [Fact]
    public void A_stored_task_with_its_day_and_time_or_no_time_loads_as_saved()
    {
        var repairs = new List<string>();
        var day = new DateOnly(2026, 10, 1);

        var timed = PregnancyPlan.RehydrateTask(TaskId.New(), "Call the clinic", day, new TimeOnly(9, 0), isDone: false, repairs);
        var untimed = PregnancyPlan.RehydrateTask(TaskId.New(), "Buy a pillow", null, null, isDone: false, repairs);
        var plan = PregnancyPlan.Rehydrate(
            PlanId.New(),
            PregnancyDating.FromDueDate(TestData.DueDate),
            new TemplateReference("test-gr", 1),
            LocalizedText.English,
            DateTimeOffset.UnixEpoch,
            [],
            [timed, untimed],
            new CareTeam([Doctor]),
            revision: 7,
            repairs: repairs,
            timeZoneId: "Europe/Athens");

        Assert.Equal((day, new TimeOnly(9, 0)), (timed.DueDate, timed.DueTime));
        Assert.Null(untimed.DueDate);
        Assert.Equal("Europe/Athens", plan.TimeZoneId);
        Assert.Empty(repairs);
    }

    [Fact]
    public void Text_saved_under_a_longer_limit_still_loads()
    {
        // Limits are for what she types now. If one is ever tightened, what she saved before must still open.
        var longTitle = new string('t', PlanTask.MaxTitleLength + 20);
        var longQuestion = new string('q', DoctorQuestion.MaxTextLength + 20);

        var plan = Rehydrate(
            tasks: [PregnancyPlan.RehydrateTask(TaskId.New(), longTitle, null, null, isDone: false)],
            questions: [PregnancyPlan.RehydrateQuestion(QuestionId.New(), longQuestion, isAnswered: false)]);

        Assert.Equal(longTitle, Assert.Single(plan.Tasks).Title);
        Assert.Equal(longQuestion, Assert.Single(plan.Questions).Text);
    }

    [Fact]
    public void What_she_types_now_still_keeps_to_the_limits()
    {
        var plan = TestData.Plan();

        Assert.Equal("task.title.too_long", Assert.Throws<DomainException>(() => plan.AddTask(new string('t', PlanTask.MaxTitleLength + 1))).Code);
        Assert.Equal("question.text.too_long", Assert.Throws<DomainException>(() => plan.AddQuestion(new string('q', DoctorQuestion.MaxTextLength + 1))).Code);
    }
}
