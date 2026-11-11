using Maia.Domain.Common;

namespace Maia.Domain.Plans;

/// <summary>
/// A blood pressure reading she logged on New ("118/76"), at the day and time it was taken, a wall-clock time in her home
/// zone. Kept as she wrote it: Maia never interprets, flags or charts it, and it can be deleted but not changed.
/// </summary>
public sealed record BloodPressureReading
{
    /// <summary>The range of the upper number a monitor can show; outside it, she has mistyped.</summary>
    public const int MinSystolic = 50;

    public const int MaxSystolic = 300;

    /// <summary>The range of the lower number a monitor can show.</summary>
    public const int MinDiastolic = 20;

    public const int MaxDiastolic = 200;

    private BloodPressureReading(ReadingId id, int systolic, int diastolic, DateOnly date, TimeOnly time)
    {
        Id = id;
        Systolic = systolic;
        Diastolic = diastolic;
        Date = date;
        Time = time;
    }

    public ReadingId Id { get; }

    /// <summary>The upper number, in mmHg.</summary>
    public int Systolic { get; }

    /// <summary>The lower number, in mmHg.</summary>
    public int Diastolic { get; }

    public DateOnly Date { get; }

    /// <summary>The time it was taken, to the minute.</summary>
    public TimeOnly Time { get; }

    /// <summary>
    /// Whether two numbers can be a reading as a monitor shows it: each within what a monitor can show, the upper above
    /// the lower. A check on what she typed, never on her health.
    /// </summary>
    public static bool IsValid(int systolic, int diastolic) =>
        systolic is >= MinSystolic and <= MaxSystolic
        && diastolic is >= MinDiastolic and <= MaxDiastolic
        && systolic > diastolic;

    internal static BloodPressureReading Create(ReadingId id, int systolic, int diastolic, DateOnly date, TimeOnly time)
    {
        if (!IsValid(systolic, diastolic))
        {
            throw new DomainException("reading.values.invalid", "Not a blood pressure reading as a monitor shows it.");
        }

        return new BloodPressureReading(id, systolic, diastolic, date, new TimeOnly(time.Hour, time.Minute));
    }

    /// <summary>Rebuilds a stored reading as it was saved, whatever the rules are now. Persistence only.</summary>
    internal static BloodPressureReading Restore(ReadingId id, int systolic, int diastolic, DateOnly date, TimeOnly time) =>
        new(id, systolic, diastolic, date, time);
}
