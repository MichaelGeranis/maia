namespace Maia.App.Services;

/// <summary>
/// Whether Maia's screens should keep still (every decorative animation checks this first): the phone asks for reduced
/// motion, or she turned "Animations" off in Settings. And whether the phone is saving battery (the ambient loops hold
/// still meanwhile; a card rising or a tick popping still plays).
/// </summary>
public static class Motion
{
    private const string AnimationsKey = "motion.animations_on";

    private static bool? s_animationsOn;

    /// <summary>Raised, on any thread, when Battery Saver or her "Animations" choice changes.</summary>
    public static event EventHandler? Changed;

    /// <summary>Her "Animations" choice in Settings: on unless she turned it off. Kept in app preferences.</summary>
    public static bool AnimationsOn
    {
        get => s_animationsOn ??= Preferences.Default.Get(AnimationsKey, true);
        set
        {
            if (value == AnimationsOn)
            {
                return;
            }

            s_animationsOn = value;
            Preferences.Default.Set(AnimationsKey, value);
            OnChanged();
        }
    }

    public static bool IsReduced => !AnimationsOn || PhoneAsksForLessMotion;

    /// <summary>Whether Battery Saver is on (Android). Ambient loops pause while it is.</summary>
    public static bool IsSaving
    {
        get
        {
#if ANDROID
            return Platforms.Android.PowerSaver.IsOn;
#else
            return false;
#endif
        }
    }

    private static bool PhoneAsksForLessMotion
    {
        get
        {
#if ANDROID
            // "Remove animations" in the accessibility settings sets the animator scale to zero.
            var resolver = Android.App.Application.Context.ContentResolver;
            return Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#elif IOS || MACCATALYST
            return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif WINDOWS
            return !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#else
            return false;
#endif
        }
    }

    internal static void OnChanged() => Changed?.Invoke(null, EventArgs.Empty);
}
