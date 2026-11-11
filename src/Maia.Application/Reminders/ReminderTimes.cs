using Maia.Application.Abstractions;
using Maia.Domain.Common;
using Maia.Domain.Plans;

namespace Maia.Application.Reminders;

/// <summary>
/// When the reminders for a booking (of any entry: a check, a doctor visit, a hospital visit) or a timed task ring, and
/// the keys they are known by. Pure: no I/O and no clock, so the rules are fully unit-tested. Reminders depend only on
/// the booking's time:
/// <list type="bullet">
/// <item>"24 hours before": 24 hours before a booking with a time, or the evening before one with only a day, while that
/// is still ahead. Nothing stands in once it has passed: a booking made less than a day ahead gets no reminder (the
/// owner's call, so that a booking made days ahead never rings twice).</item>
/// <item>"2 hours before": 2 hours before a booking with a time; a booking with only a day has no time to count from, so
/// it rings the evening before instead.</item>
/// <item>"Both": each of them, while it is still ahead.</item>
/// </list>
/// </summary>
public static class ReminderTimes
{
    /// <summary>The time before a booking at which the day-before reminder comes.</summary>
    public static readonly TimeSpan DayBeforeReminder = TimeSpan.FromHours(24);

    /// <summary>The time before a booking at which the short-notice reminder comes.</summary>
    public static readonly TimeSpan ShortNoticeReminder = TimeSpan.FromHours(2);

    public static string KeyFor(PlannedCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return KeyFor(check.Id);
    }

    public static string KeyFor(CheckId checkId) => $"check:{checkId}";

    public static string KeyFor(PlanTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return KeyFor(task.Id);
    }

    public static string KeyFor(TaskId taskId) => $"task:{taskId}";

    /// <summary>
    /// When the day-before reminder for a booking on <paramref name="date"/> comes: 24 hours before its time, or, for a
    /// booking with only a day, at <see cref="ReminderSettings.EveningReminderHour"/> the day before.
    /// </summary>
    public static DateTimeOffset DayBefore(DateOnly date, TimeOnly? time, TimeZoneInfo timeZone) =>
        time is { } at
            ? ToInstant(date, at, timeZone) - DayBeforeReminder
            : ToInstant(date.AddDays(-1), new TimeOnly(ReminderSettings.EveningReminderHour, 0), timeZone);

    /// <summary>
    /// The reminders that will ring for a booking on <paramref name="date"/> at <paramref name="now"/>, by the rules
    /// above: <see cref="ReminderPreference.DayBefore"/> for the 24-hour (or evening-before) one,
    /// <see cref="ReminderPreference.TwoHoursBefore"/> for the 2-hour one, <see cref="ReminderPreference.Both"/> for both,
    /// and null when none is still ahead.
    /// </summary>
    public static ReminderPreference? ToCome(ReminderPreference when, DateOnly date, TimeOnly? time, TimeZoneInfo timeZone, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var wantsDayBefore = when is ReminderPreference.DayBefore or ReminderPreference.Both;
        var wantsTwoHours = when is ReminderPreference.TwoHoursBefore or ReminderPreference.Both;
        var dayBeforeAhead = DayBefore(date, time, timeZone) > now;
        if (time is not { } at)
        {
            // Only a day: the evening before stands in for "2 hours before" too.
            return dayBeforeAhead ? ReminderPreference.DayBefore : null;
        }

        var dayBefore = wantsDayBefore && dayBeforeAhead;
        var twoHours = wantsTwoHours && ToInstant(date, at, timeZone) - ShortNoticeReminder > now;
        return (dayBefore, twoHours) switch
        {
            (true, true) => ReminderPreference.Both,
            (true, false) => ReminderPreference.DayBefore,
            (false, true) => ReminderPreference.TwoHoursBefore,
            _ => null,
        };
    }

    /// <summary>Converts a wall-clock time to an instant, moving times that fall in a DST gap forward.</summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }
}
