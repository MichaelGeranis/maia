namespace Maia.Presentation.Accessibility;

/// <summary>
/// What one of the design's drawn controls is (pills, switches, tick circles, icon-only buttons are borders and layouts,
/// not platform controls), so a screen reader can name it: "Lock Maia, switch, off" rather than just "Lock Maia".
/// </summary>
public enum ControlRole
{
    /// <summary>Not announced as a control.</summary>
    None,

    /// <summary>Does one thing when tapped: Back, Add question, a row that opens a screen.</summary>
    Button,

    /// <summary>On or off: the design's pill switch, the setup tile that turns reminders on.</summary>
    Switch,

    /// <summary>Ticked off or not: a task, a question, a check done at the visit.</summary>
    CheckBox,

    /// <summary>One choice of a set, the others then not chosen: a reminder time, a results filter, a language.</summary>
    Radio,

    /// <summary>A bottom-bar slot; on is the screen she is on.</summary>
    Tab,

    /// <summary>A card that opens in place, such as a check on her plan; on is open.</summary>
    Expander,
}
