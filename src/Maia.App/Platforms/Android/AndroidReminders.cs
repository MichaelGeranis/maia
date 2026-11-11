using Android.App;
using Android.Content;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Maia.Application.Abstractions;
using Maia.Application.Reminders;
using Maia.Presentation.Localization;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Maia's reminders on Android: inexact alarms that may fire while the phone idles (no exact-alarm permission), and a
/// "Reminders" notification channel. The lock screen only ever shows "Maia reminder". The full words (the check or task
/// name, unless discreet titles are on, which the words already follow) go out as a secret notification, which Android
/// never shows on a secure lock screen, whatever her setting for sensitive content; when the phone is locked, a plain
/// "Maia reminder" rings for it on the lock screen, and the full one is in the shade once she unlocks.
/// </summary>
public sealed class AndroidReminderScheduler : IReminderScheduler
{
    public Task ReplaceAllAsync(IReadOnlyList<PhoneReminder> reminders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ReminderAlarms.Replace(global::Android.App.Application.Context, reminders);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Shows a reminder when its alarm goes off, and takes its plain "Maia reminder" away when she dismisses the full one.
/// Not exported: only Maia's own alarms and notifications reach it.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        if (intent.Action == ReminderAlarms.DismissedAction)
        {
            ReminderAlarms.Dismissed(context, intent);
        }
        else
        {
            ReminderAlarms.Show(context, intent);
        }
    }
}

/// <summary>
/// The phone forgets alarms when it restarts (and when Maia is updated): they are set again from the small schedule
/// Maia keeps, which holds only plain numbers, times and plain words. The next start of Maia sets them again in full.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced])]
public sealed class ReminderRestartReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && intent?.Action is Intent.ActionBootCompleted or Intent.ActionMyPackageReplaced)
        {
            ReminderAlarms.RestoreKept(context);
        }
    }
}

/// <summary>The alarms, the kept schedule and the notifications behind <see cref="AndroidReminderScheduler"/>.</summary>
internal static class ReminderAlarms
{
    public const string ChannelId = "reminders";
    public const string DismissedAction = "com.michailgeranis.maia.action.REMINDER_DISMISSED";

    private const string Action = "com.michailgeranis.maia.action.REMINDER";
    private const string PreferencesName = "maia.reminders";
    private const string ScheduleKey = "schedule";
    private const string NextNumberKey = "next";
    private const string ExtraId = "id";
    private const string ExtraTitle = "title";
    private const string ExtraText = "text";
    private const string ExtraLockScreen = "lock";

    /// <summary>The two notifications of one reminder share its number and differ by tag.</summary>
    private const string FullTag = "full";
    private const string PlainTag = "plain";

    private static readonly Lock Gate = new();

    /// <summary>
    /// The reminders this run of Maia set, with their full words: in memory only, never kept, since the words name the
    /// check or task. Null until the first <see cref="Replace"/> after Maia starts, and after a failure.
    /// </summary>
    private static List<NumberedReminder>? _set;

    /// <summary>
    /// Sets exactly these reminders (<see cref="KeptReminders"/>), with their plain version kept for after a restart.
    /// The first time after Maia starts, every alarm set before is cancelled and these are set under new plain numbers;
    /// after that only what changed is (<see cref="KeptReminders.Change"/>), and a reminder exactly as it was keeps its
    /// alarm. She is in Maia, so any plain "Maia reminder" still showing has done its job and goes; with no reminders
    /// left (turned off, or her data deleted), the full ones go too.
    /// </summary>
    public static void Replace(Context context, IReadOnlyList<PhoneReminder> reminders)
    {
        lock (Gate)
        {
            var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
            var next = Preferences.Default.Get(NextNumberKey, 1, PreferencesName);
            var set = _set;
            _set = null;
            ReminderChange change;
            if (set is null)
            {
                // What this run of Maia set is not known: every alarm kept goes, and all of these are set.
                var (numbered, after) = KeptReminders.Number(reminders, next);
                change = new ReminderChange([], [.. Load().Select(k => k.Number)], numbered, after);
            }
            else
            {
                change = KeptReminders.Change(set, reminders, next);
            }

            foreach (var number in change.Cancel)
            {
                Cancel(context, alarms, number);
            }

            ClearShown(context, alsoFull: change.All.Count == 0);
            if (change.Add.Count > 0)
            {
                EnsureChannel(context);
            }

            foreach (var (number, reminder) in change.Add)
            {
                Set(context, alarms, number, reminder.At, reminder.Title, reminder.Text, reminder.LockScreenTitle);
            }

            Save(change.All.Select(n => n.Kept));
            Preferences.Default.Set(NextNumberKey, change.Next, PreferencesName);
            _set = [.. change.All];
        }
    }

    /// <summary>After a restart: the kept reminders still ahead are set again, in their plain words.</summary>
    public static void RestoreKept(Context context)
    {
        lock (Gate)
        {
            _set = null;
            var now = DateTimeOffset.UtcNow;
            var ahead = Load().Where(k => k.At > now).ToList();
            var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
            if (ahead.Count > 0)
            {
                EnsureChannel(context);
            }

            foreach (var kept in ahead)
            {
                Set(context, alarms, kept.Number, kept.At, kept.Title, kept.Text, kept.LockScreenTitle);
            }

            Save(ahead);
        }
    }

    /// <summary>
    /// Posts the reminder its alarm carries, in full, as a secret notification: Android never shows it on a secure
    /// lock screen (under its default setting it would show a private one in full there). When the phone is locked
    /// (the screen is off, or on its lock screen), it waits silently for her to unlock, and a plain "Maia reminder"
    /// rings on the lock screen instead. An app can't make its channel hide content on the lock screen (Android keeps
    /// that for the system and for her), which is why the words are posted twice rather than once with a public
    /// version. A lock screen without a PIN, pattern or password shows even secret notifications, so on such a phone
    /// a reminder that goes off while locked is only "Maia reminder".
    /// </summary>
    public static void Show(Context context, Intent intent)
    {
        var manager = NotificationManagerCompat.From(context)!;
        if (!manager.AreNotificationsEnabled())
        {
            return;
        }

        EnsureChannel(context);
        var id = intent.GetIntExtra(ExtraId, 0);
        var title = intent.GetStringExtra(ExtraTitle) ?? string.Empty;
        var text = intent.GetStringExtra(ExtraText) ?? string.Empty;
        var lockScreen = intent.GetStringExtra(ExtraLockScreen) ?? string.Empty;
        var keyguard = (KeyguardManager?)context.GetSystemService(Context.KeyguardService);
        var locked = keyguard?.IsKeyguardLocked ?? true;
        var lockHidesSecrets = keyguard?.IsDeviceSecure ?? false;

        if (!locked || lockHidesSecrets)
        {
            var full = Builder(context);
            full.SetContentTitle(title);
            full.SetContentText(text);
            full.SetStyle(new NotificationCompat.BigTextStyle().BigText(text));
            full.SetVisibility(NotificationCompat.VisibilitySecret);
            full.SetSilent(locked);
            full.SetDeleteIntent(DismissedIntent(context, id));
            manager.Notify(FullTag, id, full.Build());
        }

        if (locked)
        {
            var plain = Builder(context);
            plain.SetContentTitle(lockScreen);
            plain.SetVisibility(NotificationCompat.VisibilityPublic);
            manager.Notify(PlainTag, id, plain.Build());
        }
    }

    /// <summary>She dismissed a reminder's full words: its plain "Maia reminder", if any, goes with them.</summary>
    public static void Dismissed(Context context, Intent intent) =>
        NotificationManagerCompat.From(context)!.Cancel(PlainTag, intent.GetIntExtra(ExtraId, 0));

    /// <summary>
    /// She is back in Maia, so the phone is unlocked: a plain "Maia reminder" still showing has done its job and goes
    /// (tapping the full one opens Maia without telling its plain one). The full ones stay until she clears them.
    /// </summary>
    public static void ClearPlain(Context context) => ClearShown(context, alsoFull: false);

    /// <summary>The "Reminders" channel, named in her language. Creating it again only updates its name.</summary>
    public static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, Text.Get("Reminders_Channel"), NotificationImportance.Default)
        {
            Description = Text.Get("Reminders_ChannelDescription"),
        };
        ((NotificationManager)context.GetSystemService(Context.NotificationService)!).CreateNotificationChannel(channel);
    }

    private static NotificationCompat.Builder Builder(Context context)
    {
        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetSmallIcon(Resource.Drawable.maia_notification);
        builder.SetColor(ContextCompat.GetColor(context, Resource.Color.colorPrimary));
        builder.SetCategory(NotificationCompat.CategoryReminder);
        builder.SetContentIntent(OpenMaia(context));
        builder.SetAutoCancel(true);
        return builder;
    }

    private static void Set(Context context, AlarmManager alarms, int number, DateTimeOffset at, string title, string text, string lockScreen)
    {
        var intent = new Intent(context, typeof(ReminderReceiver))
            .SetAction(Action)!
            .PutExtra(ExtraId, number)!
            .PutExtra(ExtraTitle, title)!
            .PutExtra(ExtraText, text)!
            .PutExtra(ExtraLockScreen, lockScreen)!;
        var pending = PendingIntent.GetBroadcast(context, number, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        // Inexact, and allowed while the phone idles, so no exact-alarm permission: Android may bring it up to an hour
        // late (ReminderPlanner.MayArriveLate), which is why the words name the day and time, never how long is left.
        alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, at.ToUnixTimeMilliseconds(), pending);
    }

    private static void Cancel(Context context, AlarmManager alarms, int number)
    {
        var intent = new Intent(context, typeof(ReminderReceiver)).SetAction(Action)!;
        if (PendingIntent.GetBroadcast(context, number, intent, PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable) is { } pending)
        {
            alarms.Cancel(pending);
            pending.Cancel();
        }
    }

    private static PendingIntent DismissedIntent(Context context, int number)
    {
        var intent = new Intent(context, typeof(ReminderReceiver)).SetAction(DismissedAction)!.PutExtra(ExtraId, number)!;
        return PendingIntent.GetBroadcast(context, number, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    /// <summary>Takes away the plain "Maia reminder"s showing, and with <paramref name="alsoFull"/> the full ones too.</summary>
    private static void ClearShown(Context context, bool alsoFull)
    {
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        foreach (var shown in manager?.GetActiveNotifications() ?? [])
        {
            if (shown.Tag == PlainTag || (alsoFull && shown.Tag == FullTag))
            {
                manager!.Cancel(shown.Tag, shown.Id);
            }
        }
    }

    private static PendingIntent OpenMaia(Context context)
    {
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!) ?? new Intent(context, typeof(MainActivity));
        launch.SetFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded);
        return PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    /// <summary>The kept schedule (<see cref="KeptReminders"/>): plain numbers, times and plain words. Never a name.</summary>
    private static IReadOnlyList<KeptReminder> Load() =>
        KeptReminders.Read(Preferences.Default.Get(ScheduleKey, string.Empty, PreferencesName));

    private static void Save(IEnumerable<KeptReminder> kept) =>
        Preferences.Default.Set(ScheduleKey, KeptReminders.Write(kept), PreferencesName);
}
