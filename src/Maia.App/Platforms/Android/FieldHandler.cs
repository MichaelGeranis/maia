using Android.Views;
using Android.Widget;
using Microsoft.Maui.Platform;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Enter in a one-line field (a keyboard's Enter key, or a keyboard that sends one for Done) does what the field's
/// return does and nothing more. MAUI leaves the key unhandled, so Android moved the focus to the control below and
/// what she typed next went nowhere (Questions, once a question is in the list).
/// </summary>
internal sealed class FieldHandler : Microsoft.Maui.Handlers.EntryHandler
{
    protected override void ConnectHandler(MauiAppCompatEditText platformView)
    {
        // MAUI's own handler for the key comes first and runs the field's return.
        base.ConnectHandler(platformView);
        platformView.EditorAction += OnEditorAction;
    }

    protected override void DisconnectHandler(MauiAppCompatEditText platformView)
    {
        platformView.EditorAction -= OnEditorAction;
        base.DisconnectHandler(platformView);
    }

    private static void OnEditorAction(object? sender, TextView.EditorActionEventArgs e)
    {
        if (e.Event is { KeyCode: Keycode.Enter, Action: KeyEventActions.Up })
        {
            e.Handled = true;
        }
    }
}
