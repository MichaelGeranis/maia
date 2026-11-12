using Android.Views;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using Maia.App.Controls;
using Maia.Presentation.Accessibility;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Gives a drawn control (<see cref="Accessible"/>) the role and state TalkBack expects of the platform control it
/// looks like: a pill switch is announced as a switch that is on or off, a tick circle as a checkbox, a bottom-bar slot
/// as a tab. The state is said in the language Maia shows, and the control's name (its description) is left alone.
/// </summary>
internal static class AccessibleNodes
{
    public static void Apply(VisualElement element)
    {
        var role = Accessible.GetRole(element);
        if (role == ControlRole.None || element.Handler is not IViewHandler handler || LiveView(handler.PlatformView) is not { } view)
        {
            return;
        }

        // MAUI keeps one delegate of its own type on the view for the description and hint; this one does that too,
        // so MAUI reuses it rather than wrapping it again each time the description changes.
        var current = ViewCompat.GetAccessibilityDelegate(view);
        if (!Holds<RoleDelegate>(current))
        {
            var inner = current is MauiAccessibilityDelegateCompat maui ? maui.WrappedDelegate : current;
            ViewCompat.SetAccessibilityDelegate(view, new RoleDelegate(inner) { Handler = handler });
        }

        view.ImportantForAccessibility = ImportantForAccessibility.Yes;

        // Setting it here (not only when TalkBack asks) tells TalkBack the state changed, so "On" is said as she flips it.
        ViewCompat.SetStateDescription(view, ScreenReaderText.StateOf(role, Accessible.GetIsOn(element)));
    }

    /// <summary>
    /// Moves the screen reader's focus to <paramref name="element"/>, which it then reads out. Does nothing when no
    /// screen reader is on: the action is only taken while touch exploration is.
    /// </summary>
    public static void Focus(VisualElement element)
    {
        if (LiveView(element.Handler?.PlatformView) is { } view)
        {
            view.PerformAccessibilityAction(global::Android.Views.Accessibility.Action.AccessibilityFocus, null);
        }
    }

    /// <summary>
    /// The platform view, unless its Android view has already been let go. A timeline row can keep its binding after
    /// the list recycles the view it was shown in, so a card collapsing once a booking has saved would otherwise reach
    /// a view that is gone, and Android ends Maia.
    /// </summary>
    private static AView? LiveView(object? platformView) =>
        platformView is AView view && view.Handle != IntPtr.Zero ? view : null;

    /// <summary>Whether a delegate of type <typeparamref name="T"/> is anywhere in the chain MAUI may have wrapped.</summary>
    private static bool Holds<T>(AccessibilityDelegateCompat? @delegate)
    {
        for (var d = @delegate; d is not null; d = (d as AccessibilityDelegateCompatWrapper)?.WrappedDelegate)
        {
            if (d is T)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class RoleDelegate(AccessibilityDelegateCompat? inner) : MauiAccessibilityDelegateCompat(inner)
    {
        public override void OnInitializeAccessibilityNodeInfo(AView? host, AccessibilityNodeInfoCompat? info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (info is null || Handler?.VirtualView is not BindableObject element)
            {
                return;
            }

            var role = Accessible.GetRole(element);
            var on = Accessible.GetIsOn(element);
            var className = role switch
            {
                ControlRole.Button or ControlRole.Expander or ControlRole.Tab => "android.widget.Button",
                ControlRole.Switch => "android.widget.Switch",
                ControlRole.CheckBox => "android.widget.CheckBox",
                ControlRole.Radio => "android.widget.RadioButton",
                _ => null,
            };
            if (className is null)
            {
                return;
            }

            info.ClassName = className;

            // Every role here is tapped: TalkBack says so ("double-tap to activate") and taps it for her.
            info.AddAction(AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionClick);
            if (role is ControlRole.Switch or ControlRole.CheckBox or ControlRole.Radio)
            {
                info.Checkable = true;
                // AndroidX Core 1.19 made checked a tri-state (true, false or partial); Maia's are only ever on or off.
                info.Checked = on ? AccessibilityNodeInfoCompat.CheckedStateTrue : AccessibilityNodeInfoCompat.CheckedStateFalse;
            }

            if (ScreenReaderText.RoleName(role) is { } name)
            {
                info.RoleDescription = name;
            }

            info.StateDescription = ScreenReaderText.StateOf(role, on);
        }
    }
}
