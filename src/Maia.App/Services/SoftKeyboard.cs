namespace Maia.App.Services;

/// <summary>
/// Whether the on-screen keyboard is up. The bottom bar hides while she types, so the field stays in view above
/// the keyboard (pages pad themselves above it: MaiaPage uses the keyboard-aware safe area).
/// </summary>
public static class SoftKeyboard
{
#if ANDROID
    // The listener on the window being watched. It holds that window's root view, so a new window replaces it.
    private static LayoutListener? _listener;
#endif

    public static event EventHandler? VisibilityChanged;

    public static bool IsVisible { get; private set; }

    /// <summary>
    /// Watches the window <paramref name="view"/> is in. Further calls from the same window do nothing; a call from a
    /// new window (Android made the activity again, after a change of system font size or language, say) moves the
    /// watch to it and takes the listener off the old window, so the bar keeps hiding and nothing holds the old views.
    /// The layout listener only reads the keyboard inset; it doesn't replace the platform's own insets handling, which
    /// the safe areas depend on.
    /// </summary>
    public static void Watch(VisualElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
#if ANDROID
        if (view.Handler?.PlatformView is not Android.Views.View native || native.RootView is not { } root)
        {
            return;
        }

        if (_listener is { } current && current.Watches(root))
        {
            return;
        }

        _listener?.Stop();
        _listener = new LayoutListener(root);
        root.ViewTreeObserver?.AddOnGlobalLayoutListener(_listener);

        // A new window starts without the keyboard; its first layout says otherwise if it is up.
        Set(false);
#endif
    }

    /// <summary>
    /// Puts the keyboard away and lets go of whichever field has focus, as when an editor in a list row closes: the
    /// field it belonged to is gone, and Android would otherwise hand the keyboard to the next field on the page.
    /// </summary>
    /// <remarks>
    /// The field may already be gone, and with it the focus (a form hidden as its task is added): the keyboard is then
    /// put away through the window, so it never stays up over a screen with nothing to type in.
    /// </remarks>
    public static void Hide()
    {
#if ANDROID
        if (Platform.CurrentActivity is not { } activity)
        {
            return;
        }

        var focused = activity.CurrentFocus;
        if ((focused ?? activity.Window?.DecorView)?.WindowToken is { } token)
        {
            var keyboard = (Android.Views.InputMethods.InputMethodManager?)activity.GetSystemService(Android.Content.Context.InputMethodService);
            keyboard?.HideSoftInputFromWindow(token, Android.Views.InputMethods.HideSoftInputFlags.None);
        }

        focused?.ClearFocus();
#endif
    }

    private static void Set(bool visible)
    {
        if (visible == IsVisible)
        {
            return;
        }

        IsVisible = visible;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // The keyboard went down (Back, or its own button): the field lets go too, before the bar comes back, so the
            // screen doesn't scroll to keep the field in view as it changes size, and stays where she sees it.
            if (!visible)
            {
                LetGoOfField();
            }

            VisibilityChanged?.Invoke(null, EventArgs.Empty);
        });
    }

    private static void LetGoOfField()
    {
#if ANDROID
        if (Platform.CurrentActivity?.CurrentFocus is Android.Widget.EditText field)
        {
            field.ClearFocus();
        }
#endif
    }

#if ANDROID
    private sealed class LayoutListener(Android.Views.View root) : Java.Lang.Object, Android.Views.ViewTreeObserver.IOnGlobalLayoutListener
    {
        /// <summary>Whether <paramref name="view"/> is the root this listener watches (the same Android view, not just an equal one).</summary>
        public bool Watches(Android.Views.View view) => Android.Runtime.JNIEnv.IsSameObject(root.Handle, view.Handle);

        /// <summary>Comes off its window's layout observer, if that window is still there to take it off.</summary>
        public void Stop()
        {
            if (root.ViewTreeObserver is { IsAlive: true } observer)
            {
                observer.RemoveOnGlobalLayoutListener(this);
            }
        }

        public void OnGlobalLayout()
        {
            if (AndroidX.Core.View.ViewCompat.GetRootWindowInsets(root) is { } insets)
            {
                Set(insets.IsVisible(AndroidX.Core.View.WindowInsetsCompat.Type.Ime()));
            }
        }
    }
#endif
}
