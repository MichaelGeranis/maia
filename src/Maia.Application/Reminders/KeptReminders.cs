using System.Globalization;
using Maia.Application.Abstractions;

namespace Maia.Application.Reminders;

/// <summary>A reminder with the plain number the phone knows it by (its alarm and its notification).</summary>
public sealed record NumberedReminder(int Number, PhoneReminder Reminder)
{
    /// <summary>What is kept of it for after a restart: its number, its time and its plain words. Never its key.</summary>
    public KeptReminder Kept => new(Number, Reminder.At, Reminder.DiscreetTitle, Reminder.DiscreetText, Reminder.LockScreenTitle);
}

/// <summary>A reminder as kept for after a restart: a plain number, a time and words that name no check or task.</summary>
public sealed record KeptReminder(int Number, DateTimeOffset At, string Title, string Text, string LockScreenTitle);

/// <summary>What <see cref="KeptReminders.Change"/> found to do on the phone.</summary>
/// <param name="Keep">Set already, exactly as wanted: left as they are.</param>
/// <param name="Cancel">The numbers of those no longer wanted, or no longer as wanted.</param>
/// <param name="Add">Those to set, under new numbers.</param>
/// <param name="Next">The number to start from next time.</param>
public sealed record ReminderChange(IReadOnlyList<NumberedReminder> Keep, IReadOnlyList<int> Cancel, IReadOnlyList<NumberedReminder> Add, int Next)
{
    /// <summary>Every reminder set once the change is made.</summary>
    public IReadOnlyList<NumberedReminder> All => [.. Keep, .. Add];
}

/// <summary>
/// What a phone keeps to set reminders again after it restarts, outside Maia's encrypted plan. Each reminder gets a
/// plain number handed out in turn, never one worked out from what the reminder is for: a key such as
/// <c>check:nipt:daybefore</c> hashed into a number could be matched back to the check by anyone holding a copy of the
/// phone's storage. Numbers keep rising between calls, so a new reminder never takes the place of an older
/// notification she has not read yet.
/// </summary>
public static class KeptReminders
{
    /// <summary>The highest number handed out before counting starts again from 1.</summary>
    public const int LastNumber = 1_000_000_000;

    /// <summary>Numbers <paramref name="reminders"/> in their order, starting from <paramref name="next"/>.</summary>
    /// <returns>Each reminder with its number, and the number to start from next time.</returns>
    public static (IReadOnlyList<NumberedReminder> Numbered, int Next) Number(IReadOnlyList<PhoneReminder> reminders, int next)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        var first = next < 1 || next > LastNumber - reminders.Count ? 1 : next;
        return ([.. reminders.Select((reminder, i) => new NumberedReminder(first + i, reminder))], first + reminders.Count);
    }

    /// <summary>
    /// What to change on the phone to go from the reminders <paramref name="set"/> to <paramref name="wanted"/>: one
    /// that is exactly as it was keeps its number and its alarm; the others are cancelled, or set under new numbers from
    /// <paramref name="next"/>, so a new one still never takes the place of a notification she has not read.
    /// </summary>
    public static ReminderChange Change(IReadOnlyList<NumberedReminder> set, IReadOnlyList<PhoneReminder> wanted, int next)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(wanted);
        var unmatched = set.ToList();
        var keep = new List<NumberedReminder>();
        var add = new List<PhoneReminder>();
        foreach (var reminder in wanted)
        {
            var index = unmatched.FindIndex(s => s.Reminder == reminder);
            if (index >= 0)
            {
                keep.Add(unmatched[index]);
                unmatched.RemoveAt(index);
            }
            else
            {
                add.Add(reminder);
            }
        }

        var (added, after) = Number(add, next);
        return new ReminderChange(keep, [.. unmatched.Select(s => s.Number)], added, after);
    }

    /// <summary>One line per reminder: number, time (Unix milliseconds) and the escaped plain words, tab-separated.</summary>
    public static string Write(IEnumerable<KeptReminder> kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        return string.Join('\n', kept.Select(k => string.Join(
            '\t',
            k.Number.ToString(CultureInfo.InvariantCulture),
            k.At.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            Uri.EscapeDataString(k.Title),
            Uri.EscapeDataString(k.Text),
            Uri.EscapeDataString(k.LockScreenTitle))));
    }

    /// <summary>Reads what <see cref="Write"/> kept; lines it cannot read are left out.</summary>
    public static IReadOnlyList<KeptReminder> Read(string? kept)
    {
        var reminders = new List<KeptReminder>();
        foreach (var line in (kept ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length == 5
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var at)
                && at is >= -62_135_596_800_000 and <= 253_402_300_799_999)
            {
                reminders.Add(new KeptReminder(
                    number,
                    DateTimeOffset.FromUnixTimeMilliseconds(at),
                    Uri.UnescapeDataString(parts[2]),
                    Uri.UnescapeDataString(parts[3]),
                    Uri.UnescapeDataString(parts[4])));
            }
        }

        return reminders;
    }
}
