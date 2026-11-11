using System.Xml.Linq;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Tests;

/// <summary>
/// What the owner saw on her phone: words cut short in a pill, task buttons that left the row half empty, and ambient
/// motion that stopped under her finger as she scrolled.
/// </summary>
public sealed class ScreenFitTests
{
    [Fact]
    public void A_choice_pill_wraps_to_the_next_line_rather_than_shrink_and_cut_its_words()
    {
        // FlexLayout squeezed the last pill of a nearly full line, so Add's "Task" read "Tas".
        var pill = Repo.CodeOf(Repo.App("Controls", "ChoicePill.cs"));

        Assert.Contains("FlexLayout.SetShrink(this, 0);", pill, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tasks_save_cancel_and_delete_share_the_whole_row()
    {
        var row = XDocument.Load(Repo.App("Controls", "TaskRowView.xaml"));
        var save = row.Descendants().Single(e => AccessibilityTests.Attr(e, "Command") == "{Binding SaveCommand}" && e.Name.LocalName == "Button");
        var buttons = save.Parent!;

        Assert.Equal("Grid", buttons.Name.LocalName);
        Assert.Equal("*,*,*", AccessibilityTests.Attr(buttons, "ColumnDefinitions"));
        Assert.Equal(
            new[] { "{Binding SaveCommand}", "{Binding CancelCommand}", "{Binding AskDeleteCommand}" },
            buttons.Elements().Select(b => AccessibilityTests.Attr(b, "Command")));
        Assert.Equal(new[] { "0", "1", "2" }, buttons.Elements().Select(b => AccessibilityTests.Attr(b, "Grid.Column") ?? "0"));
        Assert.All(buttons.Elements(), b => Assert.Null(AccessibilityTests.Attr(b, "HorizontalOptions")));
    }

    [Fact]
    public void Ambient_motion_keeps_moving_while_she_scrolls()
    {
        Assert.DoesNotContain("HoldForScroll", Repo.CodeOf(Repo.App("Controls", "AmbientLoop.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("HoldForScroll", Repo.CodeOf(Repo.App("Controls", "MaiaPage.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Tasks_from_before_today_are_called_past()
    {
        Assert.Equal(("Past", "Περασμένες"), (Text.In("Tasks_Earlier", Text.English), Text.In("Tasks_Earlier", Text.Greek)));
    }
}
