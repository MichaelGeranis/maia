using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Maia.Presentation.Tests;

/// <summary>
/// The app's colour and style keys: every colour a status or a drawn control asks for is a colour in the palette, and
/// no key hides another (a style and a colour both called "Card" once left booked cards without their white).
/// </summary>
public sealed class DesignResourceTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    [Fact]
    public void No_resource_key_is_defined_twice_across_the_app_and_its_styles()
    {
        var files = new[] { Repo.App("App.xaml") }.Concat(Directory.GetFiles(Repo.App("Resources", "Styles"), "*.xaml"));
        var keys = files
            .SelectMany(file => XDocument.Load(file).Descendants()
                .Select(element => (string?)element.Attribute(Xaml + "Key"))
                .OfType<string>()
                .Select(key => (Key: key, File: Path.GetFileName(file))))
            .ToList();

        Assert.NotEmpty(keys);
        Assert.Empty(keys.GroupBy(k => k.Key).Where(g => g.Count() > 1).Select(g => $"{g.Key}: {string.Join(", ", g.Select(k => k.File))}"));
    }

    [Fact]
    public void Every_colour_a_status_converter_picks_is_a_colour_in_the_palette()
    {
        var colours = PaletteColours();
        var code = Repo.CodeOf(Repo.App("Converters", "StatusConverters.cs"));
        var picked = Regex.Matches(code, "(?:=>|\\?|:)\\s*\"(\\w+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.Contains("White", picked);
        Assert.All(picked, key => Assert.Contains(key, colours));
    }

    [Fact]
    public void Every_colour_drawn_in_code_is_a_colour_in_the_palette()
    {
        var colours = PaletteColours();

        // Palette.Get("Sage"), and each key in Palette.Get(active ? "Pine" : "Muted").
        var used = Repo.AppSources()
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "Palette\\.Get\\(([^()]*)\\)"))
            .SelectMany(call => Regex.Matches(call.Groups[1].Value, "\"(\\w+)\"").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, colours));
    }

    [Fact]
    public void The_light_only_app_styles_its_controls_without_theme_bindings_or_label_states()
    {
        var styles = Repo.App("Resources", "Styles");
        var themeBound = Directory.GetFiles(styles, "*.xaml").Append(Repo.App("App.xaml"))
            .SelectMany(file => XDocument.Load(file).Descendants().Attributes()
                .Where(value => value.Value.Contains("{AppThemeBinding", StringComparison.Ordinal))
                .Select(value => $"{Path.GetFileName(file)}: {value.Parent!.Attribute("Property")?.Value ?? value.Name.LocalName}"));
        var label = XDocument.Load(Path.Combine(styles, "Styles.xaml")).Root!.Elements()
            .Single(style => (string?)style.Attribute("TargetType") == "Label" && style.Attribute(Xaml + "Key") is null);

        Assert.Empty(themeBound);
        Assert.DoesNotContain(label.Elements(), setter => (string?)setter.Attribute("Property") == "VisualStateManager.VisualStateGroups");
    }

    [Fact]
    public void No_text_sets_a_line_height_and_dates_read_as_the_design_writes_them()
    {
        // The font already sets its lines generously apart; a multiplier on top read as gaps.
        var xaml = Directory.GetFiles(Repo.App(), "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        var setting = xaml
            .Where(file => XDocument.Load(file).Descendants().Any(e =>
                e.Attributes().Any(a => a.Name.LocalName == "LineHeight") || (string?)e.Attribute("Property") == "LineHeight"))
            .Select(Path.GetFileName);
        var datePicker = XDocument.Load(Repo.App("Resources", "Styles", "Styles.xaml")).Root!.Elements()
            .Single(style => (string?)style.Attribute("TargetType") == "DatePicker" && style.Attribute(Xaml + "Key") is null);

        Assert.Empty(setting);
        Assert.DoesNotContain("LineHeight", Repo.CodeOf(Repo.App("Controls", "ToastView.cs")), StringComparison.Ordinal);
        Assert.Contains(datePicker.Elements(), s => (string?)s.Attribute("Property") == "Format" && (string?)s.Attribute("Value") == "ddd d MMM");
    }

    /// <summary>Maia's fourteen colours (docs/design/README.md, Colour), as the design writes them.</summary>
    private static readonly Dictionary<string, string> Fourteen = new(StringComparer.Ordinal)
    {
        ["Pine"] = "2E5A4C", ["Sage"] = "4E8A70", ["SageTint"] = "E1ECE3",
        ["SkyDeep"] = "1F4F5B", ["Sea"] = "2F6F7E", ["Sky"] = "DCEBEE",
        ["SandDeep"] = "5C4611", ["Sun"] = "E9C979", ["Sand"] = "F5EAC8",
        ["Ink"] = "1E2D29", ["Muted"] = "56665F", ["Line"] = "DDE7DF", ["Mist"] = "F2F5EF", ["White"] = "FFFFFF",
    };

    [Fact]
    public void The_palette_is_the_fourteen_colours_and_partly_transparent_copies_of_them()
    {
        var colours = XDocument.Load(Repo.App("Resources", "Styles", "Colors.xaml")).Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .ToDictionary(element => (string)element.Attribute(Xaml + "Key")!, element => element.Value.Trim().TrimStart('#').ToUpperInvariant());

        Assert.All(Fourteen, colour => Assert.Equal(colour.Value, colours.GetValueOrDefault(colour.Key)));
        var others = colours.Where(colour => !Fourteen.ContainsKey(colour.Key)).ToList();
        Assert.All(others, colour => Assert.True(
            colour.Value.Length == 8 && Fourteen.ContainsValue(colour.Value[2..]),
            $"{colour.Key} #{colour.Value} is not one of the fourteen at some opacity"));
    }

    [Fact]
    public void No_screen_or_drawing_carries_a_colour_of_its_own()
    {
        var hex = new Regex(@"#[0-9A-Fa-f]{6}\b");
        var xaml = Directory.GetFiles(Repo.App(), "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.EndsWith("Colors.xaml", StringComparison.Ordinal))
            .Where(file => hex.IsMatch(File.ReadAllText(file)))
            .Select(Path.GetFileName);
        var code = Repo.AppSources()
            .Where(file => hex.IsMatch(Repo.CodeOf(file)) || Regex.IsMatch(Repo.CodeOf(file), @"Color\.From(Rgb|Rgba|Argb|Hex)|Colors\.(?!Transparent\b)[A-Z]"))
            .Select(Path.GetFileName);

        Assert.Empty(xaml);
        Assert.Empty(code);
    }

    [Fact]
    public void No_screen_sets_a_text_size_of_its_own()
    {
        // The six sizes live in the styles (docs/design/README.md, Type). The bar's five names are the one exception:
        // they shrink together to fit (BottomBar, TabLabelFit).
        var xaml = new[] { "Views", "Controls" }
            .SelectMany(dir => Directory.GetFiles(Repo.App(dir), "*.xaml"))
            .Where(file => XDocument.Load(file).Descendants().Any(e => e.Attribute("FontSize") is not null))
            .Select(Path.GetFileName);
        var code = Repo.AppSources()
            .Where(file => !file.EndsWith("BottomBar.xaml.cs", StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(Repo.CodeOf(file), @"FontSize\s*="))
            .Select(Path.GetFileName);

        Assert.Empty(xaml);
        Assert.Empty(code);
    }

    [Fact]
    public void Every_icon_is_from_the_one_set_drawn_at_one_weight_and_three_sizes()
    {
        var icons = XDocument.Load(Repo.App("Resources", "Styles", "Icons.xaml")).Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var drawn = new[] { "Views", "Controls" }
            .SelectMany(dir => Directory.GetFiles(Repo.App(dir), "*.xaml"))
            .SelectMany(file => XDocument.Load(file).Descendants().Where(e => e.Name.LocalName == "Icon")
                .Select(icon => (File: Path.GetFileName(file), Icon: icon)))
            .ToList();
        var used = Repo.AppSources().Concat(Directory.GetFiles(Repo.App(), "*.xaml", SearchOption.AllDirectories))
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"(?:StaticResource |Geometry\("")(Icon[A-Z]\w*)").Select(m => m.Groups[1].Value))
            .Distinct();

        // Android's path reader draws some arcs wrongly or not at all: the set is written in lines and curves.
        var arcs = XDocument.Load(Repo.App("Resources", "Styles", "Icons.xaml")).Descendants()
            .Where(e => (string?)e.Attribute("Figures") is { } figures && Regex.IsMatch(figures, "[Aa]"))
            .Select(e => (string?)e.Attribute(Xaml + "Key"));

        Assert.NotEmpty(drawn);
        Assert.Empty(arcs);
        Assert.All(used, key => Assert.Contains(key, icons));
        Assert.Empty(drawn.Where(d => d.Icon.Attribute("StrokeThickness") is not null).Select(d => d.File));
        Assert.Empty(drawn.Where(d => (string?)d.Icon.Attribute("Size") is { } size && size is not ("16" or "18" or "24")).Select(d => d.File));
    }

    [Fact]
    public void Every_button_style_is_a_pill()
    {
        var styles = XDocument.Load(Repo.App("Resources", "Styles", "Maia.xaml")).Root!.Elements()
            .Where(style => (string?)style.Attribute("TargetType") == "Button")
            .ToDictionary(style => (string)style.Attribute(Xaml + "Key")!, style => style);

        string? Setter(XElement style, string property) =>
            (string?)style.Elements().FirstOrDefault(s => (string?)s.Attribute("Property") == property)?.Attribute("Value")
            ?? (Regex.Match((string?)style.Attribute("BasedOn") ?? string.Empty, @"StaticResource (\w+)") is { Success: true } basedOn
                ? Setter(styles[basedOn.Groups[1].Value], property)
                : null);

        var square = styles
            .Where(style => Setter(style.Value, "BackgroundColor") != "Transparent")
            .Where(style => double.Parse(Setter(style.Value, "CornerRadius") ?? "0", System.Globalization.CultureInfo.InvariantCulture) * 2
                < double.Parse(Setter(style.Value, "MinimumHeightRequest") ?? "44", System.Globalization.CultureInfo.InvariantCulture))
            .Select(style => style.Key);

        Assert.Empty(square);
    }

    private static HashSet<string> PaletteColours() =>
        XDocument.Load(Repo.App("Resources", "Styles", "Colors.xaml")).Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
}
