using Maia.Domain.Common;

namespace Maia.Domain.Pregnancy;

/// <summary>
/// Converts between calendar dates and gestational age for one pregnancy.
/// The due date is the single source of truth; a dating scan can move it, and every
/// week-based date in the plan follows automatically.
/// </summary>
public sealed record PregnancyDating
{
    /// <summary>Naegele's rule: a pregnancy is dated as 280 days (40+0) from the last period.</summary>
    public const int TermDays = 280;

    /// <summary>Longest gestational age the app accepts (44+0), well past any post-term induction.</summary>
    public static readonly GestationalAge MaxSupported = GestationalAge.FromWeeks(44);

    private PregnancyDating(DateOnly dueDate)
    {
        DueDate = dueDate;
    }

    public DateOnly DueDate { get; }

    public DateOnly LastMenstrualPeriod => DueDate.AddDays(-TermDays);

    public static PregnancyDating FromDueDate(DateOnly dueDate) => new(dueDate);

    public static PregnancyDating FromLastMenstrualPeriod(DateOnly firstDayOfLastPeriod) =>
        new(firstDayOfLastPeriod.AddDays(TermDays));

    /// <summary>
    /// Creates dating for a pregnancy that is plausible on <paramref name="today"/>: already started
    /// and not beyond <see cref="MaxSupported"/>.
    /// </summary>
    public static PregnancyDating ForToday(DateOnly dueDate, DateOnly today)
    {
        var dating = new PregnancyDating(dueDate);
        var daysSinceLmp = today.DayNumber - dating.LastMenstrualPeriod.DayNumber;
        if (daysSinceLmp < 0 || daysSinceLmp > MaxSupported.TotalDays)
        {
            throw new DomainException(
                "dating.out_of_range",
                "The due date is not plausible for today.");
        }

        return dating;
    }

    /// <summary>Gestational age on <paramref name="date"/>; dates before the last period count as 0+0.</summary>
    public GestationalAge On(DateOnly date) =>
        new(Math.Max(0, date.DayNumber - LastMenstrualPeriod.DayNumber));

    /// <summary>The calendar date on which the pregnancy reaches <paramref name="age"/>.</summary>
    public DateOnly DateAt(GestationalAge age) => LastMenstrualPeriod.AddDays(age.TotalDays);

    /// <summary>The first day of pregnancy week <paramref name="week"/> (that is, <c>week+0</c>).</summary>
    public DateOnly StartOfWeek(int week) => DateAt(GestationalAge.FromWeeks(week));

    public int DaysUntilDue(DateOnly today) => DueDate.DayNumber - today.DayNumber;
}
