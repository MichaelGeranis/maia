using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Maia.App.Platforms.Android;

namespace Maia.App;

// Connecting a keyboard (Keyboard, KeyboardHidden) no longer remakes the activity. A change of font size or language
// still does, and the keyboard watcher follows the new window (SoftKeyboard).
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// A share that starts Maia: its files are taken in before the first screen decides where to go. Her plan never shows
    /// in the recents screen (see <see cref="ScreenPrivacy"/>), and no screen is veiled grey by a focused container
    /// (see <see cref="FocusVeil"/>).
    /// </summary>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        ScreenPrivacy.Apply(this, new Services.PreferencesAppLockPreference().IsOn);
        if (savedInstanceState is null)
        {
            SharedIntents.Receive(this, Intent, open: false);
        }

        base.OnCreate(savedInstanceState);
        FocusVeil.KeepOffScreenContainers(this);
    }

    /// <summary>
    /// MAUI lets go of the window's services as Maia goes away (on stop when she leaves with Back, on destroy when Android
    /// remakes the screen), before the screens' views are taken down. A field still focused then loses focus, and MAUI's
    /// focus mapper asks those services for something and ends Maia. So the field lets go first, while they are there.
    /// </summary>
    protected override void OnStop()
    {
        if (IsFinishing)
        {
            LetGoOfFocus();
        }

        base.OnStop();
    }

    protected override void OnDestroy()
    {
        LetGoOfFocus();
        base.OnDestroy();
    }

    /// <summary>The focused field lets go, and nothing on the screen takes focus after it on the way out.</summary>
    private void LetGoOfFocus()
    {
        if (Window?.DecorView is Android.Views.ViewGroup decor)
        {
            decor.DescendantFocusability = Android.Views.DescendantFocusability.BlockDescendants;
        }

        CurrentFocus?.ClearFocus();
    }

    /// <summary>A share while Maia is running: Results opens to file it.</summary>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        SharedIntents.Receive(this, intent, open: true);
#if DEBUG
        CaptureSample.Receive(intent);
#endif
    }

    /// <summary>
    /// Hands the phone-number and document pickers, the camera and the screen lock's confirm screen their results (see
    /// <see cref="PhonePicker"/>).
    /// </summary>
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (!PhonePicker.OnResult(requestCode, resultCode, data)
            && !DocumentPicker.OnResult(requestCode, resultCode, data)
            && !CameraCapture.OnResult(requestCode, resultCode)
            && !AndroidScreenLock.OnResult(requestCode, resultCode))
        {
            base.OnActivityResult(requestCode, resultCode, data);
        }
    }
}
