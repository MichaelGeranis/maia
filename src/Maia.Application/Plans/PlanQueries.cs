using Maia.Application.Abstractions;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;

namespace Maia.Application.Plans;

/// <summary>A check with its status for today, ready for display.</summary>
public sealed record CheckView(PlannedCheck Check, CheckStatus Status, string Title);

/// <summary>
/// Everything the "Your week" screen needs, read from one load of the plan. <see cref="NextAppointment"/> is her next
/// booked entry of any kind (a visit, an exam, a hospital visit);
/// <see cref="Questions"/> are the questions she noted for the doctor, in the order she keeps them (the newest first).
/// </summary>
public sealed record WeekOverview(
    GestationalAge Age,
    Trimester Trimester,
    DateOnly DueDate,
    CheckView? NextAppointment,
    IReadOnlyList<PlanTask> OpenTasks,
    IReadOnlyList<DoctorQuestion> Questions)
{
    /// <summary>
    /// "Next up": her next booked entry from today on (a check, a visit, an exam…). Her tasks live in the task card
    /// only, so a timed task is never next up too. Null when nothing is booked.
    /// </summary>
    public NextUp? NextUp =>
        NextAppointment?.Check.Appointment is { } booked ? new NextUp(booked.Date, booked.Time, NextAppointment) : null;

    /// <summary>The day this overview is for.</summary>
    public DateOnly Today { get; init; }

    /// <summary>How many tasks she has ticked off and not cleared yet (they count towards her list's limit).</summary>
    public int DoneTasks { get; init; }

    /// <summary>Her OB-GYN contacts, in the order she linked them (the first is her doctor).</summary>
    public IReadOnlyList<CareContact> ObGyns { get; init; } = [];

    /// <summary>
    /// Whether the plan holds something of her own that would be hard to lose (a result, a booking or a check she
    /// marked done, an entry she added, a task or a question) and has been in use for a week: then a backup is worth
    /// suggesting.
    /// </summary>
    public bool WorthKeeping { get; init; }
}

/// <summary>
/// Everything Your tasks needs, from one read of her plan: every task, ticked or not, in list order
/// (<see cref="TaskGroups.InOrder"/>), the day they are grouped for (<see cref="TaskGroups.Of"/>), and how far the
/// pregnancy is that day (where a trimester or a week chosen for a task starts).
/// </summary>
public sealed record TasksOverview(IReadOnlyList<PlanTask> Tasks, DateOnly Today, GestationalAge Age);

/// <summary>
/// What comes next on Your week: her next booked entry (<see cref="Entry"/>), on <see cref="Date"/> at <see cref="Time"/> (no time: some time that day).
/// </summary>
public sealed record NextUp(DateOnly Date, TimeOnly? Time, CheckView Entry);

/// <summary>
/// One result for the Results screen: a PDF report or a photo. <see cref="Kind"/> says which.
/// </summary>
public sealed record ResultView(ResultAttachment Result)
{
    public AttachmentKind Kind => Result.Kind;
}

/// <summary>Everything the Results screen needs, from one read of her plan: every result, newest first.</summary>
public sealed record ResultsOverview(IReadOnlyList<ResultView> Results);

/// <summary>A trimester's worth of checks on the plan timeline.</summary>
public sealed record TimelineSection(string Key, IReadOnlyList<CheckView> Checks);

/// <summary>
/// The whole plan timeline, with the dating needed to turn weeks into dates and her OB-GYN contacts (for "call to
/// book"), all from one load of the plan.
/// </summary>
public sealed record PlanTimeline(PregnancyDating Dating, GestationalAge Age, IReadOnlyList<TimelineSection> Sections)
{
    /// <summary>The contacts linked as her OB-GYN, in the order she linked them.</summary>
    public IReadOnlyList<CareContact> ObGyns { get; init; } = [];

    /// <summary>
    /// Her tasks that have a place on the timeline, ticked or not: a date (by date and then time, one with no time first
    /// that day), a week or a trimester, so Journey can show them there. A task saved with none of them, before tasks
    /// needed one, has no place on a timeline and is left out.
    /// </summary>
    public IReadOnlyList<PlanTask> TimelineTasks { get; init; } = [];

    /// <summary>Her blood pressure readings, the earliest first, so Journey can show each at the day and time it was taken.</summary>
    public IReadOnlyList<BloodPressureReading> Readings { get; init; } = [];
}

/// <summary>Read-only views over the plan. No side effects, safe to call on every screen refresh.</summary>
public sealed class PlanQueries
{
    /// <summary>How long a plan is in use before a backup is suggested, so setting up is never met with a reminder.</summary>
    public const int WorthKeepingAfterDays = 7;

    private readonly IPlanRepository _plans;
    private readonly IClock _clock;

    public PlanQueries(IPlanRepository plans, IClock clock)
    {
        _plans = plans;
        _clock = clock;
    }

    public async Task<WeekOverview?> GetWeekAsync(CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan is null
            ? null
            : BuildWeek(plan, TodayAtHome(plan, _clock), NowAtHome(plan, _clock));
    }

    /// <summary>
    /// Your tasks: every task she has, ticked or not, in list order (<see cref="TaskGroups.InOrder"/>), with the day they
    /// are grouped for. Null without a plan.
    /// </summary>
    public async Task<TasksOverview?> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan is null ? null : new TasksOverview(TaskGroups.InOrder(plan.Tasks).ToList(), _clock.Today, plan.AgeOn(_clock.Today));
    }

    public async Task<PlanTimeline?> GetTimelineAsync(CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan is null
            ? null
            : new PlanTimeline(plan.Dating, plan.AgeOn(_clock.Today), BuildTimeline(plan, TodayAtHome(plan, _clock), NowAtHome(plan, _clock)))
            {
                ObGyns = plan.CareTeam.For(CareRole.ObGyn),
                TimelineTasks = TimelineTasks(plan),
                Readings = plan.BloodPressure,
            };
    }

    /// <summary>The language her checks are named in, or null without a plan: all start-up needs from it.</summary>
    public async Task<string?> GetLanguageAsync(CancellationToken cancellationToken = default) =>
        (await _plans.LoadAsync(cancellationToken).ConfigureAwait(false))?.Language;

    /// <summary>
    /// The Results screen, from one read of her plan: every result in one list, newest first. Null without a plan.
    /// </summary>
    public async Task<ResultsOverview?> GetResultsOverviewAsync(CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan is null ? null : new ResultsOverview(Results(plan));
    }

    /// <summary>
    /// Every result in her plan, in one list, newest first. Null without a plan.
    /// </summary>
    public async Task<IReadOnlyList<ResultView>?> GetResultsAsync(CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan is null ? null : Results(plan);
    }

    /// <summary>See <see cref="GetResultsAsync"/>.</summary>
    internal static IReadOnlyList<ResultView> Results(PregnancyPlan plan) =>
        plan.Results
            .Select(r => new ResultView(r))
            .OrderByDescending(v => v.Result.AddedAt)
            .ToList();

    /// <summary>The contacts linked to a role, in the order she linked them. Empty without a plan.</summary>
    public async Task<IReadOnlyList<CareContact>> GetCareTeamAsync(CareRole role, CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan?.CareTeam.For(role) ?? [];
    }

    /// <summary>Every contact she linked, whatever its role, in the order she linked them, from one load. Empty without a plan.</summary>
    public async Task<IReadOnlyList<CareContact>> GetCareContactsAsync(CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan?.CareTeam.Contacts.ToList() ?? [];
    }

    /// <summary>
    /// One read of the plan for a screen that needs several views of it at once (Add), so it loads the plan
    /// once per refresh. Null without a plan.
    /// </summary>
    public async Task<PlanSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var plan = await _plans.LoadAsync(cancellationToken).ConfigureAwait(false);
        return plan is null ? null : new PlanSnapshot(plan, TodayAtHome(plan, _clock), NowAtHome(plan, _clock));
    }

    /// <summary>
    /// Today in her home zone (<see cref="HomeTimeZone.Of"/>), where her bookings' days and times are: a booking turns
    /// done at its time there, wherever the phone is.
    /// </summary>
    internal static DateOnly TodayAtHome(PregnancyPlan plan, IClock clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, HomeTimeZone.Of(plan, clock)).DateTime);

    /// <summary>The time it is now in her home zone, to the minute (see <see cref="TodayAtHome"/>).</summary>
    internal static TimeOnly NowAtHome(PregnancyPlan plan, IClock clock)
    {
        var now = TimeZoneInfo.ConvertTime(clock.UtcNow, HomeTimeZone.Of(plan, clock)).DateTime;
        return new TimeOnly(now.Hour, now.Minute);
    }

    /// <param name="plan">Her plan.</param>
    /// <param name="today">The day the overview is for.</param>
    /// <param name="now">The time of day it is: a booking earlier today has passed and is no longer next up.</param>
    internal static WeekOverview BuildWeek(PregnancyPlan plan, DateOnly today, TimeOnly now = default)
    {
        var next = plan.NextAppointment(today, now);
        var age = plan.AgeOn(today);

        var openTasks = TaskGroups.InOrder(plan.Tasks.Where(t => !t.IsDone)).ToList();

        return new WeekOverview(
            age,
            age.Trimester,
            plan.Dating.DueDate,
            next is null ? null : View(plan, next, today, now),
            openTasks,
            plan.Questions)
        {
            Today = today,
            DoneTasks = plan.Tasks.Count(t => t.IsDone),
            ObGyns = plan.CareTeam.For(CareRole.ObGyn),
            WorthKeeping = IsWorthKeeping(plan, today),
        };
    }

    /// <summary>See <see cref="WeekOverview.WorthKeeping"/>.</summary>
    internal static bool IsWorthKeeping(PregnancyPlan plan, DateOnly today) =>
        DateOnly.FromDateTime(plan.CreatedAt.DateTime).AddDays(WorthKeepingAfterDays) <= today
        && (plan.Checks.Any(c => c.State is CheckState.Booked or CheckState.Done || c.Origin == CheckOrigin.AddedByParent)
            || plan.Results.Count > 0
            || plan.Tasks.Count > 0
            || plan.Questions.Count > 0
            || plan.BloodPressure.Count > 0);

    /// <summary>Stable key for the timeline section a week belongs to; the UI maps it to a localized heading.</summary>
    public static string SectionKeyFor(int fromWeek) => fromWeek switch
    {
        < 14 => "trimester1",
        < 28 => "trimester2",
        <= 40 => "trimester3",
        _ => "after_birth",
    };

    internal static IReadOnlyList<TimelineSection> BuildTimeline(PregnancyPlan plan, DateOnly today, TimeOnly now) =>
        plan.Checks
            .Select(c => View(plan, c, today, now))
            .GroupBy(v => SectionKeyFor(v.Check.Window.FromWeek))
            .Select(g => new TimelineSection(g.Key, g.ToList()))
            .ToList();

    /// <summary>See <see cref="PlanTimeline.TimelineTasks"/>.</summary>
    internal static IReadOnlyList<PlanTask> TimelineTasks(PregnancyPlan plan) =>
        plan.Tasks
            .Where(t => t.DueDate is not null || t.Trimester is not null || t.Week is not null)
            .OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.DueTime ?? TimeOnly.MinValue)
            .ToList();

    internal static CheckView View(PregnancyPlan plan, PlannedCheck check, DateOnly today, TimeOnly now) =>
        new(check, plan.StatusOf(check, today, now), check.Title.In(plan.Language));
}
