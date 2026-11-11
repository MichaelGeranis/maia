using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Testing;

namespace Maia.Application.Tests;

/// <summary>Reading her plan: the care team, one snapshot for Add, her home time zone, and what the ports do by default.</summary>
public sealed class PlanReadTests
{
    private static readonly CareContact Doctor = new(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");
    private static readonly CareContact Midwife = new(CareContactId.New(), CareRole.Midwife, "Anna", "+30 690 000 1234");

    private readonly FakeClock _clock = new(TestData.Today);
    private readonly InMemoryPlanRepository _plans = new();

    private PlanQueries Queries => new(_plans, _clock);

    [Fact]
    public async Task Without_a_plan_her_care_team_is_empty()
    {
        Assert.Empty(await Queries.GetCareTeamAsync(CareRole.ObGyn));
        Assert.Empty(await Queries.GetCareContactsAsync());
    }

    [Fact]
    public async Task Her_care_team_reads_by_role_and_in_full()
    {
        var plan = TestData.Plan();
        plan.LinkContact(Doctor);
        plan.LinkContact(Midwife);
        await _plans.SaveAsync(plan, CancellationToken.None);

        Assert.Equal(Doctor.Id, Assert.Single(await Queries.GetCareTeamAsync(CareRole.ObGyn)).Id);
        Assert.Equal(new[] { Doctor.Id, Midwife.Id }, (await Queries.GetCareContactsAsync()).Select(c => c.Id));
    }

    [Fact]
    public async Task A_snapshot_reads_in_the_plan_s_language_and_builds_its_timeline_once()
    {
        var plan = TestData.Plan();
        plan.ChangeLanguage(LocalizedText.Greek);
        await _plans.SaveAsync(plan, CancellationToken.None);

        var snapshot = (await Queries.GetSnapshotAsync())!;

        Assert.Equal(LocalizedText.Greek, snapshot.Language);
        Assert.Same(snapshot.Timeline, snapshot.Timeline);
        Assert.Contains(snapshot.Timeline.Sections.SelectMany(s => s.Checks), v => v.Title == "Καμπύλη σακχάρου");
    }

    [Fact]
    public void Next_up_is_only_ever_a_booked_entry()
    {
        var plan = TestData.Plan();
        var unbooked = new CheckView(plan.GetCheck(TestData.Glucose), CheckStatus.NotBooked, "Glucose test");

        var week = new WeekOverview(GestationalAge.FromWeeks(24, 3), Trimester.Second, TestData.DueDate, unbooked, [], []);

        Assert.Null(week.NextUp);
        Assert.Null((week with { NextAppointment = null }).NextUp);
    }

    [Fact]
    public void A_home_time_zone_this_phone_does_not_know_falls_back_to_the_phone_s()
    {
        var plan = TestData.Plan();
        plan.SetHomeTimeZoneIfUnknown("Mars/Olympus_Mons");

        Assert.Null(HomeTimeZone.Find("Mars/Olympus_Mons", _clock));
        Assert.Same(_clock.TimeZone, HomeTimeZone.Of(plan, _clock));
        Assert.Same(_clock.TimeZone, HomeTimeZone.Of(null, _clock));
        Assert.Null(HomeTimeZone.Find(null, _clock));
    }

    [Fact]
    public void A_home_time_zone_the_phone_knows_is_found_by_its_id()
    {
        var plan = TestData.Plan();
        plan.SetHomeTimeZoneIfUnknown("Europe/London");

        Assert.Equal("Europe/London", HomeTimeZone.Of(plan, _clock).Id);
    }

    [Fact]
    public void A_zone_known_by_its_Windows_name_is_kept_by_its_IANA_id()
    {
        var windows = TimeZoneInfo.CreateCustomTimeZone("GTB Standard Time", TimeSpan.FromHours(2), "Athens", "Athens");
        var unknown = TimeZoneInfo.CreateCustomTimeZone("Maia Test Time", TimeSpan.FromHours(2), "Test", "Test");

        Assert.Equal("Europe/Bucharest", HomeTimeZone.IdOf(windows));
        Assert.Equal("Maia Test Time", HomeTimeZone.IdOf(unknown));
        Assert.Equal("Europe/Athens", HomeTimeZone.IdOf(FakeClock.Athens));
    }

    [Fact]
    public void A_clock_works_out_today_in_its_own_zone()
    {
        IClock clock = new BareClock(new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero), FakeClock.Athens);

        Assert.Equal(new DateOnly(2026, 10, 1), clock.Today);
        Assert.Equal(new DateTime(2026, 10, 1, 1, 30, 0), clock.Now);
    }

    [Fact]
    public void A_reporter_that_keeps_nothing_has_nothing_to_forget()
    {
        var reporter = new ForwardingReporter();
        IErrorReporter errors = reporter;
        errors.Report(new IOException("disk"), "plan.save");

        errors.Forget();

        Assert.Equal("plan.save", Assert.Single(reporter.Operations));
    }

    [Fact]
    public void A_booking_or_task_already_past_gets_no_reminder()
    {
        var plan = TestData.Plan();
        var yesterday = TestData.Today.AddDays(-1);
        plan.Book(TestData.Glucose, new Appointment(yesterday, new TimeOnly(9, 0)));
        plan.Book(TestData.Checkup25, new Appointment(TestData.Today.AddDays(3), new TimeOnly(9, 0)));
        var late = plan.AddTask("Call the clinic", yesterday, new TimeOnly(9, 0));
        var soon = plan.AddTask("Pack the bag", TestData.Today.AddDays(3), new TimeOnly(9, 0));
        var now = new DateTimeOffset(TestData.Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(3));

        var keys = ReminderPlanner.For(plan, new ReminderSettings { When = ReminderPreference.Both }, now, FakeClock.Athens).Select(r => r.Key).ToList();

        Assert.DoesNotContain(keys, k => k.StartsWith(ReminderTimes.KeyFor(plan.GetCheck(TestData.Glucose)), StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.StartsWith(ReminderTimes.KeyFor(plan.GetTask(late)), StringComparison.Ordinal));
        Assert.Contains(keys, k => k.StartsWith(ReminderTimes.KeyFor(plan.GetCheck(TestData.Checkup25)), StringComparison.Ordinal));
        Assert.Contains(keys, k => k.StartsWith(ReminderTimes.KeyFor(plan.GetTask(soon)), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1\t0\ta\tb")]
    [InlineData("x\t0\ta\tb\tc")]
    [InlineData("1\tsoon\ta\tb\tc")]
    [InlineData("1\t-62135596800001\ta\tb\tc")]
    [InlineData("1\t253402300800000\ta\tb\tc")]
    public void A_kept_reminder_line_that_cannot_be_read_is_left_out(string line)
    {
        var good = KeptReminders.Write([new KeptReminder(7, DateTimeOffset.UnixEpoch, "Appointment", "Tomorrow", "Maia")]);

        var read = KeptReminders.Read(line + "\n" + good);

        Assert.Equal(7, Assert.Single(read).Number);
    }

    [Fact]
    public void A_kept_reminder_at_the_edges_of_time_still_reads()
    {
        var read = KeptReminders.Read("1\t-62135596800000\ta\tb\tc\n2\t253402300799999\ta\tb\tc");

        Assert.Equal(new[] { DateTimeOffset.MinValue, DateTimeOffset.MaxValue.AddTicks(-9999) }, read.Select(r => r.At));
    }

    /// <summary>A clock that only knows the instant and its zone, as a minimal adapter would.</summary>
    private sealed class BareClock(DateTimeOffset utcNow, TimeZoneInfo zone) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;

        public TimeZoneInfo TimeZone => zone;
    }

    /// <summary>A reporter that passes failures on and keeps nothing on the phone.</summary>
    private sealed class ForwardingReporter : IErrorReporter
    {
        public List<string> Operations { get; } = [];

        public void Report(Exception exception, string operation) => Operations.Add(operation);
    }
}
