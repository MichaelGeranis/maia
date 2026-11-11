using Maia.Domain.Common;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;

namespace Maia.Domain.Plans;

/// <summary>Where an entry on her journey came from.</summary>
public enum CheckOrigin
{
    /// <summary>Copied from the country template at setup, because she kept it.</summary>
    Template = 0,

    /// <summary>Added by her, on Add.</summary>
    AddedByParent = 1,
}

/// <summary>
/// An entry on her journey: a check she kept at setup, or one she added. Once on her journey it is hers: she can book
/// it, rename it, move it or delete it, whatever its origin. Only <see cref="PregnancyPlan"/> changes it, so plan-wide
/// rules (events, revision) are always applied.
/// </summary>
/// <remarks>
/// Exception messages never name the check, its dates or its title (health data); the codes say what went wrong.
/// </remarks>
public sealed class PlannedCheck
{
    /// <summary>Longest name an entry may have, in any language (what she types is held shorter on screen).</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Longest note an entry may have, in any language.</summary>
    public const int MaxNoteLength = 500;

    internal PlannedCheck(
        CheckId id,
        CheckOrigin origin,
        LocalizedText title,
        LocalizedText? description,
        CheckCategory category,
        WeekWindow window,
        CheckState state,
        Appointment? appointment,
        Trimester? trimesterOnly = null,
        EntryKind? kind = null,
        bool isEdited = false)
    {
        Id = id;
        Origin = origin;
        Title = title;
        Description = description;
        Category = category;
        Window = window;
        State = state;
        Appointment = appointment;
        TrimesterOnly = trimesterOnly;
        Kind = kind;
        IsEdited = isEdited;
    }

    public CheckId Id { get; }

    public CheckOrigin Origin { get; }

    /// <summary>
    /// What an entry she added is (a doctor visit, an exam or test, a hospital visit, something else). Null for a check
    /// from her country's plan, which has its <see cref="Category"/> instead.
    /// </summary>
    public EntryKind? Kind { get; }

    public LocalizedText Title { get; private set; }

    /// <summary>
    /// Its note, shown when its card is opened: what a check from her country's plan is for, or the words she gave an
    /// entry; null when it has none.
    /// </summary>
    public LocalizedText? Description { get; private set; }

    public CheckCategory Category { get; private set; }

    /// <summary>
    /// The weeks it stands in: the week of its booking; with no booking, the week she gave, the trimester's weeks when she
    /// gave only that (<see cref="TrimesterOnly"/>), or for a check from setup the weeks it is usually done in.
    /// </summary>
    public WeekWindow Window { get; private set; }

    public CheckState State { get; private set; }

    /// <summary>The booking (a day and a time), while <see cref="State"/> is <see cref="CheckState.Booked"/>.</summary>
    public Appointment? Appointment { get; private set; }

    /// <summary>Set when she gave only the trimester (not booked, or done): it stands first in that trimester.</summary>
    public Trimester? TrimesterOnly { get; private set; }

    /// <summary>
    /// Whether she has booked, moved, marked or renamed it since it joined her journey: a newer template, or setup run
    /// again, then leaves it as she has it.
    /// </summary>
    public bool IsEdited { get; private set; }

    public bool IsMilestone => Category == CheckCategory.Milestone;

    /// <summary>The pregnancy week it stands in (the week of its booking, or the first of its weeks).</summary>
    public int Week => Window.FromWeek;

    /// <summary>The trimester it stands in.</summary>
    public Trimester Trimester => TrimesterOnly ?? GestationalAge.FromWeeks(Window.FromWeek).Trimester;

    /// <summary>
    /// What the entry shows at <paramref name="now"/> on <paramref name="today"/>, when the pregnancy is
    /// <paramref name="age"/> old. A booking is done once its time has passed (a day without a time, once the day has).
    /// </summary>
    public CheckStatus StatusAt(GestationalAge age, DateOnly today, TimeOnly now)
    {
        if (IsMilestone)
        {
            return CheckStatus.Milestone;
        }

        return State switch
        {
            CheckState.Booked when Appointment is { } booked && HasPassed(booked, today, now) => CheckStatus.Done,
            CheckState.Booked => CheckStatus.Booked,
            CheckState.Done => CheckStatus.Done,
            _ when Origin == CheckOrigin.Template && Window.HasPassed(age) => CheckStatus.ProbablyDone,
            _ => CheckStatus.NotBooked,
        };
    }

    /// <summary>Whether a booking's time has come by <paramref name="now"/> on <paramref name="today"/>.</summary>
    public static bool HasPassed(Appointment appointment, DateOnly today, TimeOnly now)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        return appointment.Date < today || (appointment.Date == today && appointment.Time is { } time && time <= now);
    }

    /// <summary>Takes its new timing: booked, or not booked or done in a trimester or a week.</summary>
    /// <returns>Whether anything changed.</returns>
    internal bool Take(EntryWhen when, WeekWindow window)
    {
        EnsureNotMilestone();
        var state = when.IsBooked ? CheckState.Booked : when.IsDone ? CheckState.Done : CheckState.NotBooked;
        if (state == State && when.Appointment == Appointment && when.Trimester == TrimesterOnly && window == Window)
        {
            return false;
        }

        State = state;
        Appointment = when.Appointment;
        TrimesterOnly = when.Trimester;
        Window = window;
        IsEdited = true;
        return true;
    }

    /// <summary>Gives it a new name. The same name again changes nothing.</summary>
    /// <returns>Whether the name changed.</returns>
    internal bool Rename(LocalizedText title)
    {
        EnsureNotMilestone();
        if (title.Equals(Title))
        {
            return false;
        }

        Title = title;
        IsEdited = true;
        return true;
    }

    /// <summary>Gives it a new note, or none. The same note again changes nothing.</summary>
    /// <returns>Whether the note changed.</returns>
    internal bool Describe(LocalizedText? note)
    {
        EnsureNotMilestone();
        if (Equals(note, Description))
        {
            return false;
        }

        Description = note;
        IsEdited = true;
        return true;
    }

    /// <summary>
    /// Takes what a newer version of its template says, while she has not acted on it (<see cref="IsEdited"/>):
    /// title, description, category and weeks. A check the
    /// newer version turns into a milestone becomes one; a milestone the newer version turns into a check stays as it is,
    /// since nothing new to book appears without her.
    /// </summary>
    /// <returns>Whether anything she sees changed.</returns>
    internal bool TakeTemplateContent(CheckTemplate newer)
    {
        var untouched = State == CheckState.NotBooked && !IsEdited;
        var becomesMilestone = newer.Category == CheckCategory.Milestone;
        if (!untouched || (IsMilestone && !becomesMilestone))
        {
            return false;
        }

        var changed = becomesMilestone != IsMilestone || !Title.Equals(newer.Title) || !newer.Description.Equals(Description) || Window != newer.Window;
        Title = newer.Title;
        Description = newer.Description;
        Category = newer.Category;
        Window = newer.Window;
        return changed;
    }

    /// <summary>Stands it in other weeks, when the due date moves (see <see cref="Window"/>).</summary>
    internal void MoveTo(WeekWindow window) => Window = window;

    internal void ForgetContact(CareContactId contactId)
    {
        if (Appointment is { } appointment && appointment.With == contactId)
        {
            Appointment = new Appointment(appointment.Date, appointment.Time, appointment.Location, with: null);
        }
    }

    /// <summary>
    /// Brings a stored check back within the rules its commands keep: a booked check has its appointment and nothing
    /// else, and a milestone (the due date) is never booked or done. Persistence only.
    /// </summary>
    /// <returns>A code for what was repaired, or null when the check keeps every rule.</returns>
    internal string? RepairStoredState()
    {
        if (IsMilestone && (State != CheckState.NotBooked || Appointment is not null || TrimesterOnly is not null))
        {
            State = CheckState.NotBooked;
            Appointment = null;
            TrimesterOnly = null;
            return "check.milestone_state";
        }

        if (State == CheckState.Booked && Appointment is null)
        {
            // Booked means "has a date": without one it goes back to not booked.
            State = CheckState.NotBooked;
            return "check.booked_without_appointment";
        }

        if (State != CheckState.Booked && Appointment is not null)
        {
            Appointment = null;
            return "check.appointment_without_booking";
        }

        if (State == CheckState.Booked && TrimesterOnly is not null)
        {
            TrimesterOnly = null;
            return "check.booked_with_trimester";
        }

        return null;
    }

    private void EnsureNotMilestone()
    {
        if (IsMilestone)
        {
            throw new DomainException("check.is_milestone", "A milestone is not an exam.");
        }
    }
}
