using Maia.Domain.Common;

namespace Maia.Application.Common;

/// <summary>
/// The few strings the core produces itself, in the plan's language: the words of the reminders Maia sets on the
/// phone, and the name Maia gives a doctor visit. UI text lives in the app's resource files; keep this list short.
/// </summary>
internal static class AppStrings
{
    /// <summary>All a reminder shows on the lock screen.</summary>
    public static readonly LocalizedText ReminderLockScreen = Text("Maia reminder", "Υπενθύμιση από τη Maia");

    public static readonly LocalizedText ReminderTomorrowAt = Text("Tomorrow at {0}", "Αύριο στις {0}");

    public static readonly LocalizedText ReminderTomorrow = Text("Tomorrow", "Αύριο");

    /// <summary>The short-notice reminder names the day, not "in 2 hours", so it stays true when the phone brings it late.</summary>
    public static readonly LocalizedText ReminderTodayAt = Text("Today at {0}", "Σήμερα στις {0}");

    /// <summary>For the rare short-notice reminder that may arrive on either side of midnight.</summary>
    public static readonly LocalizedText ReminderAt = Text("At {0}", "Στις {0}");

    public static readonly LocalizedText DiscreetAppointment = Text("Appointment", "Ραντεβού");

    public static readonly LocalizedText DiscreetTask = Text("Reminder", "Υπενθύμιση");

    /// <summary>
    /// The name a doctor visit gets when Maia names it (her next visit from an older Maia, the next appointment after a
    /// visit): "Visit with Dr. Eleni Papadaki". Greek names the doctor after a colon, so the name needs no case ending.
    /// </summary>
    public static readonly LocalizedText VisitWith = Text("Visit with {0}", "Επίσκεψη: {0}");

    private static LocalizedText Text(string english, string greek) =>
        new(new Dictionary<string, string> { [LocalizedText.English] = english, [LocalizedText.Greek] = greek });
}
