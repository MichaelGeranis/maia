using Maia.Domain.Common;

namespace Maia.Domain.Plans;

/// <summary>
/// A booked slot. Date and time are wall-clock values in the plan's home time zone
/// (<see cref="PregnancyPlan.TimeZoneId"/>); conversion to an instant happens at the edges (reminders),
/// never in the domain.
/// </summary>
public sealed record Appointment
{
    public const int MaxLocationLength = 200;

    public Appointment(DateOnly date, TimeOnly? time = null, string? location = null, CareContactId? with = null)
    {
        if (location is { Length: > MaxLocationLength })
        {
            throw new DomainException("appointment.location.too_long", "Location is too long.");
        }

        Date = date;
        Time = time;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        With = with;
    }

    public DateOnly Date { get; }

    /// <summary>Null when only the day is known ("some time on Monday").</summary>
    public TimeOnly? Time { get; }

    public string? Location { get; }

    /// <summary>The care-team contact the appointment is with, if linked.</summary>
    public CareContactId? With { get; }
}
