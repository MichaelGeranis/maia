using System.Globalization;
using Maia.Domain.Plans;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Tests;

/// <summary>Words, dates and the bottom bar's names at their edges: a missing key, a mixed phone, a name not measured yet.</summary>
public sealed class TextEdgeTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void Maia_in_greek_on_a_phone_formatting_in_english_writes_dates_the_greek_way()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("el-GR");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("el-GR", Text.Culture.Name);
        Assert.Equal("Τρί 6 Οκτ", DisplayFormat.Date(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 3)));
    }

    [Fact]
    public void A_key_missing_in_both_languages_shows_as_itself_never_a_crash()
    {
        Assert.Equal("No_Such_Key", Text.In("No_Such_Key", Text.English));
        Assert.Equal("No_Such_Key", Text.In("No_Such_Key", Text.Greek));
    }

    [Fact]
    public void A_language_maia_does_not_ship_has_no_keys_of_its_own() =>
        Assert.Empty(Text.KeysFor(CultureInfo.GetCultureInfo("fr")));

    [Fact]
    public void A_booking_without_a_time_reads_as_its_day()
    {
        var today = new DateOnly(2026, 10, 3);

        Assert.Equal("Tue 6 Oct", DisplayFormat.Appointment(new Appointment(new DateOnly(2026, 10, 6)), today));
        Assert.Equal("Tue 6 Oct 2026 at 10:30", DisplayFormat.Appointment(new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)), new DateOnly(2028, 1, 1)));
    }

    [Fact]
    public void No_booking_has_no_words() =>
        Assert.Throws<ArgumentNullException>(() => DisplayFormat.Appointment(null!));

    [Theory]
    [InlineData("//main/week", "/")]
    [InlineData("//main/week", "//")]
    public void A_route_with_no_screen_in_it_is_never_where_she_is(string location, string route) =>
        Assert.False(NavigationGate.IsAt(location, route));

    [Fact]
    public void Names_not_measured_yet_keep_the_designs_size() =>
        Assert.Equal(TabLabelFit.DesignSize, TabLabelFit.Size(70, 8, 6, [0, 0, 0, 0, 0], 1));
}
