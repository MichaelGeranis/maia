using Maia.Application.Abstractions;
using Maia.Domain.Plans;

namespace Maia.Application.Plans;

/// <summary>
/// The plan's home time zone: every booking, visit and task time is a wall-clock time there, so her
/// reminders stay at the same instant while she travels. A plan saved before Maia kept one uses the phone's zone until
/// it is set (<see cref="PlanService.KeepHomeTimeZoneAsync"/>).
/// </summary>
public static class HomeTimeZone
{
    /// <summary>The zone's IANA id (<c>Europe/Athens</c>), as the plan keeps it; a Windows id is converted when it can be.</summary>
    public static string IdOf(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return zone.HasIanaId || !TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? zone.Id : iana;
    }

    /// <summary>
    /// The zone her bookings are in: the plan's home zone, or the phone's for a plan that has none yet or names one this
    /// phone does not know.
    /// </summary>
    public static TimeZoneInfo Of(PregnancyPlan? plan, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return Find(plan?.TimeZoneId, clock) ?? clock.TimeZone;
    }

    /// <summary>A zone by its id, or null when the id is empty or this phone does not know it.</summary>
    public static TimeZoneInfo? Find(string? timeZoneId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (string.IsNullOrEmpty(timeZoneId))
        {
            return null;
        }

        if (timeZoneId == clock.TimeZone.Id || timeZoneId == IdOf(clock.TimeZone))
        {
            return clock.TimeZone;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
