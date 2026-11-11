using System.Globalization;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;

namespace Maia.Application.Reminders;

/// <summary>
/// Which reminders a plan needs on the phone. Pure: no I/O and no clock, so the rules are fully unit-tested.
/// <list type="bullet">
/// <item>Each upcoming booking (of any entry: a check, a doctor visit, a hospital visit) and each timed task: 24 hours
/// before (the evening before for one with only a day) and/or 2 hours before, as she chose in Settings, by the rules of
/// <see cref="ReminderTimes"/>: a booking made less than a day ahead still rings, 2 hours before.</item>
/// </list>
/// Times are wall-clock times in the plan's home time zone. Only reminders still ahead are set, the soonest
/// <see cref="MaxSet"/>; setting them again after every change and on every start keeps the next ones coming.
/// </summary>
public static class ReminderPlanner
{
    /// <summary>At most this many are set at once (the phone limits how many alarms an app may hold).</summary>
    public const int MaxSet = 64;

    public static readonly TimeSpan ShortNotice = ReminderTimes.ShortNoticeReminder;

    /// <summary>
    /// How late the phone may bring a reminder: Android's inexact alarms (no exact-alarm permission) can arrive up to an
    /// hour after their time. The words never say how long is left, only the day and time, so they stay true.
    /// </summary>
    public static readonly TimeSpan MayArriveLate = TimeSpan.FromHours(1);

    /// <param name="plan">Her plan.</param>
    /// <param name="settings">Her choices: when to remind her, and discreet titles.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="home">The plan's home time zone.</param>
    public static IReadOnlyList<PhoneReminder> For(PregnancyPlan plan, ReminderSettings settings, DateTimeOffset now, TimeZoneInfo home)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(home);

        var language = plan.Language;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, home).DateTime);
        var reminders = new List<PhoneReminder>();

        void Remind(string key, string title, string discreetTitle, DateOnly date, TimeOnly? time)
        {
            var toCome = ReminderTimes.ToCome(settings.When, date, time, home, now);
            if (toCome is ReminderPreference.DayBefore or ReminderPreference.Both)
            {
                var text = time is { } at ? Format(language, AppStrings.ReminderTomorrowAt, Clock(at)) : AppStrings.ReminderTomorrow.In(language);
                Add(key + ":daybefore", ReminderTimes.DayBefore(date, time, home), title, discreetTitle, text);
            }

            if (toCome is ReminderPreference.TwoHoursBefore or ReminderPreference.Both && time is { } start)
            {
                var soon = ReminderTimes.ToInstant(date, start, home) - ShortNotice;
                Add(key + ":soon", soon, title, discreetTitle, Format(language, ShortNoticeWords(soon, date, home), Clock(start)));
            }
        }

        void Add(string key, DateTimeOffset at, string title, string discreetTitle, string text, string? discreetText = null)
        {
            if (at > now)
            {
                reminders.Add(new PhoneReminder(
                    key,
                    at,
                    settings.DiscreetTitles ? discreetTitle : title,
                    settings.DiscreetTitles ? discreetText ?? text : text,
                    discreetTitle,
                    discreetText ?? text,
                    AppStrings.ReminderLockScreen.In(language)));
            }
        }

        // Bookings.
        var appointment = AppStrings.DiscreetAppointment.In(language);
        foreach (var check in plan.Checks)
        {
            if (check is { State: CheckState.Booked, Appointment: { } a } && a.Date >= today)
            {
                Remind(ReminderTimes.KeyFor(check), check.Title.In(language), appointment, a.Date, a.Time);
            }
        }

        // Timed tasks.
        foreach (var task in plan.Tasks)
        {
            // A scheduled task always has its day.
            if (task.IsScheduled && task.DueDate.GetValueOrDefault() >= today)
            {
                Remind(ReminderTimes.KeyFor(task), task.Title, AppStrings.DiscreetTask.In(language), task.DueDate.GetValueOrDefault(), task.DueTime);
            }
        }

        return [.. reminders.OrderBy(r => r.At).ThenBy(r => r.Key, StringComparer.Ordinal).Take(MaxSet)];
    }

    /// <summary>
    /// "Today at 08:30" when the reminder comes on the day itself however late the phone brings it, "Tomorrow at 00:30"
    /// when it comes the day before, and just "At 00:30" when it may come on either side of midnight.
    /// </summary>
    private static Domain.Common.LocalizedText ShortNoticeWords(DateTimeOffset at, DateOnly date, TimeZoneInfo home)
    {
        var earliest = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, home).DateTime);
        var latest = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at + MayArriveLate, home).DateTime);
        return earliest == latest
            ? earliest == date ? AppStrings.ReminderTodayAt : AppStrings.ReminderTomorrowAt
            : AppStrings.ReminderAt;
    }

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Format(string language, Domain.Common.LocalizedText text, object value) =>
        string.Format(CultureInfo.InvariantCulture, text.In(language), value);
}
