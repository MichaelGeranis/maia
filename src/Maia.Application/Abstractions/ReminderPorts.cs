namespace Maia.Application.Abstractions;

/// <summary>When she wants to be reminded of a booking or a timed task (Settings, the Reminders card).</summary>
public enum ReminderPreference
{
    /// <summary>24 hours before a booking with a time; the evening before one with only a day. Once "Evening before".</summary>
    DayBefore = 0,
    TwoHoursBefore = 1,
    Both = 2,
}

/// <summary>Her choices for the reminders on this phone. Whether they are on at all is kept apart.</summary>
public sealed record ReminderSettings
{
    /// <summary>Hour, local time, of the evening-before reminder for a booking with only a day (no time).</summary>
    public const int EveningReminderHour = 20;

    public static readonly ReminderSettings Default = new();

    /// <summary>When she is reminded of a booking or a timed task.</summary>
    public ReminderPreference When { get; init; } = ReminderPreference.DayBefore;

    /// <summary>
    /// "Discreet lock screen": plain words ("Appointment", "Reminder") with no check or task name, even on an unlocked
    /// phone. The lock screen itself only ever shows "Maia reminder".
    /// </summary>
    public bool DiscreetTitles { get; init; }
}

/// <summary>
/// One reminder Maia sets on this phone: a notification at a time. It carries its words already written, in the plan's
/// language: the full ones for an unlocked phone, and plain ones that never name a check or task, for the lock screen and
/// for bringing reminders back after the phone restarts.
/// </summary>
/// <param name="Key">
/// What it is for, such as <c>check:ogtt:daybefore</c>, for ordering and tests. It names a check, so an adapter never
/// keeps it, nor anything worked out from it such as a hash: the phone knows each reminder by a plain number from
/// <see cref="Reminders.KeptReminders"/>.
/// </param>
/// <param name="At">When it goes off.</param>
/// <param name="Title">"Glucose test", or a plain word ("Appointment") while discreet titles are on.</param>
/// <param name="Text">"Tomorrow at 08:30".</param>
/// <param name="DiscreetTitle">The title without any name: what is kept to set it again after a restart.</param>
/// <param name="DiscreetText">The text without any name.</param>
/// <param name="LockScreenTitle">All the lock screen ever shows: "Maia reminder".</param>
public sealed record PhoneReminder(
    string Key,
    DateTimeOffset At,
    string Title,
    string Text,
    string DiscreetTitle,
    string DiscreetText,
    string LockScreenTitle);

/// <summary>
/// The phone's alarms and notifications for Maia's reminders (Android: inexact alarms and a "Reminders" channel; other
/// platforms: none yet). Calls come one at a time from <see cref="Reminders.PhoneReminders"/>, off the UI thread.
/// </summary>
public interface IReminderScheduler
{
    /// <summary>
    /// Sets exactly these reminders, replacing every one Maia set before; an empty list cancels them all. What it keeps
    /// to set them again after a restart holds only plain numbers, times and the plain words
    /// (<see cref="Reminders.KeptReminders"/>), never a check's or task's name or anything worked out from one.
    /// </summary>
    Task ReplaceAllAsync(IReadOnlyList<PhoneReminder> reminders, CancellationToken cancellationToken);
}

/// <summary>
/// Whether she wants reminders on this phone, and when (device preferences in the app). Off until she turns them on.
/// </summary>
public interface IReminderSettingsStore
{
    Task<bool> IsOnAsync(CancellationToken cancellationToken);

    Task SetOnAsync(bool enabled, CancellationToken cancellationToken);

    Task<ReminderSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(ReminderSettings settings, CancellationToken cancellationToken);
}
