using System.Globalization;
using System.Text.RegularExpressions;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("Strings.resx")]
    [InlineData("Strings.el.resx")]
    public void No_string_is_written_twice(string file)
    {
        // A merge can leave an old copy of a string beside the new one; the app would then show whichever comes first.
        var path = Path.Combine(Repo.Root(), "src", "Maia.Presentation", "Resources", file);
        var twice = System.Xml.Linq.XDocument.Load(path).Root!.Elements("data")
            .GroupBy(data => (string?)data.Attribute("name"))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(twice);
    }

    [Fact]
    public void Every_english_text_has_a_greek_translation()
    {
        var english = Text.KeysFor(CultureInfo.InvariantCulture);
        var greek = Text.KeysFor(CultureInfo.GetCultureInfo("el"));

        Assert.NotEmpty(english);
        Assert.Empty(english.Except(greek));
        Assert.Empty(greek.Except(english));
    }

    [Fact]
    public void Shows_greek_on_greek_phones_and_english_everywhere_else()
    {
        using (new CultureScope("el-GR"))
        {
            Assert.Equal("el", Text.Language);
            Assert.Equal("Πλάνο", Text.Get("Plan_Title"));
        }

        using (new CultureScope("de-DE"))
        {
            Assert.Equal("en", Text.Language);
            Assert.Equal("Journey", Text.Get("Plan_Title"));
        }
    }

    [Fact]
    public void Every_greek_text_has_the_same_placeholders_as_the_english_and_formats()
    {
        var english = Text.KeysFor(CultureInfo.InvariantCulture);
        var placeholder = new Regex(@"\{(\d+)(:[^}]*)?\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        Assert.All(english, key =>
        {
            var en = Text.In(key, Text.English);
            var el = Text.In(key, Text.Greek);
            var enSlots = placeholder.Matches(en).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
            var elSlots = placeholder.Matches(el).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
            Assert.True(enSlots.SetEquals(elSlots), $"'{key}': English has {string.Join(" ", enSlots)}, Greek has {string.Join(" ", elSlots)}");

            // Both format with as many values as the English asks for: a stray brace would throw here, not on her phone.
            var count = enSlots.Count == 0 ? 0 : placeholder.Matches(en).Max(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) + 1;
            var values = Enumerable.Range(0, count).Select(i => (object)("x" + i)).ToArray();
            _ = string.Format(CultureInfo.InvariantCulture, en, values);
            _ = string.Format(CultureInfo.InvariantCulture, el, values);
        });
    }

    [Fact]
    public void Text_can_be_read_in_either_language_whatever_maia_shows()
    {
        using var _ = new CultureScope("en-US");

        Assert.Equal("Iron recheck", Text.In("Extra_Iron", Text.English));
        Assert.Equal("Επανέλεγχος σιδήρου", Text.In("Extra_Iron", Text.Greek));
    }

    [Fact]
    public void Missing_keys_show_the_key_instead_of_crashing() =>
        Assert.Equal("Nope_Missing", Text.Get("Nope_Missing"));

    [Fact]
    public void Every_status_and_category_has_text()
    {
        var keys = Text.KeysFor(CultureInfo.InvariantCulture);

        Assert.All(Enum.GetValues<CheckStatus>(), s => Assert.Contains($"Status_{s}", keys));
        Assert.All(Enum.GetValues<CheckCategory>(), c => Assert.Contains($"Tag_{c}", keys));
    }
}

public sealed class DisplayFormatTests
{
    private static readonly PregnancyDating Dating = PregnancyDating.FromDueDate(TestData.DueDate);

    [Fact]
    public void Formats_weeks_in_both_languages()
    {
        using (new CultureScope("en-US"))
        {
            Assert.Equal("Week 24 + 3", DisplayFormat.WeekLabel(GestationalAge.FromWeeks(24, 3)));
        }

        using (new CultureScope("el-GR"))
        {
            Assert.Equal("Εβδομάδα 24 + 3", DisplayFormat.WeekLabel(GestationalAge.FromWeeks(24, 3)));
        }
    }

    [Fact]
    public void Windows_show_weeks_and_real_dates()
    {
        using var _ = new CultureScope("en-US");

        Assert.Equal("Weeks 24–28 · 27 Sep – 31 Oct", DisplayFormat.Window(new WeekWindow(24, 28), Dating));
        Assert.Equal("Week 25 · around 4 Oct", DisplayFormat.Window(WeekWindow.OfWeek(25), Dating));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("sq-AL")]
    [InlineData("ru-RU")]
    public void On_a_phone_in_another_language_dates_and_numbers_read_in_english(string culture)
    {
        using var _ = new CultureScope(culture);

        Assert.Equal("Mon 12 Oct", DisplayFormat.Date(new DateOnly(2026, 10, 12)));
        Assert.Equal("Weeks 28–30 · 25 Oct – 14 Nov", DisplayFormat.Window(new WeekWindow(28, 30), Dating));
        Assert.Equal("Glucose test booked for Mon 12 Oct at 08:30.", Text.Format("Toast_Booked", "Glucose test", DisplayFormat.Date(new DateOnly(2026, 10, 12)), "08:30"));
        Assert.Equal("1.5 MB", Results.ResultItem.FileSize(1536 * 1024));
        Assert.Equal("en-GB", Text.Culture.Name);
    }

    [Fact]
    public void A_date_far_from_today_reads_with_its_year_and_one_within_half_a_year_without()
    {
        var today = new DateOnly(2026, 10, 3);

        Assert.Equal("Sat 3 Oct", DisplayFormat.Date(today, today));
        Assert.Equal("Tue 5 Jan", DisplayFormat.Date(new DateOnly(2027, 1, 5), today));
        Assert.Equal("Fri 3 Oct 2031", DisplayFormat.Date(new DateOnly(2031, 10, 3), today));
        Assert.Equal("Thu 3 Oct 2024", DisplayFormat.Date(new DateOnly(2024, 10, 3), today));
        Assert.Equal("Fri 3 Oct 2031 at 09:00", DisplayFormat.Appointment(new Appointment(new DateOnly(2031, 10, 3), new TimeOnly(9, 0)), today));
        Assert.Equal("ddd d MMM", DisplayFormat.DateFieldFormat(today.AddDays(DisplayFormat.YearlessDays), today));
        Assert.Equal("ddd d MMM yyyy", DisplayFormat.DateFieldFormat(today.AddDays(DisplayFormat.YearlessDays + 1), today));
        Assert.Equal("ddd d MMM yyyy", DisplayFormat.DateFieldFormat(today.AddDays(-DisplayFormat.YearlessDays - 1), today));
        using (new CultureScope("el-GR"))
        {
            Assert.Equal("Παρ 3 Οκτ 2031", DisplayFormat.Date(new DateOnly(2031, 10, 3), today));
        }
    }

    [Fact]
    public void A_phone_in_english_or_greek_keeps_its_own_format()
    {
        using (new CultureScope("en-US"))
        {
            Assert.Equal("en-US", Text.Culture.Name);
        }

        using (new CultureScope("el-GR"))
        {
            Assert.Equal("el-GR", Text.Culture.Name);
            Assert.Equal("1,5 MB", Results.ResultItem.FileSize(1536 * 1024));
        }
    }
}

public sealed class DesignFormatTests
{
    [Theory]
    [InlineData("el-GR", "Επόμενο · αύριο", "ΕΠΟΜΕΝΟ · ΑΥΡΙΟ")]
    [InlineData("el-GR", "Εβδομάδα 20 + 0 · πρωτεΐνη", "ΕΒΔΟΜΑΔΑ 20 + 0 · ΠΡΩΤΕΪΝΗ")]
    [InlineData("el-GR", "1ο τρίμηνο", "1ο ΤΡΙΜΗΝΟ")]
    [InlineData("el-GR", "3ο τρίμηνο · 2 από 3", "3ο ΤΡΙΜΗΝΟ · 2 ΑΠΟ 3")]
    [InlineData("el-GR", "Από την 2η δόση έως την 3ης", "ΑΠΟ ΤΗΝ 2η ΔΟΣΗ ΕΩΣ ΤΗΝ 3ης")]
    [InlineData("el-GR", "24ωρη μέτρηση", "24ΩΡΗ ΜΕΤΡΗΣΗ")]
    [InlineData("en-US", "1st trimester", "1ST TRIMESTER")]
    [InlineData("en-US", "Next up · café", "NEXT UP · CAFÉ")]
    public void Greek_capitals_drop_their_accents_and_keep_an_ordinal_ending_small(string culture, string text, string caps) =>
        Assert.Equal(caps, DisplayFormat.Caps(text, System.Globalization.CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void Greek_trimester_headings_read_1o_2o_3o_in_capitals()
    {
        using var _ = new CultureScope("el-GR");
        var greek = System.Globalization.CultureInfo.GetCultureInfo("el-GR");

        Assert.Equal(
            new[] { "1ο ΤΡΙΜΗΝΟ", "2ο ΤΡΙΜΗΝΟ", "3ο ΤΡΙΜΗΝΟ" },
            new[] { "trimester1", "trimester2", "trimester3" }.Select(key => DisplayFormat.Caps(DisplayFormat.Section(key), greek)));
    }

    [Fact]
    public void Times_are_24_hour_as_in_the_design()
    {
        using var _ = new CultureScope("en-US");
        Assert.Equal("09:00", DisplayFormat.Time(new TimeOnly(9, 0)));
        Assert.Equal("Tue 6 Oct at 10:30", DisplayFormat.Appointment(new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30))));
    }

    [Fact]
    public void One_day_reads_as_one_day()
    {
        using var _ = new CultureScope("en-US");
        Assert.Equal("24 weeks and 1 day", DisplayFormat.WeeksAndDays(GestationalAge.FromWeeks(24, 1)));
        Assert.Equal("24 weeks and 3 days", DisplayFormat.WeeksAndDays(GestationalAge.FromWeeks(24, 3)));
    }

    [Fact]
    public void Repeated_check_names_get_their_week_where_they_stand_alone()
    {
        using var _ = new CultureScope("en-US");
        var plan = TestData.Plan();
        var extra = plan.AddEntry(Maia.Domain.Common.LocalizedText.Plain("Check-up"), EntryKind.Exam, EntryWhen.NotBookedInWeek(31));

        Assert.Equal("Check-up · week 25", DisplayFormat.CheckName(plan.GetCheck(TestData.Checkup25), plan.Checks, "en"));
        Assert.Equal("Check-up · week 31", DisplayFormat.CheckName(plan.GetCheck(extra), plan.Checks, "en"));
        Assert.Equal("Glucose test", DisplayFormat.CheckName(plan.GetCheck(TestData.Glucose), plan.Checks, "en"));
    }

    [Fact]
    public void English_and_greek_share_one_rounded_font_with_bold_titles()
    {
        Assert.Equal(new FontSet("RoundedBold", "RoundedRegular", "RoundedMedium", "RoundedBold"), Typography.Fonts);
    }

    [Fact]
    public void Every_bundled_font_is_registered_and_used_so_none_ships_for_nothing()
    {
        var app = Path.Combine(RepoRoot(), "src", "Maia.App");
        var registered = Regex.Matches(File.ReadAllText(Path.Combine(app, "MauiProgram.cs")), "AddFont\\(\"([^\"]+)\", \"([^\"]+)\"\\)")
            .ToDictionary(m => m.Groups[2].Value, m => m.Groups[1].Value, StringComparer.Ordinal);
        var fonts = Typography.Fonts;
        var used = new[] { fonts.Title, fonts.Text, fonts.TextSemiBold, fonts.TextBold }
            .Distinct(StringComparer.Ordinal);
        var bundled = Directory.GetFiles(Path.Combine(app, "Resources", "Fonts")).Select(Path.GetFileName);

        Assert.Equal(used.Order(StringComparer.Ordinal), registered.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(bundled.Order(StringComparer.Ordinal), registered.Values.Order(StringComparer.Ordinal));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Maia.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Maia.slnx not found above the test output.");
    }
}
