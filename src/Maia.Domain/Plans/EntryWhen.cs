using Maia.Domain.Common;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;

namespace Maia.Domain.Plans;

/// <summary>
/// When an entry on her journey happens: booked for a day and time (past or to come; once it has passed it is done),
/// or, with no booking, not booked yet or already done, in a trimester or a week. Exactly one of
/// <see cref="Appointment"/>, <see cref="Trimester"/> and <see cref="Week"/> is set.
/// </summary>
public sealed record EntryWhen
{
    private EntryWhen(Appointment? appointment, bool isDone, Trimester? trimester, int? week)
    {
        Appointment = appointment;
        IsDone = isDone;
        Trimester = trimester;
        Week = week;
    }

    /// <summary>The booking, when it is booked.</summary>
    public Appointment? Appointment { get; }

    /// <summary>Done with no booking (in <see cref="Trimester"/> or <see cref="Week"/>).</summary>
    public bool IsDone { get; }

    /// <summary>The trimester, when only that is known.</summary>
    public Trimester? Trimester { get; }

    /// <summary>The pregnancy week, when that is known and there is no booking.</summary>
    public int? Week { get; }

    public bool IsBooked => Appointment is not null;

    /// <summary>"Booked for": it gets its reminders, as any booking does, and is done once its time has passed.</summary>
    public static EntryWhen Booked(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        return new EntryWhen(appointment, isDone: false, null, null);
    }

    /// <summary>Not booked yet, some time in <paramref name="trimester"/>.</summary>
    public static EntryWhen NotBookedIn(Trimester trimester) => new(null, isDone: false, ValidTrimester(trimester), null);

    /// <summary>Not booked yet, in pregnancy week <paramref name="week"/>.</summary>
    public static EntryWhen NotBookedInWeek(int week) => new(null, isDone: false, null, ValidWeek(week));

    /// <summary>Already done, some time in <paramref name="trimester"/>.</summary>
    public static EntryWhen DoneIn(Trimester trimester) => new(null, isDone: true, ValidTrimester(trimester), null);

    /// <summary>Already done, in pregnancy week <paramref name="week"/>.</summary>
    public static EntryWhen DoneInWeek(int week) => new(null, isDone: true, null, ValidWeek(week));

    private static Trimester ValidTrimester(Trimester trimester) =>
        Enum.IsDefined(trimester) ? trimester : throw new DomainException("entry.trimester.invalid", "Not a trimester.");

    private static int ValidWeek(int week) =>
        week is >= 0 and <= WeekWindow.MaxWeek ? week : throw new DomainException("entry.week.invalid", "Not a pregnancy week.");
}
