using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Maia.Presentation.Tests;

/// <summary>
/// What TalkBack can know about every screen, read from the app's XAML: each tappable thing says what kind of control
/// it is, each control has a name, each field says what goes in it, and a password is never read out.
/// </summary>
public sealed class AccessibilityTests
{
    [Fact]
    public void Every_switch_is_named_after_what_it_turns_on()
    {
        var switches = Xaml().Where(x => x.Element.Name.LocalName == "PillSwitch").ToList();

        Assert.NotEmpty(switches);
        Assert.All(switches, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(Attr(x.Element, "Label")), $"{x.File}: a switch without a Label");

            // The switch speaks its own name and state; a description set here would be read instead of either.
            Assert.Null(Attr(x.Element, "SemanticProperties.Description"));
            Assert.Null(Attr(x.Element, "SemanticProperties.Hint"));
        });
    }

    [Fact]
    public void Everything_she_can_tap_says_what_kind_of_control_it_is()
    {
        var tappable = Xaml().Where(x => x.Element.Elements()
            .Any(child => child.Name.LocalName.EndsWith(".GestureRecognizers", StringComparison.Ordinal)
                && child.Elements().Any(gesture => gesture.Name.LocalName == "TapGestureRecognizer")))
            .ToList();

        Assert.NotEmpty(tappable);
        Assert.Empty(tappable.Where(x => Attr(x.Element, "Accessible.Role") is null).Select(Describe));
    }

    [Fact]
    public void A_control_without_words_of_its_own_has_a_spoken_name()
    {
        var controls = Xaml().Where(x => Attr(x.Element, "Accessible.Role") is not null).ToList();
        var unnamed = controls.Where(x =>
            Attr(x.Element, "SemanticProperties.Description") is null
            && Attr(x.Element, "Text") is null
            && x.Element.Name.LocalName != "Label"
            && !x.Element.Descendants().Any(d => d.Name.LocalName == "Label"));

        Assert.NotEmpty(controls);
        Assert.Empty(unnamed.Select(Describe));
    }

    [Fact]
    public void Every_field_says_what_goes_in_it()
    {
        // A password field is named by its placeholder alone (see below); every other field by its description, a
        // picker by its title.
        var fields = new[] { "Entry", "Editor", "DatePicker", "TimePicker", "Picker" };
        var unnamed = Xaml().Where(x => fields.Contains(x.Element.Name.LocalName)
            && Attr(x.Element, "IsPassword") != "True"
            && Attr(x.Element, "SemanticProperties.Description") is null
            && Attr(x.Element, "Title") is null);

        Assert.Empty(unnamed.Select(Describe));
    }

    [Fact]
    public void A_password_is_never_part_of_what_a_screen_reader_reads_out()
    {
        // MAUI puts a field's typed text into what is read out next to its description (on every Android version) or
        // its hint (below Android 8), and for a password field that text is the password itself. So a password field is
        // named only by its placeholder: Android keeps it as the field's own hint and reads it without the password.
        var named = new[] { "SemanticProperties.Description", "SemanticProperties.Hint", "AutomationProperties.Name", "AutomationProperties.HelpText" };
        var passwords = Xaml().Where(x => x.Element.Name.LocalName == "Entry" && Attr(x.Element, "IsPassword") == "True").ToList();

        Assert.True(passwords.Count >= 3, $"Only {passwords.Count} password fields found");
        Assert.All(passwords, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(Attr(x.Element, "Placeholder")), $"{Describe(x)}: a password field without a placeholder");
            Assert.All(named, name => Assert.True(Attr(x.Element, name) is null, $"{Describe(x)}: {name} would read the password out"));
        });
    }

    [Fact]
    public void Drawn_controls_speak_only_words_from_both_language_files()
    {
        var literal = new Regex("SetDescription\\([^,]+,\\s*\"|\"(On|Off)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var offenders = Directory.GetFiles(Repo.App("Controls"), "*.cs")
            .Where(file => literal.IsMatch(Repo.CodeOf(file)));

        Assert.Empty(offenders.Select(Path.GetFileName));
    }

    [Fact]
    public void A_question_asked_in_place_takes_the_screen_readers_focus_when_it_appears()
    {
        // Remove a result, unlink a contact, delete a task or question, clear answered ones, start again, delete all,
        // did you keep the backup: Sand panels shown only while a question is open, with their answers as buttons
        // (delete all answers with a round check and ×).
        var questions = Xaml().Where(x => x.Element.Name.LocalName == "Border"
                && Attr(x.Element, "BackgroundColor") == "{StaticResource Sand}"
                && (Attr(x.Element, "IsVisible")?.StartsWith("{Binding Is", StringComparison.Ordinal) ?? false)
                && x.Element.Descendants().Any(IsTappable))
            .ToList();

        Assert.True(questions.Count >= 9, $"Only {questions.Count} questions asked in place found");
        Assert.Contains(questions, x => Attr(x.Element, "IsVisible") == "{Binding IsConfirmingDelete}");
        Assert.Empty(questions.Where(x => Attr(x.Element, "Accessible.FocusWhenShown") != "True").Select(Describe));
    }

    [Fact]
    public void A_form_or_question_opened_in_place_of_a_button_takes_the_screen_readers_focus()
    {
        // She taps a button that hides itself and a form or a question opens in its place (change the due date, back up,
        // add a task, delete all, start again). TalkBack's focus was on that button, so it moves to the
        // first words or field of what opened instead of being left on nothing.
        var opened = OpenedInPlace().ToList();

        Assert.True(opened.Count >= 9, $"Only {opened.Count} forms or questions opened in place found");
        Assert.Empty(opened.Where(x => Attr(x.Panel, "Accessible.FocusWhenShown") != "True").Select(x => Describe((x.File, x.Panel))));
    }

    [Fact]
    public void When_it_closes_the_focus_goes_back_to_the_buttons_that_opened_it()
    {
        // Cancel, Keep it, Close, Save: the answer she tapped goes with the panel, so the button (or the row of buttons in
        // a task or question being changed) that it replaced takes the focus again. Not "Add a task": it also comes back
        // when she opens a task for changes, and the focus belongs there.
        var openers = OpenedInPlace()
            .Where(x => Attr(x.Trigger, "IsVisible") != "{Binding IsTaskFormOpen, Converter={StaticResource InvertedBool}}")
            .Select(x => (x.File, Element: x.Trigger))
            .Distinct()
            .ToList();

        Assert.True(openers.Count >= 7, $"Only {openers.Count} buttons that open something in place found");
        Assert.Empty(openers.Where(x => Attr(x.Element, "Accessible.FocusWhenShown") != "True").Select(Describe));
    }

    [Fact]
    public void Everything_maia_says_aloud_goes_through_one_place_so_a_focus_move_never_cuts_it_off()
    {
        // TalkBack stops speaking when the focus moves. Toasts and notes go through Accessible.SayAfterFocus, which waits
        // for the focus moves the same tap causes (Save, then "Change due date" taking the focus) before speaking.
        var speaking = Repo.AppSources()
            .Where(file => Path.GetFileName(file) != "Accessible.cs" && Repo.CodeOf(file).Contains("SemanticScreenReader", StringComparison.Ordinal))
            .Select(Path.GetFileName);

        Assert.Empty(speaking);
    }

    [Fact]
    public void Every_calm_note_and_error_line_is_read_out_when_it_appears()
    {
        var notes = Xaml().Where(x => x.Element.Name.LocalName == "Label"
                && Regex.IsMatch(Attr(x.Element, "Text") ?? string.Empty, "^\\{Binding [\\w.]*(Notice|ErrorMessage)\\}$"))
            .ToList();

        Assert.True(notes.Count >= 12, $"Only {notes.Count} notes found");
        Assert.Empty(notes.Where(x => Attr(x.Element, "Accessible.Announce") != "True").Select(Describe));
    }

    /// <summary>Every element of every screen, the bottom bar and the lock screen.</summary>
    internal static IEnumerable<(string File, XElement Element)> Xaml() =>
        Directory.GetFiles(Repo.App("Views"), "*.xaml")
            .Concat(Directory.GetFiles(Repo.App("Controls"), "*.xaml"))
            .SelectMany(file => XDocument.Load(file, LoadOptions.SetLineInfo).Descendants().Select(element => (Path.GetFileName(file), element)));

    /// <summary>An attribute by its name as written in XAML, whatever its prefix ("Accessible.Role" for controls:Accessible.Role).</summary>
    internal static string? Attr(XElement element, string name) =>
        element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;

    /// <summary>Something she can tap: a button, a drawn control with a role, or anything with a tap gesture.</summary>
    internal static bool IsTappable(XElement element) =>
        element.Name.LocalName is "Button" or "ChoicePill" or "PillSwitch" or "BackPill" or "DashedButton"
        || Attr(element, "Accessible.Role") is not null
        || element.Elements().Any(child => child.Name.LocalName.EndsWith(".GestureRecognizers", StringComparison.Ordinal));

    /// <summary>
    /// A control she taps (or a row of them), shown by a binding, next to a panel of fields or buttons that a binding
    /// shows exactly when the control is hidden: the same flag inverted, or flags a view model keeps as opposites.
    /// </summary>
    private static IEnumerable<(string File, XElement Trigger, XElement Panel)> OpenedInPlace() =>
        Xaml().SelectMany(x =>
        {
            var children = x.Element.Elements().Where(child => !child.Name.LocalName.Contains('.', StringComparison.Ordinal)).ToList();
            return from trigger in children
                   where IsTappable(trigger) || HoldsControls(trigger)
                   from panel in children
                   where panel != trigger && !IsTappable(panel) && HoldsControls(panel) && AreOpposite(Attr(trigger, "IsVisible"), Attr(panel, "IsVisible"))
                   select (x.File, trigger, panel);
        });

    private static bool HoldsControls(XElement panel) =>
        panel.Descendants().Any(d => IsTappable(d) || d.Name.LocalName is "Entry" or "Editor" or "DatePicker" or "TimePicker" or "Picker");

    private static bool AreOpposite(string? trigger, string? panel)
    {
        // SettingsViewModel.CanStartBackup is false while the backup form is open or its "did you keep it?" question is
        // asked; a Journey card (CheckRow) shows its buttons exactly when no booking, edit or delete question is open.
        string[][] opposites =
        [
            ["CanStartBackup", "IsBackupFormOpen"], ["CanStartBackup", "IsAskingIfBackupKept"],
            ["ShowsActions", "IsChanging"], ["ShowsActions", "IsConfirmingDelete"],
        ];

        // CheckRow.CanBook picks which of two button rows a card shows ("Book" or "Edit"): one never opens the other.
        string[] alternatives = ["CanBook"];
        if (BindingOf(trigger) is not { } shown || BindingOf(panel) is not { } opened || alternatives.Contains(shown.Path))
        {
            return false;
        }

        return (shown.Path == opened.Path && shown.Inverted != opened.Inverted)
            || (!shown.Inverted && !opened.Inverted && opposites.Any(pair => pair[0] == shown.Path && pair[1] == opened.Path));
    }

    /// <summary>"{Binding Path}" or "{Binding Path, Converter={StaticResource InvertedBool}}", or nothing for any other value.</summary>
    private static (string Path, bool Inverted)? BindingOf(string? value)
    {
        var match = Regex.Match(value ?? string.Empty, "^\\{Binding ([\\w.]+)(, Converter=\\{StaticResource InvertedBool\\})?\\}$",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Success) : null;
    }

    private static string Describe((string File, XElement Element) x) =>
        $"{x.File}:{((IXmlLineInfo)x.Element).LineNumber} {x.Element.Name.LocalName}";
}

/// <summary>Whatever she types starts with a capital: every text field but a password opens a sentence-case keyboard.</summary>
public sealed class CapitalsTests
{
    [Fact]
    public void Every_field_she_writes_in_starts_with_a_capital_but_passwords()
    {
        var entries = AccessibilityTests.Xaml().Where(x => x.Element.Name.LocalName is "Entry" or "Editor").ToList();

        // Blood pressure's two numbers open the number pad: they have no words to start with a capital.
        var numbers = entries.Where(x => AccessibilityTests.Attr(x.Element, "Keyboard") == "Numeric").ToList();
        Assert.Equal(["{Binding Systolic}", "{Binding Diastolic}"], numbers.Select(x => AccessibilityTests.Attr(x.Element, "Text")));
        entries = entries.Except(numbers).ToList();

        Assert.NotEmpty(entries);
        Assert.All(
            entries.Where(x => AccessibilityTests.Attr(x.Element, "IsPassword") != "True"),
            x => Assert.True(AccessibilityTests.Attr(x.Element, "Keyboard") == "Text", $"{x.File}: a field without Keyboard=\"Text\""));
        Assert.All(
            entries.Where(x => AccessibilityTests.Attr(x.Element, "IsPassword") == "True"),
            x => Assert.Null(AccessibilityTests.Attr(x.Element, "Keyboard")));
    }
}
