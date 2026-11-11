using System.Xml.Linq;
using Maia.Presentation.Common;

namespace Maia.Presentation.Tests;

/// <summary>A card's two buttons ("Book" or "Edit", and "Delete") share their row while each fits its half on one line, and stack once one doesn't.</summary>
public sealed class SideBySideTests
{
    [Fact]
    public void Two_buttons_stay_side_by_side_while_each_fits_on_one_line() =>
        Assert.False(SideBySide.Stacks(300, 8, [120, 146]));

    [Fact]
    public void The_buttons_stack_when_one_would_break_a_word() =>
        Assert.True(SideBySide.Stacks(300, 8, [120, 147]));

    [Fact]
    public void One_button_or_a_row_not_laid_out_yet_never_stacks()
    {
        Assert.False(SideBySide.Stacks(100, 8, [400]));
        Assert.False(SideBySide.Stacks(0, 8, [120, 400]));
    }

    [Fact]
    public void Journeys_book_or_edit_shares_a_button_row_with_delete()
    {
        var rows = XDocument.Load(Repo.App("Views", "PlanPage.xaml")).Descendants()
            .Where(e => e.Name.LocalName == "ButtonRow")
            .Select(row => row.Elements().Select(button => AccessibilityTests.Attr(button, "Command")).ToArray())
            .ToList();

        Assert.Equal(
            new[]
            {
                new[] { "{Binding StartBookCommand}", "{Binding AskDeleteCommand}" },
                new[] { "{Binding StartEditCommand}", "{Binding AskDeleteCommand}" },
            },
            rows);
    }
}
