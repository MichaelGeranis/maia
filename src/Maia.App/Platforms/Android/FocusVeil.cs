using Android.App;
using Android.Views;
using AView = Android.Views.View;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Keeps Android's default focus highlight off the containers that fill Maia's screen. When the phone isn't in touch
/// mode (after a key press: a keyboard, a switch device, or entering the PIN with keys), Android draws a grey veil over
/// a focused view that has no focus look of its own. Shell's drawer layout takes focus whenever the screen under it is
/// swapped (the start-up screen opening her plan after she unlocks, setup after Delete everything), so every screen
/// showed about 7% darker, as if disabled, until focus moved on. A container that fills the screen never shows the
/// veil; Maia's buttons, fields and switches keep their own focus look.
/// </summary>
public static class FocusVeil
{
    public static void KeepOffScreenContainers(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && activity.Window?.DecorView is { } decor)
        {
            Watch(decor);
        }
    }

    /// <summary>The default focus highlight came with Android 8 (API 26).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
    private static void Watch(AView decor)
    {
        if (decor.ViewTreeObserver is { } observer)
        {
            observer.GlobalFocusChange += (_, e) => Clear(decor, e.NewFocus);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
    private static void Clear(AView decor, AView? focused)
    {
        if (focused is not ViewGroup container || !FillsScreen(decor, container) || !container.DefaultFocusHighlightEnabled)
        {
            return;
        }

        container.DefaultFocusHighlightEnabled = false;

        // The highlight is chosen as the view's state is drawn, so it goes with the next state refresh.
        container.RefreshDrawableState();
    }

    /// <summary>Nearly as wide as the window and at least half as tall: a screen or a whole page, never one control.</summary>
    private static bool FillsScreen(AView decor, AView view) =>
        decor.Width > 0 && view.Width >= decor.Width * 0.9 && view.Height >= decor.Height * 0.5;
}
