using System.Globalization;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Formatting;

/// <summary>How dates, weeks and statuses read in the UI. One place, so every screen says it the same way.</summary>
public static class DisplayFormat
{
    /// <summary>The endings of Greek ordinals written with a number: "1ο", "2η", "3ης", "4ου"…</summary>
    private static readonly string[] OrdinalEndings = ["ο", "η", "α", "ος", "ης", "ας", "ου", "ων", "οι", "ες", "ους"];

    /// <summary>
    /// Text in capitals, as eyebrows show it. Greek capitals drop their accents ("Επόμενο" → "ΕΠΟΜΕΝΟ"), as Greek
    /// typography does; the dialytika stays ("ϊ" → "Ϊ"), and an ordinal's ending after its number stays small ("1ο
    /// τρίμηνο" → "1ο ΤΡΙΜΗΝΟ", "3ης" → "3ης"). Other languages are simply upper-cased.
    /// </summary>
    public static string Caps(string text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        if (culture.TwoLetterISOLanguageName != Text.Greek)
        {
            return text.ToUpper(culture);
        }

        // Accents off first: "ΐ" has no capital form of its own, "ι" with a dialytika does.
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var kept = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            // The acute (tonos). The combined dialytika and tonos (U+0344) is never here: FormD splits it into
            // U+0308 U+0301, so its tonos goes and its dialytika stays.
            if (c != (char)0x0301)
            {
                kept.Append(c);
            }
        }

        var plain = kept.ToString().Normalize(System.Text.NormalizationForm.FormC);
        var caps = plain.ToUpper(culture).ToCharArray();

        // Upper-casing maps letter for letter, so each ordinal ending is put back where it was.
        for (var i = 0; i < plain.Length; i++)
        {
            if (OrdinalEndingAfter(plain, i) is { } length)
            {
                plain.CopyTo(i + 1, caps, i + 1, length);
                i += length;
            }
        }

        return new string(caps);
    }

    /// <summary>
    /// The length of a Greek ordinal's ending right after the digit at <paramref name="index"/>: one of
    /// <see cref="OrdinalEndings"/>, ending the word, as in "1ο" or "2ης" (not "24ωρη"). Null when there is none.
    /// </summary>
    private static int? OrdinalEndingAfter(string text, int index)
    {
        if (!char.IsAsciiDigit(text[index]))
        {
            return null;
        }

        var end = index + 1;
        while (end < text.Length && char.IsLetter(text[end]))
        {
            end++;
        }

        var ending = text.AsSpan(index + 1, end - index - 1);
        foreach (var known in OrdinalEndings)
        {
            if (ending.SequenceEqual(known))
            {
                return ending.Length;
            }
        }

        return null;
    }

    public static string WeekLabel(GestationalAge age) => Text.Format("Week_Label", age.Weeks, age.Days);

    /// <summary>"24 weeks and 3 days", "24 weeks and 1 day".</summary>
    public static string WeeksAndDays(GestationalAge age) =>
        Text.Format(age.Days == 1 ? "Week_WeeksAndOneDay" : "Week_WeeksAndDays", age.Weeks, age.Days);

    /// <summary>Short date with weekday, for example "Tue 6 Oct" or "Τρί 6 Οκτ", in the language Maia shows.</summary>
    public static string Date(DateOnly date) => date.ToString("ddd d MMM", Text.Culture);

    /// <summary>
    /// As <see cref="Date(DateOnly)"/>, with the year when the day is more than <see cref="YearlessDays"/> from
    /// <paramref name="today"/>: "Fri 3 Oct 2031" would otherwise read as this year's.
    /// </summary>
    public static string Date(DateOnly date, DateOnly today) => date.ToString(DateFieldFormat(date, today), Text.Culture);

    /// <summary>
    /// How far a day may be from today and still read without its year: within about half a year either way a day and
    /// month can only mean one day ("Tue 5 Jan" in October is the coming January), so a pregnancy's dates rarely need one.
    /// </summary>
    public const int YearlessDays = 182;

    /// <summary>The format a date reads in: "ddd d MMM", with the year too when it is far from <paramref name="today"/>.</summary>
    public static string DateFieldFormat(DateOnly date, DateOnly today) =>
        Math.Abs(date.DayNumber - today.DayNumber) <= YearlessDays ? "ddd d MMM" : "ddd d MMM yyyy";

    /// <summary>Day and month only, for ranges: "6 Oct".</summary>
    public static string DayMonth(DateOnly date) => date.ToString("d MMM", Text.Culture);

    /// <summary>Days from one to another: "11–17 Oct", or "27 Sep – 3 Oct" when they are in different months.</summary>
    public static string DayRange(DateOnly from, DateOnly to) =>
        from.Month == to.Month
            ? $"{from.Day.ToString(Text.Culture)}–{DayMonth(to)}"
            : $"{DayMonth(from)} – {DayMonth(to)}";

    /// <summary>24-hour time, as in the design: "09:00".</summary>
    public static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"Weeks 24–28 · 27 Sep – 31 Oct", or for one week "Week 16 · around 2 Aug".</summary>
    public static string Window(WeekWindow window, PregnancyDating dating)
    {
        ArgumentNullException.ThrowIfNull(dating);
        var start = dating.StartOfWeek(window.FromWeek);
        var end = dating.StartOfWeek(window.ToWeek).AddDays(GestationalAge.DaysPerWeek - 1);
        return window.FromWeek == window.ToWeek
            ? $"{Text.Format("Plan_Week", window.FromWeek)} · {Text.Format("Plan_Around", DayMonth(start))}"
            : $"{Text.Format("Plan_Weeks", window.FromWeek, window.ToWeek)} · {DayMonth(start)} – {DayMonth(end)}";
    }

    /// <summary>"Tue 6 Oct at 10:30", or just the day when no time is set.</summary>
    public static string Appointment(Appointment appointment) => Appointment(appointment, appointment?.Date ?? default);

    /// <summary>As <see cref="Appointment(Domain.Plans.Appointment)"/>, with the year when it is not in <paramref name="today"/>'s year.</summary>
    public static string Appointment(Appointment appointment, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        return appointment.Time is { } time
            ? Text.Format("Plan_Booked", Date(appointment.Date, today), Time(time))
            : Date(appointment.Date, today);
    }

    /// <summary>
    /// The line under an entry on her journey: its trimester, its week (or a check's usual weeks, "Weeks 24–28"; none
    /// when she gave only the trimester) and, when booked, the day and time: "2nd trimester · Week 24 · Tue 6 Oct, 10:30".
    /// The due date reads its day. Reminders are not named here: they ring as Settings says.
    /// </summary>
    public static string PlanCaption(CheckView view, PregnancyDating dating, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(dating);
        var check = view.Check;
        if (view.Status == CheckStatus.Milestone)
        {
            return Date(dating.DueDate);
        }

        var parts = new List<string>(4) { Section(PlanQueries.SectionKeyFor(check.Window.FromWeek)) };
        if (check.TrimesterOnly is null)
        {
            parts.Add(check.Window.FromWeek == check.Window.ToWeek
                ? Text.Format("Plan_Week", check.Window.FromWeek)
                : Text.Format("Plan_Weeks", check.Window.FromWeek, check.Window.ToWeek));
        }

        if (check.Appointment is { } booked)
        {
            parts.Add(BookedShort(booked, today));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>"Mon 12 Oct, 08:30", or just the day when no time is set.</summary>
    private static string BookedShort(Appointment appointment, DateOnly today) => appointment.Time is { } time
        ? Text.Format("Plan_BookedShort", Date(appointment.Date, today), Time(time))
        : Date(appointment.Date, today);

    /// <summary>"2nd trimester", or "After birth": a trimester's name, as Journey's headings say it.</summary>
    public static string Trimester(Trimester trimester) =>
        Section(trimester == Domain.Pregnancy.Trimester.AfterBirth ? "after_birth" : $"trimester{(int)trimester}");

    /// <summary>
    /// When a task without a date is for: "Week 26", "3rd trimester" or "After birth"; null when it is for neither (a
    /// task saved before tasks needed one).
    /// </summary>
    public static string? TaskPeriod(Trimester? trimester, int? week) =>
        week is { } w ? Text.Format("Plan_Week", w)
        : trimester is { } t ? Trimester(t)
        : null;

    /// <summary>
    /// A check's name where it stands alone (toasts, pickers, reminders): repeated names such as "Check-up" get their
    /// week, "Check-up · week 25", so she can tell them apart. Cards on the timeline use the plain title.
    /// </summary>
    public static string CheckName(PlannedCheck check, IEnumerable<PlannedCheck> plan, string language)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(plan);
        var title = check.Title.In(language);
        var repeated = plan.Count(c => c.Title.In(language) == title) > 1;
        return repeated ? Text.Format("Plan_NameWithWeek", title, check.Window.FromWeek) : title;
    }

    /// <summary>Picks the singular or plural text: <c>Count("Plan_OneResult", "Plan_Results", n)</c>.</summary>
    public static string Count(string oneKey, string manyKey, int count) =>
        count == 1 ? Text.Get(oneKey) : Text.Format(manyKey, count);

    /// <summary>
    /// The status chip's words: "Not booked", "Booked", "Done", "Probably done" or "Due date".
    /// </summary>
    public static string Status(CheckStatus status) => Text.Get($"Status_{status}");

    public static string Category(CheckCategory category) => Text.Get($"Tag_{category}");

    /// <summary>
    /// What an entry she added is: "Doctor visit", "Exam or test", "Hospital visit" or "Something else". The same words name
    /// the four choices on Add and label each entry on Journey.
    /// </summary>
    public static string Kind(EntryKind kind) => Text.Get($"Kind_{kind}");

    public static string Section(string key) => Text.Get($"Section_{key}");

    /// <summary>Stable, culture-free name the views use to pick status colours.</summary>
    public static string StatusKind(CheckStatus status) => status.ToString();
}
