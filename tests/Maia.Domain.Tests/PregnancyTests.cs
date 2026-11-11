using Maia.Domain.Common;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Domain.Tests;

public sealed class GestationalAgeTests
{
    [Theory]
    [InlineData(0, "0+0")]
    [InlineData(171, "24+3")]
    [InlineData(280, "40+0")]
    public void Formats_as_weeks_plus_days(int days, string expected) =>
        Assert.Equal(expected, new GestationalAge(days).ToString());

    [Theory]
    [InlineData(13, 6, Trimester.First)]
    [InlineData(14, 0, Trimester.Second)]
    [InlineData(27, 6, Trimester.Second)]
    [InlineData(28, 0, Trimester.Third)]
    public void Trimester_boundaries_follow_clinical_convention(int weeks, int days, Trimester expected) =>
        Assert.Equal(expected, GestationalAge.FromWeeks(weeks, days).Trimester);

    [Fact]
    public void Rejects_negative_and_out_of_range_parts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GestationalAge(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GestationalAge.FromWeeks(10, 7));
    }
}

public sealed class PregnancyDatingTests
{
    [Fact]
    public void Due_date_is_280_days_after_last_period()
    {
        var dating = PregnancyDating.FromLastMenstrualPeriod(new DateOnly(2026, 4, 12));

        Assert.Equal(TestData.DueDate, dating.DueDate);
    }

    [Fact]
    public void Computes_gestational_age_on_a_date()
    {
        var dating = PregnancyDating.FromDueDate(TestData.DueDate);

        Assert.Equal(GestationalAge.FromWeeks(24, 3), dating.On(TestData.Today));
        Assert.Equal(GestationalAge.FromWeeks(40), dating.On(TestData.DueDate));
        Assert.Equal(new GestationalAge(0), dating.On(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Converts_weeks_back_to_dates()
    {
        var dating = PregnancyDating.FromDueDate(TestData.DueDate);

        Assert.Equal(new DateOnly(2026, 9, 27), dating.StartOfWeek(24));
        Assert.Equal(TestData.Today, dating.DateAt(GestationalAge.FromWeeks(24, 3)));
        Assert.Equal(109, dating.DaysUntilDue(TestData.Today));
    }

    [Theory]
    [InlineData("2027-06-01", "2026-08-01")] // pregnancy would not have started yet
    [InlineData("2026-06-01", "2026-09-30")] // would be past 44 weeks
    public void Rejects_implausible_due_dates(string due, string today)
    {
        var ex = Assert.Throws<DomainException>(() => PregnancyDating.ForToday(DateOnly.Parse(due, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal("dating.out_of_range", ex.Code);
    }

    [Fact]
    public void Accepts_a_plausible_due_date() =>
        Assert.Equal(TestData.DueDate, PregnancyDating.ForToday(TestData.DueDate, TestData.Today).DueDate);
}

public sealed class WeekWindowTests
{
    [Theory]
    [InlineData(-1, 4)]
    [InlineData(10, 9)]
    [InlineData(40, 53)]
    public void Rejects_invalid_windows(int from, int to) =>
        Assert.Throws<DomainException>(() => new WeekWindow(from, to));

    [Fact]
    public void Includes_the_whole_last_week()
    {
        var window = new WeekWindow(24, 28);

        Assert.False(window.Contains(GestationalAge.FromWeeks(23, 6)));
        Assert.True(window.Contains(GestationalAge.FromWeeks(24)));
        Assert.True(window.Contains(GestationalAge.FromWeeks(28, 6)));
        Assert.False(window.HasPassed(GestationalAge.FromWeeks(28, 6)));
        Assert.True(window.HasPassed(GestationalAge.FromWeeks(29)));
    }
}

public sealed class LocalizedTextTests
{
    [Fact]
    public void Requires_english()
    {
        var ex = Assert.Throws<DomainException>(() => new LocalizedText(new Dictionary<string, string> { ["el"] = "Καμπύλη σακχάρου" }));
        Assert.Equal("text.english.missing", ex.Code);
    }

    [Fact]
    public void Falls_back_to_english_and_ignores_case()
    {
        var text = TestData.Text("Glucose test", "Καμπύλη σακχάρου");

        Assert.Equal("Καμπύλη σακχάρου", text.In("EL"));
        Assert.Equal("Glucose test", text.In("de"));
    }

    [Fact]
    public void Compares_by_value()
    {
        Assert.Equal(TestData.Text("A", "Α"), TestData.Text("A", "Α"));
        Assert.NotEqual(TestData.Text("A", "Α"), LocalizedText.Plain("A"));
    }
}
