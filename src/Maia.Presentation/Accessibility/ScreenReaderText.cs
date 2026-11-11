using Maia.Presentation.Localization;

namespace Maia.Presentation.Accessibility;

/// <summary>
/// What a screen reader says about a drawn control besides its name, in the language Maia shows (which may not be the
/// phone's): its state ("On", "Ticked off", "Selected", "Expanded") and, where the phone has no word of its own for
/// the kind of control, its role ("Tab").
/// </summary>
public static class ScreenReaderText
{
    /// <summary>The state said after the control's name, or null when the control has none to say.</summary>
    public static string? StateOf(ControlRole role, bool on) => role switch
    {
        ControlRole.Switch => Text.Get(on ? "Common_On" : "Common_Off"),
        ControlRole.CheckBox => Text.Get(on ? "Common_Ticked" : "Common_NotTicked"),
        ControlRole.Radio => Text.Get(on ? "Common_Selected" : "Common_NotSelected"),

        // Only the screen she is on is said to be selected; the other tabs are just tabs.
        ControlRole.Tab => on ? Text.Get("Common_Selected") : null,
        ControlRole.Expander => Text.Get(on ? "Common_Expanded" : "Common_Collapsed"),
        _ => null,
    };

    /// <summary>The kind of control, for the roles a phone has no word for itself; null for the rest.</summary>
    public static string? RoleName(ControlRole role) => role == ControlRole.Tab ? Text.Get("Common_Tab") : null;
}
