using Maia.Domain.Common;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;

namespace Maia.Domain.Plans;

/// <summary>
/// A to-do the parent adds, such as "call the clinic about results". It is for a date (and maybe a time on that date),
/// or, with no date, for a trimester or a pregnancy week, as an entry on her journey can be. Only one with a date and a
/// time is scheduled (and so worth a reminder). A task saved before tasks needed one may have none of them; it keeps
/// that until she changes it.
/// </summary>
public sealed class PlanTask
{
    public const int MaxTitleLength = 200;

    internal PlanTask(TaskId id, string title, DateOnly? dueDate, TimeOnly? dueTime, bool isDone, Trimester? trimester = null, int? week = null)
        : this(id, title, dueDate, dueTime, isDone, trimester, week, isNew: true)
    {
    }

    private PlanTask(TaskId id, string title, DateOnly? dueDate, TimeOnly? dueTime, bool isDone, Trimester? trimester, int? week, bool isNew)
    {
        Validate(title, dueDate, dueTime, trimester, week, isNew);
        Id = id;
        Title = title.Trim();
        DueDate = dueDate;
        DueTime = dueTime;
        Trimester = trimester;
        Week = week;
        IsDone = isDone;
    }

    public TaskId Id { get; }

    public string Title { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public TimeOnly? DueTime { get; private set; }

    /// <summary>The trimester it is for, when she gave only that (no date).</summary>
    public Trimester? Trimester { get; private set; }

    /// <summary>The pregnancy week it is for, when she gave that (no date).</summary>
    public int? Week { get; private set; }

    public bool IsDone { get; private set; }

    /// <summary>Only tasks with a date and time are worth a reminder.</summary>
    public bool IsScheduled => !IsDone && DueDate is not null && DueTime is not null;

    internal void SetDone(bool done) => IsDone = done;

    /// <summary>Changes what the task says and when it is due, under the same rules as a new task.</summary>
    /// <returns>Whether anything changed.</returns>
    internal bool Change(string title, DateOnly? dueDate, TimeOnly? dueTime, Trimester? trimester = null, int? week = null)
    {
        Validate(title, dueDate, dueTime, trimester, week, isNew: true);
        var trimmed = title.Trim();
        if (trimmed == Title && dueDate == DueDate && dueTime == DueTime && trimester == Trimester && week == Week)
        {
            return false;
        }

        Title = trimmed;
        DueDate = dueDate;
        DueTime = dueTime;
        Trimester = trimester;
        Week = week;
        return true;
    }

    /// <summary>
    /// Rebuilds a stored task. The title limit, and the need for a date, a trimester or a week, are for what she types
    /// now: a task saved under older rules still loads.
    /// </summary>
    internal static PlanTask Restore(TaskId id, string title, DateOnly? dueDate, TimeOnly? dueTime, bool isDone, Trimester? trimester = null, int? week = null) =>
        new(id, title, dueDate, dueTime, isDone, trimester, week, isNew: false);

    /// <summary>
    /// Whether <paramref name="trimester"/> and <paramref name="week"/> can stand together with
    /// <paramref name="dueDate"/>: at most one of the three, a trimester Maia knows and a week it shows.
    /// </summary>
    internal static bool IsValidPeriod(DateOnly? dueDate, Trimester? trimester, int? week) =>
        (dueDate is null ? 0 : 1) + (trimester is null ? 0 : 1) + (week is null ? 0 : 1) <= 1
        && (trimester is not { } t || Enum.IsDefined(t))
        && week is null or (>= 0 and <= WeekWindow.MaxWeek);

    private static void Validate(string title, DateOnly? dueDate, TimeOnly? dueTime, Trimester? trimester, int? week, bool isNew)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (isNew && title.Length > MaxTitleLength)
        {
            throw new DomainException("task.title.too_long", "Task title is too long.");
        }

        if (dueTime is not null && dueDate is null)
        {
            throw new DomainException("task.time_without_date", "A task with a time must also have a date.");
        }

        if (!IsValidPeriod(dueDate, trimester, week))
        {
            throw new DomainException("task.when.invalid", "A task is for a date, a trimester or a week.");
        }

        if (isNew && dueDate is null && trimester is null && week is null)
        {
            throw new DomainException("task.when.missing", "A task needs a date, a trimester or a week.");
        }
    }
}
