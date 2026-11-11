using Maia.Domain.Plans;
using Maia.Domain.Templates;

namespace Maia.Application.Plans;

/// <summary>Where one of her tasks stands on a given day: the groups Your week counts and Your tasks lists.</summary>
public enum TaskGroup
{
    /// <summary>Still to do, with a day before today.</summary>
    Earlier,

    /// <summary>Still to do, due today or in the six days after it, with or without a time.</summary>
    ThisWeek,

    /// <summary>Still to do, with a day after this week.</summary>
    Later,

    /// <summary>Still to do, with no day.</summary>
    NoDate,

    /// <summary>Ticked off.</summary>
    Done,
}

/// <summary>Sorts her tasks into <see cref="TaskGroup"/>s, in one way for every screen that shows them.</summary>
public static class TaskGroups
{
    /// <summary>A task due within this many days from today (today included) is this week's.</summary>
    public const int ThisWeekDays = 7;

    /// <summary>The group <paramref name="task"/> belongs to on <paramref name="today"/>.</summary>
    public static TaskGroup Of(PlanTask task, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task switch
        {
            { IsDone: true } => TaskGroup.Done,
            { DueDate: null } => TaskGroup.NoDate,
            { DueDate: { } day } when day < today => TaskGroup.Earlier,
            { DueDate: { } day } when day.DayNumber - today.DayNumber < ThisWeekDays => TaskGroup.ThisWeek,
            _ => TaskGroup.Later,
        };
    }

    /// <summary>
    /// Tasks in the order a list shows them: by day, and within a day by time (one without a time last that day);
    /// those with no day after the rest, by the week they are for (one for a trimester first in its first week), and
    /// one with neither, saved before tasks needed one, last. Otherwise they keep the order she added them in.
    /// </summary>
    public static IEnumerable<PlanTask> InOrder(IEnumerable<PlanTask> tasks) =>
        tasks
            .OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.DueTime ?? TimeOnly.MaxValue)
            .ThenBy(t => t.Week ?? (t.Trimester is { } trimester ? WeekWindow.OfTrimester(trimester).FromWeek : int.MaxValue))
            .ThenBy(t => t.Week is not null);
}
