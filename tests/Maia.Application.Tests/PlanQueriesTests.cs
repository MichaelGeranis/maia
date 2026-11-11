using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Testing;

namespace Maia.Application.Tests;

public sealed class PlanQueriesTests
{
    [Fact]
    public void Week_overview_summarises_what_needs_attention()
    {
        var plan = TestData.Plan();
        plan.Reschedule(TestData.Anomaly, EntryWhen.DoneInWeek(20));
        plan.Book(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        plan.AddTask("Later task", week: 30);
        plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));

        var week = PlanQueries.BuildWeek(plan, TestData.Today);

        Assert.Equal(GestationalAge.FromWeeks(24, 3), week.Age);
        Assert.Equal(Trimester.Second, week.Trimester);
        Assert.Equal(TestData.Checkup25, week.NextAppointment?.Check.Id);
        Assert.Equal("Call clinic", week.OpenTasks[0].Title);
    }

    [Fact]
    public void A_booked_entry_shows_done_on_the_timeline_once_its_time_has_passed()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(TestData.Today, new TimeOnly(10, 30)));

        CheckStatus StatusAt(TimeOnly now) =>
            PlanQueries.BuildTimeline(plan, TestData.Today, now).SelectMany(s => s.Checks).Single(v => v.Check.Id == TestData.Glucose).Status;

        Assert.Equal(CheckStatus.Booked, StatusAt(new TimeOnly(10, 29)));
        Assert.Equal(CheckStatus.Done, StatusAt(new TimeOnly(10, 30)));
        Assert.Equal(CheckState.Booked, plan.GetCheck(TestData.Glucose).State);
    }

    [Fact]
    public async Task The_timeline_reads_the_time_of_day_from_the_clock()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(TestData.Today, new TimeOnly(10, 30)));
        var repository = new InMemoryPlanRepository();
        await repository.SaveAsync(plan, CancellationToken.None);
        var clock = new FakeClock(TestData.Today);
        var queries = new PlanQueries(repository, clock);

        clock.SetTime(TestData.Today, new TimeOnly(9, 0));
        var before = (await queries.GetTimelineAsync())!.Sections.SelectMany(s => s.Checks).Single(v => v.Check.Id == TestData.Glucose);
        clock.SetTime(TestData.Today, new TimeOnly(11, 0));
        var after = (await queries.GetTimelineAsync())!.Sections.SelectMany(s => s.Checks).Single(v => v.Check.Id == TestData.Glucose);

        Assert.Equal((CheckStatus.Booked, CheckStatus.Done), (before.Status, after.Status));
    }

    [Fact]
    public void A_setup_check_whose_weeks_have_passed_reads_probably_done_but_one_she_added_does_not()
    {
        var plan = TestData.Plan();
        var added = plan.AddEntry(LocalizedText.Plain("Dentist"), EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(10));

        var statuses = PlanQueries.BuildTimeline(plan, TestData.Today, new TimeOnly(12, 0))
            .SelectMany(s => s.Checks)
            .ToDictionary(v => v.Check.Id, v => v.Status);

        Assert.Equal(CheckStatus.ProbablyDone, statuses[TestData.Booking]);
        Assert.Equal(CheckStatus.NotBooked, statuses[added]);
        Assert.Equal(CheckStatus.Milestone, statuses[TestData.DueDateMilestone]);
    }

    [Fact]
    public async Task The_timeline_brings_her_obgyn_contacts_from_the_same_load()
    {
        var plan = TestData.Plan();
        plan.LinkContact(new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 210 000 0000"));
        var repository = new InMemoryPlanRepository();
        await repository.SaveAsync(plan, CancellationToken.None);
        var queries = new PlanQueries(repository, new FakeClock(TestData.Today));

        var timeline = await queries.GetTimelineAsync();

        Assert.Equal("Dr. Eleni Papadaki", Assert.Single(timeline!.ObGyns).DisplayName);
        Assert.Equal(1, repository.LoadCount);
    }

    [Fact]
    public async Task Start_up_learns_the_plan_language_or_that_there_is_no_plan()
    {
        var repository = new InMemoryPlanRepository();
        var queries = new PlanQueries(repository, new FakeClock(TestData.Today));

        Assert.Null(await queries.GetLanguageAsync());

        await repository.SaveAsync(TestData.Plan(), CancellationToken.None);

        Assert.Equal(TestData.Plan().Language, await queries.GetLanguageAsync());
    }

    [Fact]
    public void Timeline_groups_her_entries_by_trimester()
    {
        var sections = PlanQueries.BuildTimeline(TestData.Plan(), TestData.Today, new TimeOnly(12, 0));

        Assert.Equal(new[] { "trimester1", "trimester2", "trimester3" }, sections.Select(s => s.Key));
        Assert.Equal(new[] { TestData.Anomaly, TestData.Glucose, TestData.Checkup25 }, sections[1].Checks.Select(c => c.Check.Id));
    }

    [Fact]
    public async Task The_results_screen_gets_every_result_from_one_read_of_her_plan()
    {
        var repository = new InMemoryPlanRepository();
        var plan = TestData.Plan();
        var lab = ResultAttachment.ForFile(Maia.Domain.Common.AttachmentId.New(), "lab.pdf", "application/pdf", 2048, "0123456789abcdef0123456789abcdef", DateTimeOffset.UnixEpoch);
        plan.AttachResult(lab);
        await repository.SaveAsync(plan, CancellationToken.None);
        var queries = new PlanQueries(repository, new FakeClock(TestData.Today));

        var overview = await queries.GetResultsOverviewAsync();

        Assert.Equal(lab, Assert.Single(overview!.Results).Result);
        Assert.Null(await new PlanQueries(new InMemoryPlanRepository(), new FakeClock(TestData.Today)).GetResultsOverviewAsync());
    }

    // ---- Next up and results for the screens to come (change request #5, #10) ---------------

    [Fact]
    public void Next_up_is_the_next_booked_entry_of_any_kind()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        var visit = plan.AddEntry(LocalizedText.Plain("Dentist"), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 5), new TimeOnly(17, 0))));
        plan.AddTask("Pack the bag", new DateOnly(2026, 10, 9), new TimeOnly(9, 0));

        var next = PlanQueries.BuildWeek(plan, TestData.Today).NextUp;

        Assert.Equal((new DateOnly(2026, 10, 5), (TimeOnly?)new TimeOnly(17, 0), visit), (next!.Date, next.Time, next.Entry!.Check.Id));
        Assert.Equal("Dentist", next.Entry.Title);
    }

    [Fact]
    public void Next_up_is_never_a_task_even_a_timed_one_that_comes_first()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        plan.AddTask("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));

        var next = PlanQueries.BuildWeek(plan, TestData.Today).NextUp;

        Assert.Equal(TestData.Glucose, next!.Entry.Check.Id);
    }

    [Fact]
    public void Next_up_is_nothing_with_only_tasks()
    {
        var plan = TestData.Plan();
        plan.AddTask("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));

        Assert.Null(PlanQueries.BuildWeek(plan, TestData.Today).NextUp);
    }

    [Fact]
    public void Tasks_fall_in_one_group_each_for_the_day()
    {
        var plan = TestData.Plan();
        var earlier = plan.AddTask("Send the form", TestData.Today.AddDays(-1));
        var today = plan.AddTask("Call the clinic", TestData.Today, new TimeOnly(9, 0));
        var sixDaysOn = plan.AddTask("Buy a pillow", TestData.Today.AddDays(6), new TimeOnly(23, 59));
        var later = plan.AddTask("Order the pram", TestData.Today.AddDays(7));
        var noDate = plan.AddTask("Pack the hospital bag", trimester: Trimester.Third);
        var done = plan.AddTask("Book the class", TestData.Today.AddDays(-3));
        plan.SetTaskDone(done, done: true);

        var groups = plan.Tasks.ToDictionary(t => t.Id, t => TaskGroups.Of(t, TestData.Today));

        Assert.Equal(TaskGroup.Earlier, groups[earlier]);
        Assert.Equal(TaskGroup.ThisWeek, groups[today]);
        Assert.Equal(TaskGroup.ThisWeek, groups[sixDaysOn]);
        Assert.Equal(TaskGroup.Later, groups[later]);
        Assert.Equal(TaskGroup.NoDate, groups[noDate]);
        Assert.Equal(TaskGroup.Done, groups[done]);
    }

    [Fact]
    public async Task Your_tasks_reads_every_task_ticked_or_not_in_list_order_from_one_load()
    {
        // One saved before tasks needed a trimester or a week comes last.
        var plan = TestData.Plan().WithTasks([(null, null, false)]);
        var legacy = plan.Tasks.Single().Id;
        var classes = plan.AddTask("Book the antenatal class", week: 30);
        var bag = plan.AddTask("Pack the hospital bag", trimester: Trimester.Third);
        var form28 = plan.AddTask("Fill in the birth form", week: 28);
        var pram = plan.AddTask("Order the pram", new DateOnly(2026, 11, 20));
        var call = plan.AddTask("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var morning = plan.AddTask("Take the iron tablet", new DateOnly(2026, 10, 1), new TimeOnly(8, 0));
        var form = plan.AddTask("Send the form", new DateOnly(2026, 9, 28));
        plan.SetTaskDone(form, done: true);
        var repository = new InMemoryPlanRepository();
        await repository.SaveAsync(plan, CancellationToken.None);

        var tasks = await new PlanQueries(repository, new FakeClock(TestData.Today)).GetTasksAsync();

        // Undated: by the week they are for, one for a trimester first in its first week (the third starts at 28).
        Assert.Equal(new[] { form, morning, call, pram, bag, form28, classes, legacy }, tasks!.Tasks.Select(t => t.Id));
        Assert.Equal(TestData.Today, tasks.Today);
        Assert.Equal(GestationalAge.FromWeeks(24, 3), tasks.Age);
        Assert.Equal(1, repository.LoadCount);
        Assert.Null(await new PlanQueries(new InMemoryPlanRepository(), new FakeClock(TestData.Today)).GetTasksAsync());
    }

    [Fact]
    public void The_week_counts_the_tasks_she_ticked_off()
    {
        var plan = TestData.Plan();
        plan.AddTask("Call the clinic", week: 30);
        plan.SetTaskDone(plan.AddTask("Send the form", week: 30), done: true);

        var week = PlanQueries.BuildWeek(plan, TestData.Today);

        Assert.Equal(1, week.DoneTasks);
        Assert.Equal("Call the clinic", Assert.Single(week.OpenTasks).Title);
    }

    [Fact]
    public void Next_up_puts_a_booking_before_a_task_at_the_same_time_and_is_null_with_nothing_ahead()
    {
        var plan = TestData.Plan();
        Assert.Null(PlanQueries.BuildWeek(plan, TestData.Today).NextUp);

        var slot = new Appointment(new DateOnly(2026, 10, 2), new TimeOnly(9, 0));
        plan.Book(TestData.Glucose, slot);
        plan.AddTask("Call the clinic", slot.Date, slot.Time);

        Assert.Equal(TestData.Glucose, PlanQueries.BuildWeek(plan, TestData.Today).NextUp!.Entry!.Check.Id);
    }

    [Fact]
    public async Task Results_are_one_list_newest_first()
    {
        var plan = TestData.Plan();
        var scan = ResultAttachment.ForFile(AttachmentId.New(), "scan.jpg", "image/jpeg", 2048, "0123456789abcdef0123456789abcdef", new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));
        var lab = ResultAttachment.ForFile(AttachmentId.New(), "lab.png", "image/png", 1024, "00112233445566778899aabbccddeeff", new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero));
        var report = ResultAttachment.ForFile(AttachmentId.New(), "report.pdf", "application/pdf", 4096, "fedcba9876543210fedcba9876543210", new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        plan.AttachResult(scan);
        plan.AttachResult(lab);
        plan.AttachResult(report);
        var repository = new InMemoryPlanRepository();
        await repository.SaveAsync(plan, CancellationToken.None);

        var results = await new PlanQueries(repository, new FakeClock(TestData.Today)).GetResultsAsync();

        Assert.Equal(
            new (AttachmentId, AttachmentKind)[]
            {
                (lab.Id, AttachmentKind.Photo),
                (report.Id, AttachmentKind.Document),
                (scan.Id, AttachmentKind.Photo),
            },
            results!.Select(r => (r.Result.Id, r.Kind)));
        Assert.Null(await new PlanQueries(new InMemoryPlanRepository(), new FakeClock(TestData.Today)).GetResultsAsync());
    }

    [Fact]
    public void A_result_alone_makes_a_plan_worth_backing_up()
    {
        var plan = TestData.Plan();
        var aWeekOn = TestData.Today.AddDays(PlanQueries.WorthKeepingAfterDays);
        Assert.False(PlanQueries.IsWorthKeeping(plan, aWeekOn));

        plan.AttachResult(ResultAttachment.ForFile(AttachmentId.New(), "lab.pdf", "application/pdf", 2048, "0123456789abcdef0123456789abcdef", DateTimeOffset.UnixEpoch));

        Assert.True(PlanQueries.IsWorthKeeping(plan, aWeekOn));
    }

    [Fact]
    public void A_blood_pressure_reading_alone_makes_a_plan_worth_backing_up()
    {
        var plan = TestData.Plan();
        var aWeekOn = TestData.Today.AddDays(PlanQueries.WorthKeepingAfterDays);

        plan.LogBloodPressure(118, 76, TestData.Today, new TimeOnly(8, 0));

        Assert.True(PlanQueries.IsWorthKeeping(plan, aWeekOn));
    }
}
