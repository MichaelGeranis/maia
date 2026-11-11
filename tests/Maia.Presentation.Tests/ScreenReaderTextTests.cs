using Maia.Presentation.Accessibility;

namespace Maia.Presentation.Tests;

/// <summary>
/// What TalkBack says about the design's drawn controls besides their name: their state, in the language Maia shows,
/// never a hard-coded English "On" said over the switch's own name.
/// </summary>
public sealed class ScreenReaderTextTests
{
    [Fact]
    public void A_switch_says_on_or_off_in_the_language_maia_shows()
    {
        using (new CultureScope("en-GB"))
        {
            Assert.Equal("On", ScreenReaderText.StateOf(ControlRole.Switch, on: true));
            Assert.Equal("Off", ScreenReaderText.StateOf(ControlRole.Switch, on: false));
        }

        using (new CultureScope("el-GR"))
        {
            Assert.Equal("Ενεργό", ScreenReaderText.StateOf(ControlRole.Switch, on: true));
            Assert.Equal("Ανενεργό", ScreenReaderText.StateOf(ControlRole.Switch, on: false));
        }
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("el-GR")]
    public void Every_control_with_a_state_says_both_states_in_words_of_its_own(string culture)
    {
        using var _ = new CultureScope(culture);
        var roles = new[] { ControlRole.Switch, ControlRole.CheckBox, ControlRole.Radio, ControlRole.Expander };

        Assert.All(roles, role =>
        {
            var on = ScreenReaderText.StateOf(role, on: true);
            var off = ScreenReaderText.StateOf(role, on: false);

            // A missing text would come back as its key ("Common_On"): never spoken to her.
            Assert.False(string.IsNullOrWhiteSpace(on));
            Assert.False(string.IsNullOrWhiteSpace(off));
            Assert.DoesNotContain("_", on, StringComparison.Ordinal);
            Assert.DoesNotContain("_", off, StringComparison.Ordinal);
            Assert.NotEqual(on, off);
        });
    }

    [Fact]
    public void A_task_or_question_says_whether_it_is_ticked_off()
    {
        using var _ = new CultureScope("en-GB");

        Assert.Equal("Ticked off", ScreenReaderText.StateOf(ControlRole.CheckBox, on: true));
        Assert.Equal("Not ticked off", ScreenReaderText.StateOf(ControlRole.CheckBox, on: false));
    }

    [Fact]
    public void Only_the_tab_she_is_on_is_said_to_be_selected()
    {
        using var _ = new CultureScope("en-GB");

        Assert.Equal("Selected", ScreenReaderText.StateOf(ControlRole.Tab, on: true));
        Assert.Null(ScreenReaderText.StateOf(ControlRole.Tab, on: false));
    }

    [Fact]
    public void A_button_has_no_state_to_say()
    {
        Assert.Null(ScreenReaderText.StateOf(ControlRole.Button, on: true));
        Assert.Null(ScreenReaderText.StateOf(ControlRole.None, on: false));
    }

    [Fact]
    public void A_tab_is_named_a_tab_in_either_language()
    {
        using (new CultureScope("en-GB"))
        {
            Assert.Equal("Tab", ScreenReaderText.RoleName(ControlRole.Tab));
        }

        using (new CultureScope("el-GR"))
        {
            Assert.Equal("Καρτέλα", ScreenReaderText.RoleName(ControlRole.Tab));
        }

        // The phone names a switch, a checkbox or a button itself.
        Assert.Null(ScreenReaderText.RoleName(ControlRole.Switch));
    }
}
