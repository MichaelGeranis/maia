using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>Edits that change only what she changed: her tasks, her results' names and who a booking is with.</summary>
public sealed class PlanEditTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);
    private static readonly TimeOnly Nine = new(9, 0);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Saving_a_task_as_it_is_changes_nothing(bool hasDate, bool hasTime)
    {
        var plan = TestData.Plan();
        DateOnly? date = hasDate ? Day : null;
        TimeOnly? time = hasTime ? Nine : null;
        int? week = hasDate ? null : 30;
        var task = plan.AddTask("Call clinic", date, time, week: week);
        plan.DequeueEvents();
        var revision = plan.Revision;

        plan.EditTask(task, " Call clinic ", date, time, week: week);

        Assert.Equal(revision, plan.Revision);
        Assert.Empty(plan.DequeueEvents());
    }

    public static TheoryData<string, DateOnly?, TimeOnly?, Trimester?, int?> ChangedTasks => new()
    {
        { "Call the clinic", Day, Nine, null, null },
        { "Call clinic", null, null, null, 30 },
        { "Call clinic", null, null, Trimester.Third, null },
        { "Call clinic", null, null, Trimester.AfterBirth, null },
        { "Call clinic", Day.AddDays(1), Nine, null, null },
        { "Call clinic", Day, null, null, null },
        { "Call clinic", Day, Nine.AddHours(1), null, null },
    };

    [Theory]
    [MemberData(nameof(ChangedTasks))]
    public void Changing_a_task_s_words_day_time_trimester_or_week_saves_it(string title, DateOnly? date, TimeOnly? time, Trimester? trimester, int? week)
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Call clinic", Day, Nine);
        var revision = plan.Revision;

        plan.EditTask(task, title, date, time, trimester, week);

        Assert.Equal(revision + 1, plan.Revision);
        var edited = plan.GetTask(task);
        Assert.Equal((title, date, time, trimester, week), (edited.Title, edited.DueDate, edited.DueTime, edited.Trimester, edited.Week));
    }

    [Theory]
    [InlineData(null, 31)]
    [InlineData(Trimester.Third, null)]
    public void A_task_for_a_week_moves_to_another_week_or_a_trimester(Trimester? trimester, int? week)
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Pack the bag", week: 30);
        var revision = plan.Revision;

        plan.EditTask(task, "Pack the bag", trimester: trimester, week: week);

        Assert.Equal(revision + 1, plan.Revision);
        Assert.Equal((trimester, week), (plan.GetTask(task).Trimester, plan.GetTask(task).Week));
    }

    [Fact]
    public void Giving_an_untimed_task_a_day_saves_it_without_a_reminder()
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Buy a pillow", week: 30);
        plan.DequeueEvents();

        plan.EditTask(task, "Buy a pillow", Day);

        Assert.Equal(Day, plan.GetTask(task).DueDate);
        Assert.Empty(plan.DequeueEvents());
    }

    [Fact]
    public void Ticking_an_untimed_task_off_and_on_sets_no_reminder()
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Buy a pillow", week: 30);
        plan.DequeueEvents();
        var revision = plan.Revision;

        plan.SetTaskDone(task, done: true);
        plan.SetTaskDone(task, done: false);

        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision + 2, plan.Revision);
        Assert.False(plan.GetTask(task).IsDone);
    }

    [Fact]
    public void A_task_that_is_not_in_the_plan_is_not_found()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.Plan().GetTask(TaskId.New()));

        Assert.Equal("task.not_found", ex.Code);
    }

    [Fact]
    public void A_result_s_name_is_kept_to_its_limit()
    {
        var plan = TestData.Plan();
        var tooLong = new string('a', ResultAttachment.MaxNameLength + 20);
        var result = ResultAttachment.ForFile(AttachmentId.New(), tooLong, "application/pdf", 1024, new string('0', 32), DateTimeOffset.UnixEpoch);
        plan.AttachResult(result);

        Assert.Equal(ResultAttachment.MaxNameLength, result.DisplayName.Length);
        Assert.True(plan.RenameResult(result.Id, new string('b', ResultAttachment.MaxNameLength + 1)));
        Assert.Equal(new string('b', ResultAttachment.MaxNameLength), plan.FindResult(result.Id).DisplayName);
    }

    [Fact]
    public void Unlinking_a_contact_takes_it_off_only_the_bookings_with_that_contact()
    {
        var plan = TestData.Plan();
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");
        var midwife = new CareContact(CareContactId.New(), CareRole.Midwife, "Anna", "+30 690 000 1234");
        plan.LinkContact(doctor);
        plan.LinkContact(midwife);
        var withDoctor = new Appointment(Day, Nine, "Clinic", doctor.Id);
        var withMidwife = new Appointment(Day.AddDays(1), Nine, "Home", midwife.Id);
        var withNobody = new Appointment(Day.AddDays(2), Nine, "Lab");
        plan.Book(TestData.Glucose, withDoctor);
        plan.Book(TestData.Anomaly, withMidwife);
        plan.Book(TestData.Checkup25, withNobody);

        plan.UnlinkContact(doctor.Id);

        Assert.Equal(new Appointment(Day, Nine, "Clinic"), plan.GetCheck(TestData.Glucose).Appointment);
        Assert.Equal(withMidwife, plan.GetCheck(TestData.Anomaly).Appointment);
        Assert.Equal(withNobody, plan.GetCheck(TestData.Checkup25).Appointment);
        Assert.Null(plan.GetCheck(TestData.Booking).Appointment);
    }
}
