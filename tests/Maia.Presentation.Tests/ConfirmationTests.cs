using System.Globalization;
using System.Xml.Linq;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Tests;

/// <summary>
/// Every confirmation reads and looks the same: asked once more in place on a Sand strip, the question names what goes,
/// and two buttons in words follow, the action repeating the question's verb ("Delete task"), then "Keep it" or "Keep
/// them". Only where files or all her data go does it add "This can't be undone."
/// </summary>
public sealed class ConfirmationTests
{
    /// <summary>Each confirmation: its question, its action, and the screen it is on.</summary>
    public static TheoryData<string, string> Confirmations() => new()
    {
        { "Week_TaskDeleteConfirm", "Week_TaskDeleteYes" },
        { "Check_DeleteConfirm", "Check_DeleteYes" },
        { "Questions_DeleteConfirm", "Questions_DeleteYes" },
        { "Questions_ClearConfirm", "Questions_ClearYes" },
        { "Results_RemoveConfirm", "Results_RemoveYes" },
        { "Team_UnlinkConfirm", "Team_UnlinkYes" },
        { "Settings_DeleteConfirm", "Settings_DeleteYes" },
        { "Startup_StartAgainConfirm", "Startup_StartAgainYes" },
    };

    [Theory]
    [MemberData(nameof(Confirmations))]
    public void The_action_repeats_the_questions_verb(string question, string action)
    {
        var asked = Text.In(question, Text.English);
        var answer = Text.In(action, Text.English);
        var verb = answer.Split(' ')[0];

        Assert.StartsWith(verb + " ", asked, StringComparison.Ordinal);
        Assert.Contains("?", asked, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_what_takes_files_or_all_her_data_says_it_cannot_be_undone()
    {
        string[] lasting = ["Results_RemoveConfirm", "Settings_DeleteConfirm", "Startup_StartAgainConfirm"];

        foreach (var (question, _) in Pairs())
        {
            var says = Text.In(question, Text.English).EndsWith("This can't be undone.", StringComparison.Ordinal);
            var saysInGreek = Text.In(question, Text.Greek).EndsWith("Δεν αναιρείται.", StringComparison.Ordinal);
            Assert.Equal(lasting.Contains(question), says);
            Assert.Equal(says, saysInGreek);
        }
    }

    [Fact]
    public void The_safe_answer_is_keep_it_or_keep_them_in_both_languages()
    {
        Assert.Equal(("Keep it", "Keep them"), (Text.In("Common_KeepIt", Text.English), Text.In("Common_KeepThem", Text.English)));
        Assert.Equal(("Να μείνει", "Να μείνουν"), (Text.In("Common_KeepIt", Text.Greek), Text.In("Common_KeepThem", Text.Greek)));

        // The old, differently worded answers are gone.
        var keys = Text.KeysFor(CultureInfo.InvariantCulture);
        string[] gone = ["Common_Delete", "Tasks_ClearNo", "Questions_ClearNo", "Results_RemoveNo", "Team_UnlinkNo", "Settings_DeleteNo", "Startup_StartAgainNo"];
        Assert.DoesNotContain(keys, gone.Contains);
    }

    [Fact]
    public void Every_confirmation_is_a_sand_strip_with_the_action_then_keep_in_words()
    {
        var strips = AccessibilityTests.Xaml()
            .Select(x => x.Element)
            .Where(e => AccessibilityTests.Attr(e, "IsVisible") is { } shown && shown.StartsWith("{Binding IsConfirming", StringComparison.Ordinal)
                && AccessibilityTests.Attr(e, "BackgroundColor") == "{StaticResource Sand}")
            .ToList();

        // Journey entry, task, question, answered questions, result, the OB-GYN and other doctors, all data, start again.
        Assert.Equal(9, strips.Count);
        Assert.All(strips, strip =>
        {
            var buttons = strip.Descendants().Where(e => e.Name.LocalName == "Button").ToList();
            Assert.Equal("True", AccessibilityTests.Attr(strip, "Accessible.FocusWhenShown"));
            Assert.Equal(
                new[] { "{StaticResource ConfirmButton}", "{StaticResource KeepButton}" },
                buttons.Select(b => AccessibilityTests.Attr(b, "Style")));
            Assert.DoesNotContain(strip.Descendants(), e => AccessibilityTests.Attr(e, "Style") == "{StaticResource RoundButton}");
            Assert.Contains(
                AccessibilityTests.Attr(buttons[1], "Text"),
                new[] { "{loc:Translate Common_KeepIt}", "{loc:Translate Common_KeepThem}", "{Binding KeepAnsweredLabel}" });
        });
    }

    private static IEnumerable<(string Question, string Action)> Pairs() =>
        Confirmations().Select(row => ((string)row[0], (string)row[1]));
}
