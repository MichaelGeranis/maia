using System.Xml.Linq;
using Maia.Presentation.Localization;
using Maia.Presentation.Settings;

namespace Maia.Presentation.Tests;

/// <summary>"Animations" in Settings: on by default; off, Maia's screens keep still.</summary>
public sealed class AnimationSettingTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public void Animations_are_on_until_she_turns_them_off_and_the_choice_is_kept()
    {
        var settings = _app.CreateSettings();

        Assert.True(settings.Animations.IsOn);

        settings.Animations.IsOn = false;

        Assert.False(_app.Animations.AnimationsOn);
        Assert.False(_app.CreateSettings().Animations.IsOn);
    }

    [Fact]
    public void Turning_animations_back_on_is_kept_too()
    {
        _app.Animations.AnimationsOn = false;
        var settings = _app.CreateSettings();

        settings.Animations.IsOn = true;

        Assert.True(_app.Animations.AnimationsOn);
    }

    [Fact]
    public void Settings_has_an_animations_switch_named_for_screen_readers()
    {
        var page = XDocument.Load(Repo.App("Views", "SettingsPage.xaml"));
        var toggle = page.Descendants().Single(e => AccessibilityTests.Attr(e, "IsToggled") == "{Binding Animations.IsOn}");

        Assert.Equal("PillSwitch", toggle.Name.LocalName);
        Assert.Equal("{loc:Translate Settings_Animations}", AccessibilityTests.Attr(toggle, "Label"));
    }

    [Theory]
    [InlineData("en-US", "Animations")]
    [InlineData("el-GR", "Κινούμενα εφέ")]
    public void Its_name_reads_in_both_languages(string culture, string name)
    {
        using var _ = new CultureScope(culture);

        Assert.Equal(name, Text.Get("Settings_Animations"));
    }

    [Fact]
    public void Every_animation_asks_one_place_that_hears_her_choice_and_the_phones()
    {
        // Motion.IsReduced is what every animation checks; it must include her choice, and changing the choice must
        // tell the running background loops at once.
        var motion = Repo.CodeOf(Repo.App("Services", "Motion.cs"));

        Assert.Contains("public static bool IsReduced => !AnimationsOn || PhoneAsksForLessMotion;", motion, StringComparison.Ordinal);
        Assert.Contains("OnChanged();", motion[motion.IndexOf("public static bool AnimationsOn", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("Motion.Changed", Repo.CodeOf(Repo.App("Controls", "AmbientLoop.cs")), StringComparison.Ordinal);
    }
}
