using System.Globalization;

namespace Maia.Domain.Pregnancy;

/// <summary>
/// Trimester boundaries follow the common clinical convention: 0–13+6, 14–27+6, 28+. <see cref="AfterBirth"/> is the
/// time after the due date, as Journey groups it: where she can place an entry or a task by hand, never a pregnancy's
/// age.
/// </summary>
public enum Trimester
{
    First = 1,
    Second = 2,
    Third = 3,
    AfterBirth = 4,
}

/// <summary>
/// Pregnancy age counted from the first day of the last menstrual period, in days.
/// Written the clinical way, for example <c>24+3</c> (24 weeks and 3 days).
/// </summary>
public readonly record struct GestationalAge : IComparable<GestationalAge>
{
    public const int DaysPerWeek = 7;

    public GestationalAge(int totalDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalDays);
        TotalDays = totalDays;
    }

    public int TotalDays { get; }

    public int Weeks => TotalDays / DaysPerWeek;

    public int Days => TotalDays % DaysPerWeek;

    /// <summary>Fractional weeks, useful for comparing against week-based windows.</summary>
    public double ExactWeeks => TotalDays / (double)DaysPerWeek;

    public Trimester Trimester => Weeks switch
    {
        < 14 => Trimester.First,
        < 28 => Trimester.Second,
        _ => Trimester.Third,
    };

    public static GestationalAge FromWeeks(int weeks, int days = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(weeks);
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, DaysPerWeek - 1);
        return new GestationalAge((weeks * DaysPerWeek) + days);
    }

    public int CompareTo(GestationalAge other) => TotalDays.CompareTo(other.TotalDays);

    public static bool operator <(GestationalAge left, GestationalAge right) => left.CompareTo(right) < 0;

    public static bool operator >(GestationalAge left, GestationalAge right) => left.CompareTo(right) > 0;

    public static bool operator <=(GestationalAge left, GestationalAge right) => left.CompareTo(right) <= 0;

    public static bool operator >=(GestationalAge left, GestationalAge right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Weeks}+{Days}");
}
