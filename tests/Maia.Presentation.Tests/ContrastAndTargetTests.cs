using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Maia.Presentation.Tests;

/// <summary>
/// The design's accessibility rules (docs/design/README.md, "Accessibility"), checked against the palette and every
/// screen: text at least 4.5:1 against what it sits on, the parts of a control that show its state at least 3:1,
/// touch targets at least 44 dp, and a heading at the top of every screen.
/// </summary>
public sealed class ContrastAndTargetTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    [Theory]
    // Words on the page, on cards and on the panels inside them.
    [InlineData("Ink", "Mist")]
    [InlineData("Muted", "Mist")]
    [InlineData("Muted", "Mist+CardSoft")]
    [InlineData("Muted", "Mist+CardDone")]
    [InlineData("Muted", "Mist+BarBackground")]
    [InlineData("Muted", "White")]
    [InlineData("Muted", "Sky")]
    [InlineData("Sea", "Mist")]
    [InlineData("Sea", "White")]
    [InlineData("Pine", "Mist")]
    // Chips, notes and the pine cards: each family's dark step on its light step.
    [InlineData("SkyDeep", "Sky")]
    [InlineData("SandDeep", "Sand")]
    [InlineData("Pine", "SageTint")]
    [InlineData("Muted", "Line")]
    [InlineData("HeroText", "Pine")]
    [InlineData("White", "Pine")]
    [InlineData("White", "Sea")]
    [InlineData("White", "SkyDeep")]
    [InlineData("White", "SandDeep")]
    public void Text_is_at_least_four_and_a_half_to_one_against_what_it_sits_on(string text, string background) =>
        AssertContrast(text, background, 4.5);

    [Theory]
    // A switch's track, off and on, on a card; the white knob on the off track.
    [InlineData("Muted", "White")]
    [InlineData("Muted", "Mist+CardSoft")]
    [InlineData("White", "Muted")]
    [InlineData("Sage", "White")]
    [InlineData("Sage", "Mist+CardSoft")]
    public void What_shows_a_controls_state_is_at_least_three_to_one(string part, string background) =>
        AssertContrast(part, background, 3);

    [Fact]
    public void Placeholder_text_in_fields_is_as_readable_as_body_text()
    {
        var styles = XDocument.Load(Repo.App("Resources", "Styles", "Styles.xaml"));
        var placeholders = styles.Root!.Elements()
            .Where(style => (string?)style.Attribute("TargetType") is "Entry" or "Editor")
            .SelectMany(style => style.Elements().Where(setter => (string?)setter.Attribute("Property") == "PlaceholderColor"))
            .Select(setter => Regex.Match((string)setter.Attribute("Value")!, "StaticResource (\\w+)\\}$").Groups[1].Value)
            .ToList();

        Assert.Equal(2, placeholders.Count);
        Assert.All(placeholders, key => AssertContrast(key, "White", 4.5));
    }

    [Fact]
    public void No_size_declared_on_something_she_can_tap_is_under_44_dp()
    {
        // Only what is declared: a target's real size also depends on its words and on the layout around it, which the
        // two tests below and a look at the screen on a phone (uiautomator's bounds are clipped as touches are) cover.
        var sizes = new[] { "HeightRequest", "MinimumHeightRequest", "WidthRequest", "MinimumWidthRequest" };
        var small = AccessibilityTests.Xaml()
            .Where(x => AccessibilityTests.Attr(x.Element, "Accessible.Role") is not null)
            .SelectMany(x => sizes
                .Select(size => (x.File, x.Element, Size: size, Value: AccessibilityTests.Attr(x.Element, size)))
                .Where(s => s.Value is not null && double.Parse(s.Value, CultureInfo.InvariantCulture) < 44))
            .Select(s => $"{s.File} {s.Element.Name.LocalName} {s.Size}={s.Value}");

        Assert.Empty(small);
    }

    [Fact]
    public void A_touch_area_never_reaches_past_the_layout_that_holds_it()
    {
        // Android hands a touch to a layout's children only inside the layout's own bounds, so a 44 dp target pushed
        // past its parent with negative margins is only as big as the part inside it. Move the parent's padding into
        // the target instead (the task circles on Your week, the question boxes).
        var reaching = AccessibilityTests.Xaml()
            .Where(x => AccessibilityTests.IsTappable(x.Element) &&(AccessibilityTests.Attr(x.Element, "Margin")?.Contains('-', StringComparison.Ordinal) ?? false))
            .Select(x => $"{x.File} {x.Element.Name.LocalName} Margin={AccessibilityTests.Attr(x.Element, "Margin")}");

        Assert.Empty(reaching);
    }

    [Fact]
    public void Words_she_taps_are_a_44_dp_target_however_short_they_are()
    {
        // A label or a stack of labels is only as tall as its words (about 20 dp for one line), so one she can tap
        // asks for 44 dp itself or through its style.
        var minimumOf = Styles("Maia.xaml")
            .Where(style => style.Attribute(Xaml + "Key") is not null)
            .ToDictionary(
                style => $"{{StaticResource {(string)style.Attribute(Xaml + "Key")!}}}",
                style => (string?)style.Elements().FirstOrDefault(s => (string?)s.Attribute("Property") == "MinimumHeightRequest")?.Attribute("Value"));
        var words = new[] { "Label", "VerticalStackLayout", "HorizontalStackLayout" };
        var tooShort = AccessibilityTests.Xaml()
            .Where(x => words.Contains(x.Element.Name.LocalName) && AccessibilityTests.Attr(x.Element, "Accessible.Role") is not null)
            .Where(x =>
            {
                var height = AccessibilityTests.Attr(x.Element, "MinimumHeightRequest")
                    ?? AccessibilityTests.Attr(x.Element, "HeightRequest")
                    ?? (AccessibilityTests.Attr(x.Element, "Style") is { } style ? minimumOf.GetValueOrDefault(style) : null);
                return height is null || double.Parse(height, CultureInfo.InvariantCulture) < 44;
            })
            .Select(x => $"{x.File} {x.Element.Name.LocalName} {AccessibilityTests.Attr(x.Element, "Text")}");

        Assert.Empty(tooShort);
    }

    [Fact]
    public void Drawn_pills_and_buttons_are_at_least_44_dp_high()
    {
        foreach (var control in new[] { "ChoicePill.cs", "DashedButton.cs", "SetupControls.cs" })
        {
            var heights = Regex.Matches(Repo.CodeOf(Repo.App("Controls", control)), "MinimumHeightRequest = (\\d+)")
                .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .ToList();

            Assert.NotEmpty(heights);
            Assert.All(heights, height => Assert.True(height >= 44, $"{control}: {height} dp"));
        }
    }

    [Fact]
    public void With_large_text_a_buttons_words_wrap_inside_it_instead_of_running_off()
    {
        var styles = XDocument.Load(Repo.App("Resources", "Styles", "Maia.xaml")).Root!.Elements()
            .ToDictionary(style => (string)style.Attribute(Xaml + "Key")!, style => style);
        foreach (var name in new[] { "PrimaryButton", "OutlineButton", "TextButton" })
        {
            Assert.Contains(styles[name].Elements(), setter =>
                (string?)setter.Attribute("Property") == "LineBreakMode" && (string?)setter.Attribute("Value") == "WordWrap");
        }

        // A stack measures its label as wide as it likes, so at 130 % its words ran off the button: drawn buttons hold
        // their icon and words in a grid instead.
        var stacked = AccessibilityTests.Xaml()
            .Where(x => AccessibilityTests.Attr(x.Element, "Accessible.Role") is not null)
            .Where(x => x.Element.Elements().Any(d => d.Name.LocalName == "HorizontalStackLayout"
                && d.Elements().Any(child => child.Name.LocalName == "Label")))
            .Select(x => $"{x.File} {x.Element.Name.LocalName}");

        Assert.Empty(stacked);
    }

    [Fact]
    public void Every_screen_starts_with_a_heading()
    {
        var titleStyles = new[] { "{StaticResource Title}" };
        var screens = Directory.GetFiles(Repo.App("Views"), "*.xaml");
        var without = screens.Where(file => !XDocument.Load(file).Descendants().Any(element =>
            titleStyles.Contains(AccessibilityTests.Attr(element, "Style"))
            || AccessibilityTests.Attr(element, "SemanticProperties.HeadingLevel") == "Level1"));

        Assert.NotEmpty(screens);
        Assert.Empty(without.Select(Path.GetFileName));
    }

    [Fact]
    public void Screen_titles_and_section_names_are_headings()
    {
        var styles = XDocument.Load(Repo.App("Resources", "Styles", "Maia.xaml")).Root!.Elements()
            .ToDictionary(style => (string)style.Attribute(Xaml + "Key")!, style => style);

        Assert.Equal("Level1", HeadingOf(styles["Title"]));
        Assert.Equal("Level2", HeadingOf(styles["GroupEyebrow"]));
        Assert.Equal("Eyebrow", BasedOn(styles["GroupEyebrow"]));
    }

    private static IEnumerable<XElement> Styles(string file) =>
        XDocument.Load(Repo.App("Resources", "Styles", file)).Root!.Elements().Where(element => element.Name.LocalName == "Style");

    private static string? HeadingOf(XElement style) =>
        style.Elements().FirstOrDefault(s => (string?)s.Attribute("Property") == "SemanticProperties.HeadingLevel")?.Attribute("Value")?.Value;

    private static string? BasedOn(XElement style) =>
        Regex.Match((string?)style.Attribute("BasedOn") ?? string.Empty, "StaticResource (\\w+)").Groups[1].Value;

    private static void AssertContrast(string foreground, string background, double needed)
    {
        var colours = Palette();
        var back = background.Split('+').Aggregate((R: 1d, G: 1d, B: 1d), (under, key) => Over(colours[key], under));
        var front = Over(colours[foreground], back);
        var ratio = Ratio(front, back);

        Assert.True(ratio >= needed, $"{foreground} on {background}: {ratio:0.00}:1, needs {needed}:1");
    }

    private static Dictionary<string, (double R, double G, double B, double A)> Palette() =>
        XDocument.Load(Repo.App("Resources", "Styles", "Colors.xaml")).Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .ToDictionary(element => (string)element.Attribute(Xaml + "Key")!, element => Parse(element.Value.Trim()));

    private static (double R, double G, double B, double A) Parse(string value)
    {
        if (value == "White")
        {
            return (1, 1, 1, 1);
        }

        if (value == "Black")
        {
            return (0, 0, 0, 1);
        }

        var hex = value.TrimStart('#');
        var alpha = 1d;
        if (hex.Length == 8)
        {
            alpha = Channel(hex, 0);
            hex = hex[2..];
        }

        return (Channel(hex, 0), Channel(hex, 2), Channel(hex, 4), alpha);
    }

    private static double Channel(string hex, int at) => int.Parse(hex.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;

    /// <summary>A colour laid over another, as a card at 85 % white over the misty page.</summary>
    private static (double R, double G, double B) Over((double R, double G, double B, double A) top, (double R, double G, double B) under) =>
        ((top.R * top.A) + (under.R * (1 - top.A)), (top.G * top.A) + (under.G * (1 - top.A)), (top.B * top.A) + (under.B * (1 - top.A)));

    /// <summary>WCAG 2 contrast ratio.</summary>
    private static double Ratio((double R, double G, double B) a, (double R, double G, double B) b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance((double R, double G, double B) c) =>
        (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));

    private static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
}
