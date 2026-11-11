using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Android.Views;
using Maia.Presentation.Privacy;

namespace Maia.App.Platforms.Android;

/// <summary>
/// The phone's own screen lock, for "Lock Maia". Android 11 and later show the system prompt (fingerprint or face if
/// she set one up, always with her PIN, pattern or password as the way in); older phones show the screen lock's own
/// confirm screen. Maia never sees the PIN, and needs no library or permission beyond USE_BIOMETRIC.
/// </summary>
public sealed class AndroidScreenLock : IScreenLock
{
    private const int RequestCode = 4473;
    private static TaskCompletionSource<bool>? _pending;

    public bool IsSet => Keyguard?.IsDeviceSecure == true;

    private static KeyguardManager? Keyguard =>
        global::Android.App.Application.Context.GetSystemService(Context.KeyguardService) as KeyguardManager;

    public Task<bool> ConfirmAsync(string title, string subtitle) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to show the screen lock.");
            _pending?.TrySetResult(false);
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = pending;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                Prompt(activity, title, subtitle, pending);
            }
            else
            {
                Confirm(activity, title, subtitle);
            }

            return pending.Task;
        });

    public void OpenSettings()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(global::Android.Provider.Settings.ActionSecuritySettings).AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

    /// <summary>Called from <see cref="MainActivity.OnActivityResult"/> for the confirm screen of Android 10 and older.</summary>
    public static bool OnResult(int requestCode, Result resultCode)
    {
        if (requestCode != RequestCode)
        {
            return false;
        }

        _pending?.TrySetResult(resultCode == Result.Ok);
        _pending = null;
        return true;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android30.0")]
    private static void Prompt(Activity activity, string title, string subtitle, TaskCompletionSource<bool> pending)
    {
        var prompt = new BiometricPrompt.Builder(activity)
            .SetTitle(title)
            .SetSubtitle(subtitle)
            .SetAllowedAuthenticators((int)(BiometricManagerAuthenticators.BiometricWeak | BiometricManagerAuthenticators.DeviceCredential))
            .Build();
        prompt.Authenticate(new CancellationSignal(), activity.MainExecutor!, new Callback(pending));
    }

    private static void Confirm(Activity activity, string title, string subtitle)
    {
        // Android 10 and older: the confirm screen is the way to ask for the screen lock with the PIN as a fallback
        // (BiometricPrompt only takes the PIN from Android 11). It is marked obsolete from API 29 but still works there.
#pragma warning disable CA1422 // Only reached below Android 11, where it is the supported way.
        var intent = Keyguard?.CreateConfirmDeviceCredentialIntent(title, subtitle);
#pragma warning restore CA1422
        if (intent is null)
        {
            // No screen lock after all: there is nothing to confirm.
            _pending?.TrySetResult(false);
            _pending = null;
            return;
        }

        activity.StartActivityForResult(intent, RequestCode);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android28.0")]
    private sealed class Callback(TaskCompletionSource<bool> pending) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result) => pending.TrySetResult(true);

        /// <summary>Cancelled, too many tries, or no screen lock: Maia stays as it was.</summary>
        public override void OnAuthenticationError(BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString) => pending.TrySetResult(false);

        // A fingerprint that didn't match: the prompt stays up for another try, so nothing is decided yet.
    }
}

/// <summary>
/// Keeps Maia's content out of the recents screen. From Android 13 the thumbnail is simply not taken, always; older
/// Android can only hide it with FLAG_SECURE, which also blocks screenshots, so it is set only while the lock is on.
/// </summary>
public static class ScreenPrivacy
{
    public static void Apply(Activity? activity, bool lockOn)
    {
        if (activity is null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            activity.SetRecentsScreenshotEnabled(false);
        }
        else if (lockOn)
        {
            activity.Window?.AddFlags(WindowManagerFlags.Secure);
        }
        else
        {
            activity.Window?.ClearFlags(WindowManagerFlags.Secure);
        }
    }
}
