using System.Xml.Linq;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Tests;

/// <summary>
/// The bottom bar: five flat slots of one size (Week · Journey · Add · Results · Settings), Add and Settings tabs like the
/// others with no gear left on any screen and no Team (her care team is in Settings), and five names that fit on one line
/// in English and Greek.
/// </summary>
public sealed class BottomBarTests
{
    // Each name's width at 12 sp in the bundled bold font (M PLUS Rounded 1c Bold), measured with its kerning, plus 0.5.
    private static readonly double[] English = [30.4, 46.9, 22.8, 44.2, 50.8];
    private static readonly double[] Greek = [57.9, 56.4, 58.5, 85.8, 59.9];

    private const double Edge = 6;
    private const double Overhang = 10;

    [Fact]
    public void The_bar_has_five_flat_slots_of_one_size_in_order()
    {
        var bar = XDocument.Load(Repo.App("Controls", "BottomBar.xaml"));
        var slots = bar.Descendants().Single(e => AccessibilityTests.Attr(e, "Name") == "Slots");
        var tabs = slots.Elements().Where(e => AccessibilityTests.Attr(e, "Accessible.Role") == "Tab").ToList();

        Assert.Equal("*,*,*,*,*", AccessibilityTests.Attr(slots, "ColumnDefinitions"));
        Assert.Equal(
            new[] { "{loc:Translate Tab_Week}", "{loc:Translate Tab_Journey}", "{loc:Translate Tab_AddHint}", "{loc:Translate Tab_Results}", "{loc:Translate Tab_Settings}" },
            tabs.Select(tab => AccessibilityTests.Attr(tab, "SemanticProperties.Description")));
        Assert.Equal(new[] { "0", "1", "2", "3", "4" }, tabs.Select(tab => AccessibilityTests.Attr(tab, "Grid.Column") ?? "0"));
        Assert.All(tabs, tab => Assert.Equal("{StaticResource TabItem}", AccessibilityTests.Attr(tab, "Style")));

        // No raised button any more: every control in the bar is one of the five slots.
        Assert.Equal(5, bar.Descendants().Count(e => AccessibilityTests.Attr(e, "Accessible.Role") is not null));
    }

    [Fact]
    public void The_slots_read_the_same_names_in_english_and_greek()
    {
        string[] keys = ["Tab_Week", "Tab_Journey", "Tab_Add", "Tab_Results", "Tab_Settings"];

        Assert.Equal(new[] { "Today", "Journey", "New", "Results", "Settings" }, keys.Select(key => Text.In(key, Text.English)));
        Assert.Equal(new[] { "Σήμερα", "Πλάνο", "Νέο", "Αποτελέσματα", "Ρυθμίσεις" }, keys.Select(key => Text.In(key, Text.Greek)));

        // A screen reader hears New with what it is for.
        Assert.Equal("New, something for your journey", Text.In("Tab_AddHint", Text.English));
        Assert.Equal("Νέο, κάτι για το πλάνο σου", Text.In("Tab_AddHint", Text.Greek));
    }

    [Fact]
    public void Add_and_settings_are_tabs_team_is_gone_and_no_screen_has_a_gear_any_more()
    {
        var shell = XDocument.Load(Repo.App("AppShell.xaml"));
        var tabs = shell.Descendants().Single(e => e.Name.LocalName == "TabBar").Elements()
            .Select(content => AccessibilityTests.Attr(content, "Route"));
        var settings = XDocument.Load(Repo.App("Views", "SettingsPage.xaml"));

        Assert.Equal(new[] { "week", "plan", "add", "results", "settings" }, tabs);
        Assert.False(File.Exists(Repo.App("Views", "TeamPage.xaml")));
        Assert.DoesNotContain("Tab_Team", Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture));
        Assert.DoesNotContain("Routes.Add", Repo.CodeOf(Repo.App("AppShell.xaml.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Routes.Settings", Repo.CodeOf(Repo.App("AppShell.xaml.cs")), StringComparison.Ordinal);
        Assert.Equal("settings", AccessibilityTests.Attr(settings.Root!, "Tab"));
        Assert.DoesNotContain(settings.Descendants(), e => e.Name.LocalName == "BackPill");
        Assert.False(File.Exists(Repo.App("Controls", "SettingsButton.cs")));
        Assert.DoesNotContain(AccessibilityTests.Xaml(), x => x.Element.Name.LocalName == "SettingsButton");
    }

    [Fact]
    public void Leaving_the_settings_tab_closes_what_she_left_open_there()
    {
        // A tab's page stays while she is elsewhere, so leaving it must close what she left open (SettingsViewModel.Leave),
        // and the diagnostics card on screen must be Settings' own.
        var code = Repo.CodeOf(Repo.App("Views", "SettingsPage.xaml.cs"));
        Assert.Contains("void OnDisappearing()", code, StringComparison.Ordinal);
        var leaving = code[code.IndexOf("void OnDisappearing()", StringComparison.Ordinal)..];
        leaving = leaving[..leaving.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.Contains("_viewModel.Leave();", leaving, StringComparison.Ordinal);
        Assert.Contains("DiagnosticsCard.BindingContext = viewModel.Diagnostics;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void The_add_screen_lights_the_add_slot()
    {
        var add = XDocument.Load(Repo.App("Views", "AddEntryPage.xaml"));

        Assert.Equal("add", AccessibilityTests.Attr(add.Root!, "Tab"));
    }

    [Fact]
    public void Journey_has_no_add_button_of_its_own_and_saving_on_add_is_just_save()
    {
        // The bottom bar's Add is the one way in; Journey's own "+ Add" and its string are gone.
        Assert.DoesNotContain("AddCommand", Repo.CodeOf(Repo.App("Views", "PlanPage.xaml")), StringComparison.Ordinal);
        Assert.DoesNotContain("Plan_Add", Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture));
        var save = XDocument.Load(Repo.App("Views", "AddEntryPage.xaml")).Descendants().Single(e => e.Name.LocalName == "Button");
        Assert.Equal("{loc:Translate Common_Save}", AccessibilityTests.Attr(save, "Text"));
        Assert.Equal("Save", Text.In("Common_Save", Text.English));
        Assert.Equal("Αποθήκευση", Text.In("Common_Save", Text.Greek));
    }

    /// <summary>Every phone width from a small phone to a wide one, at her phone's text sizes from small to largest.</summary>
    public static TheoryData<double, double> PhonesAndTextSizes()
    {
        var data = new TheoryData<double, double>();
        foreach (var phone in new double[] { 320, 340, 360, 384, 412 })
        {
            foreach (var textScale in new[] { 0.85, 1, 1.15, 1.3, 1.5, 2 })
            {
                data.Add(phone, textScale);
            }
        }

        return data;
    }

    [Theory]
    [InlineData(320)]
    [InlineData(360)]
    [InlineData(412)]
    public void In_english_the_names_keep_the_designs_size_on_any_phone(double phone) =>
        Assert.Equal(TabLabelFit.DesignSize, Look(phone, English, 1).Seen);

    [Theory]
    [InlineData(360)]
    [InlineData(412)]
    public void With_larger_text_the_english_names_grow_with_it(double phone) =>
        Assert.InRange(Look(phone, English, 1.3).Seen, (TabLabelFit.DesignSize * 1.3) - 0.05, TabLabelFit.DesignSize * 1.3);

    [Theory]
    [InlineData(360, 1.6)]
    [InlineData(360, 2)]
    [InlineData(412, 2)]
    public void Past_130_percent_the_names_grow_no_bigger_so_they_keep_apart(double phone, double textScale)
    {
        var look = Look(phone, English, textScale);

        Assert.True(look.Seen <= TabLabelFit.MaximumSize + 0.001, $"{phone} dp at {textScale:0.0}: the names look {look.Seen:0.0}");
        Assert.True(look.Apart >= 6, $"{phone} dp at {textScale:0.0}: two names are {look.Apart:0.0} dp apart");
    }

    [Theory]
    [InlineData(320)]
    [InlineData(360)]
    [InlineData(384)]
    public void In_greek_the_names_shrink_together_and_keep_the_full_gap_where_there_is_room(double phone)
    {
        var look = Look(phone, Greek, 1);

        Assert.InRange(look.Seen, TabLabelFit.MinimumSize, TabLabelFit.DesignSize - 0.4);
        Assert.True(look.Apart >= TabLabelFit.Gap - 0.001, $"{phone} dp: two names are {look.Apart:0.0} dp apart");
    }

    [Theory]
    [MemberData(nameof(PhonesAndTextSizes))]
    public void In_greek_no_two_names_touch_and_none_is_cut_off_at_any_text_size(double phone, double textScale)
    {
        var look = Look(phone, Greek, textScale);

        Assert.True(look.Apart >= TabLabelFit.MinimumGap - 0.001, $"{phone} dp at {textScale:0.00}: two names are {look.Apart:0.0} dp apart");
        Assert.True(look.Over <= 0.001, $"{phone} dp at {textScale:0.00}: a name runs {look.Over:0.0} dp past what its slot shows");
    }

    [Theory]
    [MemberData(nameof(PhonesAndTextSizes))]
    public void In_english_no_two_names_touch_and_none_is_cut_off_at_any_text_size(double phone, double textScale)
    {
        var look = Look(phone, English, textScale);

        Assert.True(look.Apart >= TabLabelFit.Gap - 0.001, $"{phone} dp at {textScale:0.00}: two names are {look.Apart:0.0} dp apart");
        Assert.True(look.Over <= 0.001, $"{phone} dp at {textScale:0.00}: a name runs {look.Over:0.0} dp past what its slot shows");
    }

    [Theory]
    [InlineData(320)]
    [InlineData(340)]
    [InlineData(360)]
    [InlineData(384)]
    public void Once_the_greek_names_have_shrunk_larger_text_leaves_them_looking_the_same(double phone)
    {
        var seen = Look(phone, Greek, 1).Seen;

        Assert.Equal(seen, Look(phone, Greek, 1.15).Seen, precision: 6);
        Assert.Equal(seen, Look(phone, Greek, 1.3).Seen, precision: 6);
        Assert.Equal(seen, Look(phone, Greek, 2).Seen, precision: 6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.3)]
    public void At_the_smallest_size_the_greek_names_give_up_some_of_the_gap_before_shrinking_further(double textScale)
    {
        // A 300 dp phone: the full gap would need names a little smaller than the floor, the least gap a little bigger.
        var look = Look(300, Greek, textScale);

        Assert.Equal(TabLabelFit.MinimumSize, look.Seen, precision: 6);
        Assert.InRange(look.Apart, TabLabelFit.MinimumGap, TabLabelFit.Gap);
    }

    [Fact]
    public void Only_on_phones_under_296_dp_do_the_greek_names_look_a_little_under_the_smallest_size()
    {
        var look = Look(294, Greek, 1);

        Assert.InRange(look.Seen, TabLabelFit.MinimumSize - 0.1, TabLabelFit.MinimumSize);
        Assert.Equal(TabLabelFit.MinimumGap, look.Apart, precision: 6);
    }

    [Theory]
    [InlineData(280)]
    [InlineData(290)]
    public void On_a_small_phone_the_greek_names_shrink_below_the_smallest_size_rather_than_touch(double phone)
    {
        var look = Look(phone, Greek, 1.15);

        Assert.True(look.Seen < TabLabelFit.MinimumSize);
        Assert.Equal(TabLabelFit.MinimumGap, look.Apart, precision: 6);
    }

    [Fact]
    public void A_name_never_runs_past_what_its_slot_can_show()
    {
        // Short neighbours leave room, but the long name would still be cut off at its slot plus its overhang (80 dp).
        var size = TabLabelFit.Size(60, Edge, Overhang, [10, 100, 10], 1);

        Assert.Equal(9.6, size, precision: 6);
    }

    [Fact]
    public void Before_the_bar_is_laid_out_the_names_keep_the_designs_size()
    {
        Assert.Equal(TabLabelFit.DesignSize, TabLabelFit.Size(0, Edge, Overhang, Greek, 1));
        Assert.Equal(TabLabelFit.DesignSize, TabLabelFit.Size(60, Edge, Overhang, [], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TabLabelFit.Size(60, Edge, Overhang, Greek, 0));
    }

    /// <summary>One slot's width on a phone <paramref name="phone"/> dp wide: the bar less its side padding, in five.</summary>
    private static double Slot(double phone) => (phone - (2 * Edge)) / 5;

    /// <summary>
    /// How the names show on a phone <paramref name="phone"/> dp wide with her text size at <paramref name="textScale"/>:
    /// the size she sees, the least room between two neighbours, and how far the worst name runs past what its slot
    /// shows (0 or less when every name shows whole).
    /// </summary>
    private static (double Seen, double Apart, double Over) Look(double phone, double[] names, double textScale)
    {
        var slot = Slot(phone);
        var widths = names.Select(width => width * textScale).ToArray();

        var size = TabLabelFit.Size(slot, Edge, Overhang, widths, textScale);

        var shrink = size / TabLabelFit.DesignSize;
        var apart = Enumerable.Range(0, widths.Length - 1).Min(i => slot - ((widths[i] + widths[i + 1]) / 2 * shrink));
        var over = Enumerable.Range(0, widths.Length).Max(i =>
        {
            var reach = i == 0 || i == widths.Length - 1 ? Math.Min(Overhang, Edge - TabLabelFit.Gap) : Overhang;
            return (widths[i] * shrink) - (slot + (2 * reach));
        });
        return (size * textScale, apart, over);
    }
}
