using Maia.Application.Abstractions;
using Maia.Presentation.Settings;

namespace Maia.App.Services;

/// <summary>The phone's preferences, for her reminder choices (see <see cref="ReminderPreferences"/>).</summary>
public sealed class MauiPreferenceValues : IPreferenceValues
{
    public bool Has(string key) => Preferences.Default.ContainsKey(key);

    public int ReadInt(string key, int fallback) => Preferences.Default.Get(key, fallback);

    public bool ReadBool(string key, bool fallback) => Preferences.Default.Get(key, fallback);

    public void WriteInt(string key, int value) => Preferences.Default.Set(key, value);

    public void WriteBool(string key, bool value) => Preferences.Default.Set(key, value);
}

/// <summary>
/// Notifications permission through MAUI Essentials. Android 13 and later ask for it (POST_NOTIFICATIONS); before,
/// it is granted, but she can still switch Maia's notifications off in the phone's settings, which counts as refused.
/// Reminders are Android-only for now, so elsewhere the switch is not offered.
/// </summary>
public sealed class MauiNotificationAccess : INotificationAccess
{
    public bool IsSupported => DeviceInfo.Platform == DevicePlatform.Android;

    public Task<bool> IsGrantedAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
        await Permissions.CheckStatusAsync<Permissions.PostNotifications>() == PermissionStatus.Granted && NotificationsEnabled());

    public Task<bool> RequestAsync() => MainThread.InvokeOnMainThreadAsync(async () =>
        await Permissions.RequestAsync<Permissions.PostNotifications>() == PermissionStatus.Granted && NotificationsEnabled());

    public void OpenAppSettings() => AppInfo.Current.ShowSettingsUI();

    private static bool NotificationsEnabled()
    {
#if ANDROID
        return AndroidX.Core.App.NotificationManagerCompat.From(Android.App.Application.Context)!.AreNotificationsEnabled();
#else
        return true;
#endif
    }
}

/// <summary>Reminders on this phone are Android-only for now: elsewhere nothing is set (and the switch is not offered).</summary>
public sealed class NoReminderScheduler : IReminderScheduler
{
    public Task ReplaceAllAsync(IReadOnlyList<PhoneReminder> reminders, CancellationToken cancellationToken) => Task.CompletedTask;
}
