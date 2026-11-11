using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Domain.Tests;

public sealed class PlanCreationTests
{
    [Fact]
    public void Keeps_only_the_checks_she_kept_and_leaves_switched_off_ones_out()
    {
        var plan = TestData.Plan();

        Assert.Equal(
            new[] { TestData.Booking, TestData.Anomaly, TestData.Glucose, TestData.Checkup25, TestData.DueDateMilestone },
            plan.Checks.Select(c => c.Id));
        Assert.All(plan.Checks, c => Assert.Equal(CheckState.NotBooked, c.State));
        Assert.All(plan.Checks, c => Assert.Equal(CheckOrigin.Template, c.Origin));
        Assert.Equal("check.not_found", Assert.Throws<DomainException>(() => plan.GetCheck(TestData.Nipt)).Code);
    }

    [Fact]
    public void Applies_setup_overrides_but_never_leaves_out_milestones()
    {
        var plan = TestData.Plan(new Dictionary<CheckId, bool>
        {
            [TestData.Nipt] = true,
            [TestData.Anomaly] = false,
            [TestData.DueDateMilestone] = false,
        });

        Assert.Equal(CheckState.NotBooked, plan.GetCheck(TestData.Nipt).State);
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Anomaly);
        Assert.True(plan.GetCheck(TestData.DueDateMilestone).IsMilestone);
    }

    [Fact]
    public void Rejects_overrides_for_unknown_checks()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.Plan(new Dictionary<CheckId, bool> { [new CheckId("nope")] = true }));
        Assert.Equal("plan.override.unknown_check", ex.Code);
    }

    [Fact]
    public void A_plan_made_today_leaves_out_checks_whose_weeks_have_passed_but_keeps_the_due_date()
    {
        var plan = PregnancyPlan.Create(
            PlanId.New(), PregnancyDating.FromDueDate(TestData.DueDate), TestData.Template(), "en", DateTimeOffset.UnixEpoch, today: TestData.Today);

        // 24+3: the booking visit (8-10) is behind her; the anomaly scan (20-24) runs to the end of this week.
        Assert.Equal(new[] { TestData.Anomaly, TestData.Glucose, TestData.Checkup25, TestData.DueDateMilestone }, plan.Checks.Select(c => c.Id));
    }

    [Fact]
    public void Keeps_checks_in_timeline_order_and_records_its_template()
    {
        var plan = TestData.Plan();

        Assert.Equal(plan.Checks.OrderBy(c => c.Window.FromWeek).Select(c => c.Id), plan.Checks.Select(c => c.Id));
        Assert.Equal(new TemplateReference("test-gr", 1), plan.Template);
        Assert.Empty(plan.PendingEvents);
    }
}

public sealed class CheckStatusTests
{
    private static readonly PregnancyDating Dating = PregnancyDating.FromDueDate(TestData.DueDate);
    private static readonly TimeOnly Noon = new(12, 0);

    [Theory]
    [InlineData(20, 0, CheckStatus.NotBooked)]  // before its weeks
    [InlineData(26, 3, CheckStatus.NotBooked)]  // inside its weeks
    [InlineData(28, 6, CheckStatus.NotBooked)]  // last day of its weeks
    [InlineData(29, 0, CheckStatus.ProbablyDone)]
    public void A_check_she_kept_at_setup_reads_as_probably_done_once_its_weeks_pass_unbooked(int weeks, int days, CheckStatus expected)
    {
        var check = TestData.Plan().GetCheck(TestData.Glucose);
        var age = GestationalAge.FromWeeks(weeks, days);

        Assert.Equal(expected, check.StatusAt(age, Dating.DateAt(age), Noon));
    }

    [Theory]
    [InlineData(29)]
    [InlineData(40)]
    public void An_entry_she_added_is_never_probably_done_however_late(int weeks)
    {
        var plan = TestData.Plan();
        var id = plan.AddEntry(LocalizedText.Plain("Iron recheck"), EntryKind.Exam, EntryWhen.NotBookedInWeek(25));
        var age = GestationalAge.FromWeeks(weeks);

        Assert.Equal(CheckStatus.NotBooked, plan.GetCheck(id).StatusAt(age, Dating.DateAt(age), Noon));
    }

    [Theory]
    [InlineData(-1, 23, 59, CheckStatus.Done)]   // yesterday
    [InlineData(0, 8, 59, CheckStatus.Booked)]   // today, a minute before
    [InlineData(0, 9, 0, CheckStatus.Done)]      // today, at its time
    [InlineData(0, 9, 1, CheckStatus.Done)]      // today, after its time
    [InlineData(1, 0, 0, CheckStatus.Booked)]    // tomorrow
    public void A_booking_is_done_once_its_time_has_passed(int daysFromToday, int hour, int minute, CheckStatus expected)
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(TestData.Today.AddDays(daysFromToday), new TimeOnly(9, 0)));

        Assert.Equal(expected, plan.StatusOf(plan.GetCheck(TestData.Glucose), TestData.Today, new TimeOnly(hour, minute)));
    }

    [Fact]
    public void A_booking_with_only_a_day_is_done_once_that_day_has_passed()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(TestData.Today));
        var check = plan.GetCheck(TestData.Glucose);

        Assert.Equal(CheckStatus.Booked, plan.StatusOf(check, TestData.Today, new TimeOnly(23, 59)));
        Assert.Equal(CheckStatus.Done, plan.StatusOf(check, TestData.Today.AddDays(1), TimeOnly.MinValue));
    }

    [Fact]
    public void What_she_said_wins_over_the_weeks()
    {
        var plan = TestData.Plan();
        var later = new DateOnly(2026, 12, 1);
        plan.Book(TestData.Glucose, new Appointment(later, new TimeOnly(8, 30)));
        plan.Reschedule(TestData.Anomaly, EntryWhen.DoneInWeek(21));
        var late = GestationalAge.FromWeeks(31);

        Assert.Equal(CheckStatus.Booked, plan.GetCheck(TestData.Glucose).StatusAt(late, later.AddDays(-1), Noon));
        Assert.Equal(CheckStatus.Done, plan.GetCheck(TestData.Anomaly).StatusAt(GestationalAge.FromWeeks(10), TestData.Today, Noon));
        Assert.Equal(CheckStatus.Milestone, plan.GetCheck(TestData.DueDateMilestone).StatusAt(GestationalAge.FromWeeks(42), TestData.Today, Noon));
    }

    [Fact]
    public void A_template_check_moved_to_a_week_she_gave_is_probably_done_once_that_week_passes()
    {
        var plan = TestData.Plan();
        plan.Reschedule(TestData.Glucose, EntryWhen.NotBookedInWeek(30));
        var check = plan.GetCheck(TestData.Glucose);

        Assert.Equal(CheckStatus.NotBooked, check.StatusAt(GestationalAge.FromWeeks(30, 6), TestData.Today, Noon));
        Assert.Equal(CheckStatus.ProbablyDone, check.StatusAt(GestationalAge.FromWeeks(31), TestData.Today, Noon));
    }
}

public sealed class BookingTests
{
    private static readonly Appointment OctoberSlot = new(new DateOnly(2026, 10, 12), new TimeOnly(8, 30), "Central Lab");

    [Fact]
    public void Booking_records_the_appointment_and_raises_an_event()
    {
        var plan = TestData.Plan();

        plan.Book(TestData.Glucose, OctoberSlot);

        var check = plan.GetCheck(TestData.Glucose);
        Assert.Equal(CheckState.Booked, check.State);
        Assert.Equal(OctoberSlot, check.Appointment);
        Assert.Equal(new CheckBooked(TestData.Glucose, OctoberSlot), Assert.Single(plan.DequeueEvents()));
        Assert.Equal(1, plan.Revision);
    }

    [Fact]
    public void A_booked_check_stands_in_the_week_of_its_day_whatever_its_origin()
    {
        var plan = TestData.Plan();

        // 12 Oct 2026 is 26+1 of a pregnancy due on 17 Jan 2027.
        plan.Book(TestData.Glucose, OctoberSlot);

        var check = plan.GetCheck(TestData.Glucose);
        Assert.Equal(WeekWindow.OfWeek(26), check.Window);
        Assert.Equal(26, check.Week);
        Assert.Equal(Trimester.Second, check.Trimester);
        Assert.Equal(plan.Checks.OrderBy(c => c.Window.FromWeek).Select(c => c.Id), plan.Checks.Select(c => c.Id));
    }

    [Fact]
    public void Booking_a_day_that_has_passed_records_it_as_done()
    {
        var plan = TestData.Plan();
        var past = new Appointment(new DateOnly(2026, 9, 1), new TimeOnly(10, 0));

        plan.Book(TestData.Anomaly, past);

        var check = plan.GetCheck(TestData.Anomaly);
        Assert.Equal(CheckState.Booked, check.State);
        Assert.Equal(CheckStatus.Done, plan.StatusOf(check, TestData.Today, TimeOnly.MinValue));
        Assert.Equal(new CheckBooked(TestData.Anomaly, past), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void Cannot_book_a_milestone_or_a_check_she_does_not_have()
    {
        var plan = TestData.Plan();

        Assert.Equal("check.is_milestone", Assert.Throws<DomainException>(() => plan.Book(TestData.DueDateMilestone, OctoberSlot)).Code);
        Assert.Equal("check.not_found", Assert.Throws<DomainException>(() => plan.Book(TestData.Nipt, OctoberSlot)).Code);
        Assert.Empty(plan.DequeueEvents());
    }

    [Fact]
    public void Cannot_book_with_a_contact_outside_the_care_team()
    {
        var plan = TestData.Plan();
        var slot = new Appointment(OctoberSlot.Date, OctoberSlot.Time, with: CareContactId.New());

        Assert.Equal("appointment.contact_unknown", Assert.Throws<DomainException>(() => plan.Book(TestData.Glucose, slot)).Code);
        Assert.Equal(CheckState.NotBooked, plan.GetCheck(TestData.Glucose).State);
    }

    [Fact]
    public void Saving_the_same_booking_again_changes_nothing()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, OctoberSlot);
        plan.DequeueEvents();
        var revision = plan.Revision;

        plan.Book(TestData.Glucose, new Appointment(OctoberSlot.Date, OctoberSlot.Time, OctoberSlot.Location));

        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void Moving_a_booking_to_a_trimester_clears_it_and_stands_it_first_in_that_trimester()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, OctoberSlot);
        plan.DequeueEvents();

        plan.Reschedule(TestData.Glucose, EntryWhen.NotBookedIn(Trimester.Second));

        var check = plan.GetCheck(TestData.Glucose);
        Assert.Equal((CheckState.NotBooked, (Appointment?)null, (Trimester?)Trimester.Second), (check.State, check.Appointment, check.TrimesterOnly));
        Assert.Equal(WeekWindow.OfTrimester(Trimester.Second), check.Window);
        Assert.Equal((14, Trimester.Second), (check.Week, check.Trimester));
        Assert.Equal(new CheckBookingCleared(TestData.Glucose), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void Booking_an_entry_she_gave_a_trimester_forgets_the_trimester()
    {
        var plan = TestData.Plan();
        plan.Reschedule(TestData.Glucose, EntryWhen.DoneIn(Trimester.Third));

        plan.Book(TestData.Glucose, OctoberSlot);

        var check = plan.GetCheck(TestData.Glucose);
        Assert.Null(check.TrimesterOnly);
        Assert.Equal(WeekWindow.OfWeek(26), check.Window);
        Assert.Equal(Trimester.Second, check.Trimester);
    }

    [Fact]
    public void Done_in_a_week_stands_in_that_week_and_needs_no_reminder()
    {
        var plan = TestData.Plan();
        var revision = plan.Revision;

        plan.Reschedule(TestData.Glucose, EntryWhen.DoneInWeek(30));

        var check = plan.GetCheck(TestData.Glucose);
        Assert.Equal(CheckState.Done, check.State);
        Assert.Equal(WeekWindow.OfWeek(30), check.Window);
        Assert.Equal(Trimester.Third, check.Trimester);
        Assert.Empty(plan.DequeueEvents());
        Assert.True(plan.Revision > revision);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(WeekWindow.MaxWeek + 1)]
    public void A_week_outside_a_pregnancy_is_refused(int week)
    {
        Assert.Equal("entry.week.invalid", Assert.Throws<DomainException>(() => EntryWhen.NotBookedInWeek(week)).Code);
        Assert.Equal("entry.week.invalid", Assert.Throws<DomainException>(() => EntryWhen.DoneInWeek(week)).Code);
    }

    [Fact]
    public void A_trimester_that_is_not_one_is_refused()
    {
        Assert.Equal("entry.trimester.invalid", Assert.Throws<DomainException>(() => EntryWhen.NotBookedIn((Trimester)0)).Code);
        Assert.Equal("entry.trimester.invalid", Assert.Throws<DomainException>(() => EntryWhen.DoneIn((Trimester)5)).Code);
    }

    [Fact]
    public void Next_appointment_is_the_earliest_upcoming_booking()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, OctoberSlot);
        plan.Book(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        plan.Book(TestData.Anomaly, new Appointment(new DateOnly(2026, 9, 1)));

        Assert.Equal(TestData.Checkup25, plan.NextAppointment(TestData.Today)?.Id);
    }

    [Fact]
    public void A_booking_earlier_today_is_no_longer_next_from_its_time_as_it_turns_done_but_one_with_only_a_day_stays_all_day()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(TestData.Today, new TimeOnly(9, 0)));
        plan.Book(TestData.Checkup25, new Appointment(TestData.Today.AddDays(1), new TimeOnly(10, 30)));

        Assert.Equal(TestData.Glucose, plan.NextAppointment(TestData.Today, new TimeOnly(8, 59))?.Id);
        Assert.Equal(TestData.Checkup25, plan.NextAppointment(TestData.Today, new TimeOnly(9, 0))?.Id);

        plan.Book(TestData.Anomaly, new Appointment(TestData.Today));
        Assert.Equal(TestData.Anomaly, plan.NextAppointment(TestData.Today, new TimeOnly(23, 59))?.Id);
    }

    [Fact]
    public void Changing_the_due_date_moves_every_week_based_date()
    {
        var plan = TestData.Plan();

        plan.ChangeDueDate(PregnancyDating.FromDueDate(TestData.DueDate.AddDays(7)));

        Assert.Equal(GestationalAge.FromWeeks(23, 3), plan.AgeOn(TestData.Today));
        Assert.IsType<DueDateChanged>(Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void Changing_the_language_is_an_event_only_when_it_changes()
    {
        var plan = TestData.Plan();
        var language = plan.Language;

        plan.ChangeLanguage(language.ToUpperInvariant());
        Assert.Empty(plan.DequeueEvents());

        var other = language == LocalizedText.Greek ? LocalizedText.English : LocalizedText.Greek;
        plan.ChangeLanguage(other);

        Assert.Equal(other, plan.Language);
        Assert.Equal(other, Assert.IsType<PlanLanguageChanged>(Assert.Single(plan.DequeueEvents())).Language);
    }
}

/// <summary>Entries on her journey: what she adds, renames, moves and deletes, and where each one stands.</summary>
public sealed class JourneyEntryTests
{
    /// <summary>20 Oct 2026: week 27 of a pregnancy due on 17 Jan 2027.</summary>
    private static readonly Appointment October20 = new(new DateOnly(2026, 10, 20), new TimeOnly(11, 0));

    /// <summary>5 Jan 2027: week 38.</summary>
    private static readonly Appointment January5 = new(new DateOnly(2027, 1, 5), new TimeOnly(9, 30));

    [Fact]
    public void A_doctor_visit_she_books_is_a_booking_like_any_other_and_forgets_an_unlinked_doctor()
    {
        var plan = TestData.Plan();
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");
        plan.LinkContact(doctor);
        plan.DequeueEvents();
        var visit = new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0), with: doctor.Id);

        var id = plan.AddEntry(LocalizedText.Plain("Visit with Dr. Eleni Papadaki"), EntryKind.DoctorVisit, EntryWhen.Booked(visit));

        var entry = plan.GetCheck(id);
        Assert.True(id.IsCustom);
        Assert.Equal((CheckOrigin.AddedByParent, EntryKind.DoctorVisit, CheckState.Booked, visit), (entry.Origin, entry.Kind, entry.State, entry.Appointment));
        Assert.Equal(new CheckBooked(id, visit), Assert.Single(plan.DequeueEvents()));
        Assert.Equal(CheckStatus.Booked, plan.StatusOf(entry, TestData.Today, new TimeOnly(12, 0)));
        Assert.Same(entry, plan.NextAppointment(TestData.Today));
        plan.UnlinkContact(doctor.Id);
        Assert.Null(entry.Appointment!.With);
    }

    [Fact]
    public void A_booked_entry_stands_in_the_week_of_its_day()
    {
        var plan = TestData.Plan();

        var id = plan.AddEntry(LocalizedText.Plain("Hospital tour"), EntryKind.HospitalVisit, EntryWhen.Booked(October20));

        Assert.Equal(WeekWindow.OfWeek(27), plan.GetCheck(id).Window);
        Assert.Equal(EntryKind.HospitalVisit, plan.GetCheck(id).Kind);
    }

    [Fact]
    public void An_entry_already_done_in_a_week_is_done_with_no_booking_and_no_reminder()
    {
        var plan = TestData.Plan();
        var revision = plan.Revision;

        var id = plan.AddEntry(LocalizedText.Plain("Blood test"), EntryKind.Exam, EntryWhen.DoneInWeek(23));

        var entry = plan.GetCheck(id);
        Assert.Equal((CheckState.Done, (Appointment?)null, (Trimester?)null), (entry.State, entry.Appointment, entry.TrimesterOnly));
        Assert.Equal(WeekWindow.OfWeek(23), entry.Window);
        Assert.Equal(CheckStatus.Done, plan.StatusOf(entry, TestData.Today, TimeOnly.MinValue));
        Assert.Empty(plan.DequeueEvents());
        Assert.True(plan.Revision > revision);
    }

    [Fact]
    public void An_entry_already_done_in_a_trimester_stands_in_that_trimester()
    {
        var plan = TestData.Plan();

        var id = plan.AddEntry(LocalizedText.Plain("Blood test"), EntryKind.Exam, EntryWhen.DoneIn(Trimester.First));

        var entry = plan.GetCheck(id);
        Assert.Equal((CheckState.Done, (Trimester?)Trimester.First), (entry.State, entry.TrimesterOnly));
        Assert.Equal(WeekWindow.OfTrimester(Trimester.First), entry.Window);
        Assert.Equal(Trimester.First, entry.Trimester);
        Assert.Equal(id, plan.Checks[0].Id);
    }

    [Fact]
    public void An_entry_not_booked_yet_stands_in_its_week_and_raises_nothing()
    {
        var plan = TestData.Plan();
        var revision = plan.Revision;

        var id = plan.AddEntry(LocalizedText.Plain("Something for the bag"), EntryKind.Other, EntryWhen.NotBookedInWeek(30));

        var entry = plan.GetCheck(id);
        Assert.Empty(plan.DequeueEvents());
        Assert.True(plan.Revision > revision);
        Assert.Equal(WeekWindow.OfWeek(30), entry.Window);
        Assert.Equal(CheckState.NotBooked, entry.State);
        Assert.Equal(CheckStatus.NotBooked, plan.StatusOf(entry, TestData.Today.AddDays(70), TimeOnly.MinValue));
    }

    [Fact]
    public void An_entry_after_the_last_week_maia_shows_stands_in_that_week()
    {
        var plan = TestData.Plan();

        var id = plan.AddEntry(LocalizedText.Plain("Check-up for me"), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(TestData.DueDate.AddDays(200))));

        Assert.Equal(WeekWindow.OfWeek(WeekWindow.MaxWeek), plan.GetCheck(id).Window);
    }

    [Fact]
    public void An_entry_keeps_the_name_rules_and_its_appointment_names_only_her_care_team()
    {
        var plan = TestData.Plan();
        var count = plan.Checks.Count;
        var week = EntryWhen.NotBookedInWeek(30);

        Assert.Equal("entry.title.too_long", Assert.Throws<DomainException>(() =>
            plan.AddEntry(LocalizedText.Plain(new string('a', PlannedCheck.MaxTitleLength + 1)), EntryKind.Other, week)).Code);
        Assert.Equal("appointment.contact_unknown", Assert.Throws<DomainException>(() =>
            plan.AddEntry(LocalizedText.Plain("Visit"), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(TestData.Today, with: CareContactId.New())))).Code);
        Assert.Equal("entry.kind.invalid", Assert.Throws<DomainException>(() =>
            plan.AddEntry(LocalizedText.Plain("Visit"), (EntryKind)42, week)).Code);
        Assert.Equal(count, plan.Checks.Count);

        plan.AddEntry(LocalizedText.Plain(new string('a', PlannedCheck.MaxTitleLength)), EntryKind.Other, week);
        Assert.Equal(count + 1, plan.Checks.Count);
    }

    [Fact]
    public void A_template_check_has_no_kind_and_an_entry_she_adds_has_hers()
    {
        var plan = TestData.Plan();

        var id = plan.AddEntry(LocalizedText.Plain("Iron recheck"), EntryKind.Exam, EntryWhen.NotBookedInWeek(25));

        Assert.Equal(EntryKind.Exam, plan.GetCheck(id).Kind);
        Assert.Null(plan.GetCheck(TestData.Glucose).Kind);
    }

    [Fact]
    public void An_entry_with_only_a_trimester_stands_first_in_that_trimester()
    {
        var plan = TestData.Plan();
        var week14 = plan.AddEntry(LocalizedText.Plain("Week 14 visit"), EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(14));
        var second = plan.AddEntry(LocalizedText.Plain("Some time in the second"), EntryKind.Other, EntryWhen.NotBookedIn(Trimester.Second));
        var week28 = plan.AddEntry(LocalizedText.Plain("Week 28 visit"), EntryKind.DoctorVisit, EntryWhen.DoneInWeek(28));
        var third = plan.AddEntry(LocalizedText.Plain("Some time in the third"), EntryKind.Other, EntryWhen.DoneIn(Trimester.Third));

        Assert.Equal(
            new[] { TestData.Booking, second, week14, TestData.Anomaly, TestData.Glucose, TestData.Checkup25, third, week28, TestData.DueDateMilestone },
            plan.Checks.Select(c => c.Id));
    }

    [Fact]
    public void A_booked_entry_moved_to_another_day_stands_in_the_week_of_that_day()
    {
        var plan = TestData.Plan();
        var id = plan.AddEntry(LocalizedText.Plain("Visit with my doctor"), EntryKind.DoctorVisit, EntryWhen.Booked(October20));
        plan.DequeueEvents();

        plan.Book(id, January5);

        Assert.Equal(WeekWindow.OfWeek(38), plan.GetCheck(id).Window);
        Assert.Equal(new[] { id, TestData.DueDateMilestone }, plan.Checks.TakeLast(2).Select(c => c.Id));
        Assert.Equal(new CheckBooked(id, January5), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void Renaming_an_entry_says_so_for_its_reminders_and_the_same_name_changes_nothing()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, October20);
        plan.DequeueEvents();

        plan.Rename(TestData.Glucose, LocalizedText.Plain("Sugar curve"));

        Assert.Equal("Sugar curve", plan.GetCheck(TestData.Glucose).Title.In("el"));
        Assert.Equal(new CheckRenamed(TestData.Glucose), Assert.Single(plan.DequeueEvents()));

        var revision = plan.Revision;
        plan.Rename(TestData.Glucose, LocalizedText.Plain("Sugar curve"));
        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void A_name_keeps_the_rules_of_a_new_entry_and_a_milestone_keeps_its_own()
    {
        var plan = TestData.Plan();

        Assert.Equal("entry.title.too_long", Assert.Throws<DomainException>(() =>
            plan.Rename(TestData.Glucose, LocalizedText.Plain(new string('a', PlannedCheck.MaxTitleLength + 1)))).Code);
        Assert.Equal("check.is_milestone", Assert.Throws<DomainException>(() =>
            plan.Rename(TestData.DueDateMilestone, LocalizedText.Plain("Baby day"))).Code);
        Assert.Equal("check.not_found", Assert.Throws<DomainException>(() =>
            plan.Rename(new CheckId("nope"), LocalizedText.Plain("Something"))).Code);
        Assert.Equal("Glucose test", plan.GetCheck(TestData.Glucose).Title.In("en"));
        Assert.Empty(plan.DequeueEvents());
    }

    [Fact]
    public void Deleting_a_booked_entry_takes_it_and_its_booking_away()
    {
        var plan = TestData.Plan();
        var id = plan.AddEntry(LocalizedText.Plain("Visit with my doctor"), EntryKind.DoctorVisit, EntryWhen.Booked(October20));
        plan.DequeueEvents();

        plan.Delete(id);

        Assert.DoesNotContain(plan.Checks, c => c.Id == id);
        Assert.Null(plan.NextAppointment(TestData.Today));
        Assert.Equal(new CheckDeleted(id), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void A_check_from_setup_can_be_deleted_but_the_due_date_stays()
    {
        var plan = TestData.Plan();

        plan.Delete(TestData.Anomaly);

        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Anomaly);
        Assert.Equal("check.is_milestone", Assert.Throws<DomainException>(() => plan.Delete(TestData.DueDateMilestone)).Code);
        Assert.Equal("check.not_found", Assert.Throws<DomainException>(() => plan.Delete(TestData.Anomaly)).Code);
        Assert.Equal(new CheckDeleted(TestData.Anomaly), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void A_new_due_date_keeps_booked_entries_at_their_day_in_whichever_week_it_now_falls()
    {
        var plan = TestData.Plan();
        var booked = plan.AddEntry(LocalizedText.Plain("Visit with my doctor"), EntryKind.DoctorVisit, EntryWhen.Booked(October20));
        var done = plan.AddEntry(LocalizedText.Plain("Blood test"), EntryKind.Exam, EntryWhen.DoneInWeek(23));
        var waiting = plan.AddEntry(LocalizedText.Plain("Iron recheck"), EntryKind.Exam, EntryWhen.NotBookedIn(Trimester.Third));
        plan.Book(TestData.Checkup25, January5);
        plan.DequeueEvents();

        plan.ChangeDueDate(PregnancyDating.FromDueDate(TestData.DueDate.AddDays(7)));

        // Booked: the day stays, so the week moves; any origin. Not booked or done: the week or trimester she gave stays.
        Assert.Equal(WeekWindow.OfWeek(26), plan.GetCheck(booked).Window);
        Assert.Equal(WeekWindow.OfWeek(37), plan.GetCheck(TestData.Checkup25).Window);
        Assert.Equal(WeekWindow.OfWeek(23), plan.GetCheck(done).Window);
        Assert.Equal(WeekWindow.OfTrimester(Trimester.Third), plan.GetCheck(waiting).Window);
        Assert.Equal(new WeekWindow(24, 28), plan.GetCheck(TestData.Glucose).Window);
        Assert.Equal(plan.Checks.OrderBy(c => c.Window.FromWeek).Select(c => c.Window.FromWeek), plan.Checks.Select(c => c.Window.FromWeek));
        Assert.IsType<DueDateChanged>(Assert.Single(plan.DequeueEvents()));
    }
}

/// <summary>Setup finished again on a plan it had already made.</summary>
public sealed class SetupChoicesTests
{
    /// <summary>1 Jun 2026: 7+1, before every check in the test template.</summary>
    private static readonly DateOnly Early = new(2026, 6, 1);

    [Fact]
    public void A_check_she_switched_on_joins_and_one_she_switched_off_leaves()
    {
        var plan = TestData.Plan();
        var revision = plan.Revision;

        plan.ApplySetupChoices(TestData.Template(), new Dictionary<CheckId, bool> { [TestData.Nipt] = true, [TestData.Anomaly] = false }, Early);

        var nipt = plan.GetCheck(TestData.Nipt);
        Assert.Equal((CheckOrigin.Template, CheckState.NotBooked, new WeekWindow(10, 14)), (nipt.Origin, nipt.State, nipt.Window));
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Anomaly);
        Assert.Equal(plan.Checks.OrderBy(c => c.Window.FromWeek).Select(c => c.Id), plan.Checks.Select(c => c.Id));
        Assert.True(plan.Revision > revision);
        Assert.Empty(plan.DequeueEvents());
    }

    [Fact]
    public void A_check_whose_weeks_have_passed_does_not_join()
    {
        var plan = TestData.Plan();

        plan.ApplySetupChoices(TestData.Template(), new Dictionary<CheckId, bool> { [TestData.Nipt] = true }, TestData.Today);

        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Nipt);
    }

    [Fact]
    public void A_check_she_acted_on_stays_and_the_due_date_always_stays()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        plan.Reschedule(TestData.Anomaly, EntryWhen.DoneInWeek(21));
        var revision = plan.Revision;

        plan.ApplySetupChoices(
            TestData.Template(),
            new Dictionary<CheckId, bool> { [TestData.Glucose] = false, [TestData.Anomaly] = false, [TestData.DueDateMilestone] = false },
            Early);

        Assert.Equal(CheckState.Booked, plan.GetCheck(TestData.Glucose).State);
        Assert.Equal(CheckState.Done, plan.GetCheck(TestData.Anomaly).State);
        Assert.True(plan.GetCheck(TestData.DueDateMilestone).IsMilestone);
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void A_check_she_renamed_or_moved_stays_when_she_switches_it_off_again()
    {
        var plan = TestData.Plan();
        plan.Rename(TestData.Glucose, LocalizedText.Plain("My sugar test"));
        plan.Reschedule(TestData.Anomaly, EntryWhen.NotBookedInWeek(22));

        plan.ApplySetupChoices(TestData.Template(), new Dictionary<CheckId, bool> { [TestData.Glucose] = false, [TestData.Anomaly] = false }, Early);

        Assert.Contains(plan.Checks, c => c.Id == TestData.Glucose);
        Assert.Contains(plan.Checks, c => c.Id == TestData.Anomaly);
    }

    [Fact]
    public void Choices_that_change_nothing_leave_the_plan_as_it_was()
    {
        var plan = TestData.Plan();
        var ids = plan.Checks.Select(c => c.Id).ToList();

        plan.ApplySetupChoices(
            TestData.Template(),
            new Dictionary<CheckId, bool> { [TestData.Glucose] = true, [TestData.Growth] = false, [new CheckId("not-in-template")] = true },
            Early);

        Assert.Equal(ids, plan.Checks.Select(c => c.Id));
        Assert.Equal(0, plan.Revision);
    }
}

public sealed class ResultTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Files_are_classified_by_media_type()
    {
        Assert.Equal(AttachmentKind.Photo, ResultAttachment.ForFile(AttachmentId.New(), "scan.jpg", "image/jpeg", 1000, "b1", Now).Kind);
        Assert.Equal(AttachmentKind.Document, ResultAttachment.ForFile(AttachmentId.New(), "report.pdf", "application/pdf", 1000, "b2", Now).Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ResultAttachment.MaxFileSizeBytes + 1)]
    public void Rejects_empty_or_oversized_files(long size) =>
        Assert.Throws<DomainException>(() => ResultAttachment.ForFile(AttachmentId.New(), "x.pdf", "application/pdf", size, "b", Now));

    [Fact]
    public void Removing_a_result_raises_an_event_with_its_blob()
    {
        var plan = TestData.Plan();
        var attachment = ResultAttachment.ForFile(AttachmentId.New(), "report.pdf", "application/pdf", 1000, "blob-1", Now);
        plan.AttachResult(attachment);

        plan.RemoveResult(attachment.Id);

        Assert.Empty(plan.Results);
        Assert.Equal(new ResultRemoved(attachment.Id, "blob-1"), Assert.Single(plan.DequeueEvents()));
    }
}

public sealed class TaskAndCareTeamTests
{
    [Fact]
    public void Only_tasks_with_date_and_time_are_scheduled()
    {
        var plan = TestData.Plan();

        var undated = plan.AddTask("Buy maternity bra", week: 30);
        var timed = plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));

        Assert.Equal(new TaskScheduled(timed), Assert.Single(plan.DequeueEvents()));
        Assert.False(plan.GetTask(undated).IsScheduled);

        plan.SetTaskDone(timed, done: true);
        Assert.Equal(new TaskUnscheduled(timed), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void Editing_a_task_says_when_it_gains_moves_or_loses_its_time()
    {
        var plan = TestData.Plan();
        var bag = plan.AddTask("Pack the hospital bag", week: 30);
        plan.DequeueEvents();
        var revision = plan.Revision;

        plan.EditTask(bag, "Pack the hospital bag", new DateOnly(2026, 12, 1), new TimeOnly(18, 0));
        Assert.Equal(new TaskScheduled(bag), Assert.Single(plan.DequeueEvents()));

        plan.EditTask(bag, " Pack the bag ", new DateOnly(2026, 12, 1), new TimeOnly(18, 0));
        Assert.Equal(new TaskScheduled(bag), Assert.Single(plan.DequeueEvents()));
        Assert.Equal("Pack the bag", plan.GetTask(bag).Title);

        plan.EditTask(bag, "Pack the bag", week: 30);
        Assert.Equal(new TaskUnscheduled(bag), Assert.Single(plan.DequeueEvents()));
        Assert.Null(plan.GetTask(bag).DueDate);
        Assert.Null(plan.GetTask(bag).DueTime);

        plan.EditTask(bag, "Pack the bag and the car seat", week: 30);
        Assert.Empty(plan.DequeueEvents());
        Assert.True(plan.Revision > revision);
    }

    [Fact]
    public void Saving_a_task_unchanged_changes_nothing()
    {
        var plan = TestData.Plan();
        var call = plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        plan.DequeueEvents();
        var revision = plan.Revision;

        plan.EditTask(call, "  Call clinic ", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));

        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void An_edited_task_keeps_the_rules_of_a_new_one()
    {
        var plan = TestData.Plan();
        var call = plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));

        Assert.Equal("task.time_without_date", Assert.Throws<DomainException>(() => plan.EditTask(call, "Call clinic", null, new TimeOnly(9, 0))).Code);
        Assert.Equal("task.title.too_long", Assert.Throws<DomainException>(() => plan.EditTask(call, new string('a', PlanTask.MaxTitleLength + 1))).Code);
        Assert.Throws<ArgumentException>(() => plan.EditTask(call, "  "));
        Assert.Equal("Call clinic", plan.GetTask(call).Title);
        Assert.True(plan.GetTask(call).IsScheduled);
    }

    [Fact]
    public void Deleting_a_timed_task_unschedules_it()
    {
        var plan = TestData.Plan();
        var call = plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var bag = plan.AddTask("Pack the hospital bag", week: 30);
        plan.DequeueEvents();

        plan.RemoveTask(call);
        plan.RemoveTask(bag);

        Assert.Equal(new TaskUnscheduled(call), Assert.Single(plan.DequeueEvents()));
        Assert.Empty(plan.Tasks);
    }

    [Fact]
    public void Clearing_done_tasks_takes_away_only_the_ticked_ones_as_deleting_them_would()
    {
        var plan = TestData.Plan();
        var call = plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var bag = plan.AddTask("Pack the hospital bag", week: 30);
        var form = plan.AddTask("Send the form", new DateOnly(2026, 9, 28), new TimeOnly(9, 0));
        var pillow = plan.AddTask("Buy a pillow", week: 30);
        plan.SetTaskDone(form, done: true);
        plan.SetTaskDone(pillow, done: true);
        plan.DequeueEvents();
        var revision = plan.Revision;

        Assert.Equal(2, plan.RemoveDoneTasks());

        // A ticked task was unscheduled when she ticked it, so there is nothing more to take out; the open ones stay.
        Assert.Empty(plan.DequeueEvents());
        Assert.True(plan.Revision > revision);
        Assert.Equal(new[] { call, bag }, plan.Tasks.Select(t => t.Id));
        Assert.True(plan.GetTask(call).IsScheduled);
    }

    [Fact]
    public void Clearing_done_tasks_with_none_ticked_changes_nothing()
    {
        var plan = TestData.Plan();
        plan.AddTask("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        plan.DequeueEvents();
        var revision = plan.Revision;

        Assert.Equal(0, plan.RemoveDoneTasks());

        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(revision, plan.Revision);
        Assert.Single(plan.Tasks);
    }

    [Fact]
    public void A_question_can_be_moved_up_or_down_and_the_others_keep_their_order()
    {
        var plan = TestData.Plan();
        var first = plan.AddQuestion("First?");
        var second = plan.AddQuestion("Second?");
        var third = plan.AddQuestion("Third?");
        Assert.Equal(new[] { third, second, first }, plan.Questions.Select(q => q.Id));
        var revision = plan.Revision;

        plan.MoveQuestion(first, 0);
        Assert.Equal(new[] { first, third, second }, plan.Questions.Select(q => q.Id));
        Assert.True(plan.Revision > revision);

        // Past the end is the end; where it already is changes nothing.
        plan.MoveQuestion(first, 99);
        Assert.Equal(new[] { third, second, first }, plan.Questions.Select(q => q.Id));
        revision = plan.Revision;
        plan.MoveQuestion(second, 1);
        Assert.Equal(revision, plan.Revision);
        Assert.Equal("question.not_found", Assert.Throws<DomainException>(() => plan.MoveQuestion(QuestionId.New(), 0)).Code);
    }

    [Fact]
    public void A_question_can_be_reworded_and_answered_ones_cleared()
    {
        var plan = TestData.Plan();
        var swimming = plan.AddQuestion("Is swimming fine?");
        var bag = plan.AddQuestion("When should the bag be ready?");
        var relief = plan.AddQuestion("Can we talk through pain relief?");
        plan.SetQuestionAnswered(swimming, answered: true);
        plan.SetQuestionAnswered(relief, answered: true);

        plan.EditQuestion(bag, "  When should the hospital bag be ready? ");
        Assert.Equal("When should the hospital bag be ready?", plan.GetQuestion(bag).Text);
        Assert.Equal("question.text.too_long", Assert.Throws<DomainException>(() => plan.EditQuestion(bag, new string('a', DoctorQuestion.MaxTextLength + 1))).Code);

        var revision = plan.Revision;
        Assert.Equal(2, plan.RemoveAnsweredQuestions());
        Assert.Equal(bag, Assert.Single(plan.Questions).Id);
        Assert.True(plan.Revision > revision);

        revision = plan.Revision;
        Assert.Equal(0, plan.RemoveAnsweredQuestions());
        plan.EditQuestion(bag, "When should the hospital bag be ready?");
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void A_new_question_goes_on_top_of_the_others_and_is_ticked_off_when_answered()
    {
        var plan = TestData.Plan();

        var swimming = plan.AddQuestion("  Is swimming fine?  ");
        plan.AddQuestion("When do we talk about the birth plan?");
        plan.SetQuestionAnswered(swimming, answered: true);

        Assert.Equal(new[] { "When do we talk about the birth plan?", "Is swimming fine?" }, plan.Questions.Select(q => q.Text));
        Assert.True(plan.GetQuestion(swimming).IsAnswered);

        plan.RemoveQuestion(swimming);
        Assert.Single(plan.Questions);
    }

    // ---- Results, apart from the entries on her journey ------------------------------------

    private static ResultAttachment Pdf(string blobId = "0123456789abcdef0123456789abcdef") =>
        ResultAttachment.ForFile(AttachmentId.New(), "report.pdf", "application/pdf", 2048, blobId, DateTimeOffset.UnixEpoch);

    [Fact]
    public void A_result_she_adds_is_kept_in_her_results_and_no_entry_holds_it()
    {
        var plan = TestData.Plan();
        var result = Pdf();

        plan.AttachResult(result);

        Assert.Equal(result, Assert.Single(plan.Results));
        Assert.Equal(result, plan.FindResult(result.Id));
        Assert.Equal("attachment.not_found", Assert.Throws<DomainException>(() => plan.FindResult(AttachmentId.New())).Code);
        Assert.Empty(plan.DequeueEvents());
    }

    [Fact]
    public void A_result_can_be_renamed_and_keeps_its_file_and_place()
    {
        var plan = TestData.Plan();
        var first = Pdf();
        var second = Pdf("fedcba9876543210fedcba9876543210");
        plan.AttachResult(first);
        plan.AttachResult(second);
        var revision = plan.Revision;

        Assert.True(plan.RenameResult(first.Id, "  Glucose curve  "));

        Assert.Equal(new[] { "Glucose curve", second.DisplayName }, plan.Results.Select(r => r.DisplayName));
        Assert.Equal((first.Id, first.BlobId, first.AddedAt), (plan.Results[0].Id, plan.Results[0].BlobId, plan.Results[0].AddedAt));
        Assert.True(plan.Revision > revision);
        Assert.Empty(plan.DequeueEvents());
    }

    [Fact]
    public void Renaming_a_result_to_its_own_name_changes_nothing_and_a_blank_name_is_refused()
    {
        var plan = TestData.Plan();
        var result = Pdf();
        plan.AttachResult(result);
        var revision = plan.Revision;

        Assert.False(plan.RenameResult(result.Id, result.DisplayName));
        Assert.Equal(revision, plan.Revision);
        Assert.ThrowsAny<ArgumentException>(() => plan.RenameResult(result.Id, "  "));
        Assert.Equal("attachment.not_found", Assert.Throws<DomainException>(() => plan.RenameResult(AttachmentId.New(), "Report")).Code);
        Assert.Equal(result.DisplayName, Assert.Single(plan.Results).DisplayName);
    }

    [Fact]
    public void Removing_results_lets_their_files_go()
    {
        var plan = TestData.Plan();
        var first = Pdf();
        var second = Pdf("fedcba9876543210fedcba9876543210");
        plan.AttachResult(first);
        plan.AttachResult(second);

        plan.RemoveResult(first.Id);
        plan.RemoveResult(second.Id);

        Assert.Equal(
            new IDomainEvent[] { new ResultRemoved(first.Id, first.BlobId), new ResultRemoved(second.Id, second.BlobId) },
            plan.DequeueEvents());
        Assert.Empty(plan.Results);
    }

    [Fact]
    public void A_result_is_in_the_plan_once()
    {
        var plan = TestData.Plan();
        var result = Pdf();
        plan.AttachResult(result);

        Assert.Equal("attachment.duplicate", Assert.Throws<DomainException>(() => plan.AttachResult(result)).Code);
        Assert.Single(plan.Results);
    }

    [Fact]
    public void Linking_or_unlinking_her_doctor_says_the_care_team_changed()
    {
        var plan = TestData.Plan();
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");

        plan.LinkContact(doctor);
        Assert.IsType<CareTeamChanged>(Assert.Single(plan.DequeueEvents()));

        plan.LinkContact(new CareContact(CareContactId.New(), CareRole.ObGyn, "Eleni", "+306900004567"));
        Assert.Empty(plan.DequeueEvents());

        plan.UnlinkContact(doctor.Id);
        Assert.IsType<CareTeamChanged>(Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void Questions_cannot_be_empty_or_too_long()
    {
        var plan = TestData.Plan();

        Assert.Throws<ArgumentException>(() => plan.AddQuestion("   "));
        var ex = Assert.Throws<DomainException>(() => plan.AddQuestion(new string('a', DoctorQuestion.MaxTextLength + 1)));
        Assert.Equal("question.text.too_long", ex.Code);
    }

    [Fact]
    public void A_task_time_needs_a_date()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.Plan().AddTask("Call", dueDate: null, dueTime: new TimeOnly(9, 0)));
        Assert.Equal("task.time_without_date", ex.Code);
    }

    [Fact]
    public void Linking_the_same_number_twice_is_ignored()
    {
        var plan = TestData.Plan();
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile");
        var same = new CareContact(CareContactId.New(), CareRole.ObGyn, "Eleni", "+306900004567");

        Assert.True(plan.LinkContact(doctor));
        Assert.False(plan.LinkContact(same));
        Assert.Single(plan.CareTeam.For(CareRole.ObGyn));
        Assert.Equal(doctor, plan.CareTeam.PrimaryFor(CareRole.ObGyn));
    }

    [Fact]
    public void Unlinking_a_contact_removes_it_from_appointments()
    {
        var plan = TestData.Plan();
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");
        plan.LinkContact(doctor);
        plan.Book(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30), with: doctor.Id));

        plan.UnlinkContact(doctor.Id);

        Assert.Empty(plan.CareTeam.Contacts);
        Assert.Null(plan.GetCheck(TestData.Checkup25).Appointment!.With);
    }
}

/// <summary>Her bookings are wall-clock times in one home time zone, kept with the plan, so travelling never moves them.</summary>
public sealed class HomeTimeZoneTests
{
    [Fact]
    public void A_new_plan_keeps_the_zone_it_was_made_in_wherever_the_phone_goes()
    {
        var plan = PregnancyPlan.Create(PlanId.New(), PregnancyDating.FromDueDate(TestData.DueDate), TestData.Template(), "en", DateTimeOffset.UnixEpoch, timeZoneId: "Europe/Athens");
        var revision = plan.Revision;

        Assert.Equal("Europe/Athens", plan.TimeZoneId);
        Assert.False(plan.SetHomeTimeZoneIfUnknown("Europe/London"));
        Assert.Equal("Europe/Athens", plan.TimeZoneId);
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void A_plan_saved_before_the_zone_was_kept_takes_the_phones_once()
    {
        var plan = TestData.Plan();
        Assert.Null(plan.TimeZoneId);

        Assert.True(plan.SetHomeTimeZoneIfUnknown("Europe/Athens"));
        Assert.Equal(1, plan.Revision);
        Assert.Empty(plan.DequeueEvents());

        Assert.False(plan.SetHomeTimeZoneIfUnknown("Europe/London"));
        Assert.Equal("Europe/Athens", plan.TimeZoneId);
    }

    [Theory]
    [InlineData("Europe/Athens\n")]
    [InlineData(" Europe/Athens")]
    [InlineData("Ευρώπη/Αθήνα")]
    public void A_zone_is_an_id_never_free_text(string timeZoneId)
    {
        var ex = Assert.Throws<DomainException>(() => TestData.Plan().SetHomeTimeZoneIfUnknown(timeZoneId));
        Assert.Equal("plan.time_zone.invalid", ex.Code);
        Assert.Throws<ArgumentException>(() => TestData.Plan().SetHomeTimeZoneIfUnknown(" "));
        Assert.Throws<DomainException>(() => TestData.Plan().SetHomeTimeZoneIfUnknown(new string('a', PregnancyPlan.MaxTimeZoneIdLength + 1)));
    }

    [Fact]
    public void A_stored_zone_that_is_not_an_id_is_forgotten_and_noted()
    {
        var repairs = new List<string>();

        var plan = PregnancyPlan.Rehydrate(
            PlanId.New(),
            PregnancyDating.FromDueDate(TestData.DueDate),
            new TemplateReference("test-gr", 1),
            "en",
            DateTimeOffset.UnixEpoch,
            [],
            [],
            new CareTeam(),
            revision: 3,
            repairs: repairs,
            timeZoneId: "Ευρώπη/Αθήνα");

        Assert.Null(plan.TimeZoneId);
        Assert.Equal("plan.time_zone_invalid", Assert.Single(repairs));
    }
}

