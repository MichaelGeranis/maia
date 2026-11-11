using System.Globalization;
using Maia.Domain.Common;
using Maia.Domain.Pregnancy;

namespace Maia.Domain.Templates;

/// <summary>
/// The pregnancy weeks in which a check is usually done, inclusive at both ends.
/// <c>24–28</c> means from 24+0 up to and including 28+6. Weeks after 40 describe
/// the period after birth (for example 46 is roughly six weeks postpartum).
/// </summary>
public readonly record struct WeekWindow
{
    public const int MaxWeek = 52;

    public WeekWindow(int fromWeek, int toWeek)
    {
        if (fromWeek < 0 || toWeek > MaxWeek || fromWeek > toWeek)
        {
            throw new DomainException(
                "window.invalid",
                "The week window is invalid.");
        }

        FromWeek = fromWeek;
        ToWeek = toWeek;
    }

    public int FromWeek { get; }

    public int ToWeek { get; }

    public GestationalAge Start => GestationalAge.FromWeeks(FromWeek);

    /// <summary>Last day inside the window (<c>ToWeek+6</c>).</summary>
    public GestationalAge End => GestationalAge.FromWeeks(ToWeek, GestationalAge.DaysPerWeek - 1);

    public static WeekWindow OfWeek(int week) => new(week, week);

    /// <summary>
    /// The weeks of a trimester (<see cref="GestationalAge.Trimester"/>), the third up to week 40; after birth, the
    /// weeks after that.
    /// </summary>
    public static WeekWindow OfTrimester(Trimester trimester) => trimester switch
    {
        Trimester.First => new(0, 13),
        Trimester.Second => new(14, 27),
        Trimester.AfterBirth => new(41, MaxWeek),
        _ => new(28, 40),
    };

    public bool Contains(GestationalAge age) => age >= Start && age <= End;

    public bool HasPassed(GestationalAge age) => age > End;

    public override string ToString() => FromWeek == ToWeek
        ? FromWeek.ToString(CultureInfo.InvariantCulture)
        : string.Create(CultureInfo.InvariantCulture, $"{FromWeek}-{ToWeek}");
}
