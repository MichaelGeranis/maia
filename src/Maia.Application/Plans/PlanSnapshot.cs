using Maia.Domain.Care;
using Maia.Domain.Plans;

namespace Maia.Application.Plans;

/// <summary>
/// One read of the plan, for a screen that needs several views of it at once (Add): the timeline and the care team,
/// all from the same load and for the same moment. Each view is built when first asked.
/// </summary>
public sealed class PlanSnapshot
{
    private readonly PregnancyPlan _plan;
    private readonly TimeOnly _now;
    private PlanTimeline? _timeline;

    internal PlanSnapshot(PregnancyPlan plan, DateOnly today, TimeOnly now)
    {
        _plan = plan;
        Today = today;
        _now = now;
    }

    /// <summary>The day every view is for.</summary>
    public DateOnly Today { get; }

    /// <summary>The plan's language: check titles read in it.</summary>
    public string Language => _plan.Language;

    /// <summary>As <see cref="PlanQueries.GetTimelineAsync"/>.</summary>
    public PlanTimeline Timeline => _timeline ??= new PlanTimeline(_plan.Dating, _plan.AgeOn(Today), PlanQueries.BuildTimeline(_plan, Today, _now));

    /// <summary>Every entry on her journey, in plan order.</summary>
    public IReadOnlyList<PlannedCheck> Checks => _plan.Checks;

    /// <summary>As <see cref="PlanQueries.GetCareTeamAsync"/>: the contacts linked to a role, in the order she linked them.</summary>
    public IReadOnlyList<CareContact> CareTeam(CareRole role) => _plan.CareTeam.For(role);
}
