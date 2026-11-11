using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Testing;

namespace Maia.Application.Tests;

/// <summary>When a booking's reminders ring, and the keys they are known by.</summary>
public sealed class ReminderTimesTests
{
    private static readonly DateOnly Day = new(2026, 10, 12);
    private static readonly TimeOnly HalfPastEight = new(8, 30);

    [Fact]
    public void Keys_name_what_a_reminder_is_for_as_before()
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Call the clinic", Day, new TimeOnly(9, 0));

        Assert.Equal("check:ogtt", ReminderTimes.KeyFor(plan.GetCheck(TestData.Glucose)));
        Assert.Equal($"task:{task}", ReminderTimes.KeyFor(plan.GetTask(task)));
    }

    [Fact]
    public void The_day_before_reminder_is_a_full_24_hours_across_a_daylight_saving_change()
    {
        // Clocks go back in Athens early on 25 Oct 2026: the reminder still comes 24 real hours before 09:00 on the 25th.
        var start = ReminderTimes.ToInstant(new DateOnly(2026, 10, 25), new TimeOnly(9, 0), FakeClock.Athens);

        var dayBefore = ReminderTimes.DayBefore(new DateOnly(2026, 10, 25), new TimeOnly(9, 0), FakeClock.Athens);

        Assert.Equal(TimeSpan.FromHours(24), start - dayBefore);
        Assert.Equal(TimeSpan.FromHours(2), start.Offset);
    }

    [Fact]
    public void Times_inside_a_spring_forward_gap_move_to_the_next_valid_time()
    {
        // 29 Mar 2026 in Athens: 03:00 jumps to 04:00.
        var instant = ReminderTimes.ToInstant(new DateOnly(2026, 3, 29), new TimeOnly(3, 15), FakeClock.Athens);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 4, 15, 0, TimeSpan.FromHours(3)), instant);
    }

    [Fact]
    public void A_day_without_a_time_is_reminded_at_eight_the_evening_before()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 20, 0, 0, TimeSpan.FromHours(3)), ReminderTimes.DayBefore(Day, null, FakeClock.Athens));
    }

    [Theory]
    [InlineData(ReminderPreference.DayBefore, 11, 8, ReminderPreference.DayBefore)]
    [InlineData(ReminderPreference.DayBefore, 11, 9, null)]
    [InlineData(ReminderPreference.DayBefore, 12, 7, null)]
    [InlineData(ReminderPreference.Both, 11, 8, ReminderPreference.Both)]
    [InlineData(ReminderPreference.Both, 12, 2, ReminderPreference.TwoHoursBefore)]
    [InlineData(ReminderPreference.Both, 12, 7, null)]
    [InlineData(ReminderPreference.TwoHoursBefore, 10, 8, ReminderPreference.TwoHoursBefore)]
    [InlineData(ReminderPreference.TwoHoursBefore, 12, 6, ReminderPreference.TwoHoursBefore)]
    [InlineData(ReminderPreference.TwoHoursBefore, 12, 7, null)]
    public void A_booking_with_a_time_rings_only_what_she_chose_while_it_is_still_ahead(
        ReminderPreference chosen, int day, int hour, ReminderPreference? rings)
    {
        Assert.Equal(rings, ReminderTimes.ToCome(chosen, Day, HalfPastEight, FakeClock.Athens, At(day, hour)));
    }

    [Theory]
    [InlineData(ReminderPreference.DayBefore)]
    [InlineData(ReminderPreference.TwoHoursBefore)]
    [InlineData(ReminderPreference.Both)]
    public void A_day_booked_without_a_time_rings_the_evening_before_whatever_she_chose(ReminderPreference chosen)
    {
        Assert.Equal(ReminderPreference.DayBefore, ReminderTimes.ToCome(chosen, Day, null, FakeClock.Athens, At(11, 19)));
        Assert.Null(ReminderTimes.ToCome(chosen, Day, null, FakeClock.Athens, At(11, 21)));
    }

    [Fact]
    public void A_doctor_visit_is_named_after_her_doctor_in_her_language()
    {
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");

        var title = EntryTitles.DoctorVisit(doctor);

        Assert.Equal(("Visit with Dr. Eleni Papadaki", "Επίσκεψη: Dr. Eleni Papadaki"), (title.In(LocalizedText.English), title.In(LocalizedText.Greek)));
    }

    [Fact]
    public void A_doctor_visit_with_no_doctor_linked_is_called_an_appointment_in_both_languages()
    {
        var title = EntryTitles.DoctorVisit((CareContact?)null);

        Assert.Equal(("Appointment", "Ραντεβού"), (title.In(LocalizedText.English), title.In(LocalizedText.Greek)));
    }

    /// <summary>An instant in October 2026, Athens time (summer time, UTC+3).</summary>
    private static DateTimeOffset At(int day, int hour) => new(2026, 10, day, hour, 0, 0, TimeSpan.FromHours(3));
}
