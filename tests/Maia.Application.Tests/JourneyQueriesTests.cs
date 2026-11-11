using Maia.Application.Plans;
using Maia.Domain.Pregnancy;
using Maia.Testing;

namespace Maia.Application.Tests;

/// <summary>What Journey reads besides her checks: her tasks that have a day, a week or a trimester, and her blood pressure.</summary>
public sealed class JourneyQueriesTests
{
    [Fact]
    public async Task The_timeline_brings_her_tasks_by_day_and_time_then_those_for_a_week_or_trimester_and_leaves_ones_with_neither_out()
    {
        // One saved before tasks needed a week or a trimester has no place on a timeline.
        var plan = TestData.Plan().WithTasks([(null, null, false)]);
        plan.AddTask("Pack the hospital bag", trimester: Trimester.Third);
        plan.AddTask("Buy vitamins", new DateOnly(2026, 10, 5));
        plan.AddTask("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        plan.AddTask("Book the antenatal class", week: 28);
        plan.AddTask("Pick up the results", new DateOnly(2026, 10, 1));
        var ticked = plan.AddTask("Pay the lab", new DateOnly(2026, 9, 20));
        plan.SetTaskDone(ticked, true);
        var repository = new InMemoryPlanRepository();
        await repository.SaveAsync(plan, CancellationToken.None);

        var timeline = await new PlanQueries(repository, new FakeClock(TestData.Today)).GetTimelineAsync();

        Assert.Equal(
            new[] { "Pay the lab", "Pick up the results", "Call the clinic", "Buy vitamins", "Pack the hospital bag", "Book the antenatal class" },
            timeline!.TimelineTasks.Select(t => t.Title));
        Assert.True(timeline.TimelineTasks[0].IsDone);
    }

    [Fact]
    public async Task The_timeline_brings_her_blood_pressure_readings_earliest_first()
    {
        var plan = TestData.Plan();
        var later = plan.LogBloodPressure(124, 80, new DateOnly(2026, 10, 1), new TimeOnly(20, 0));
        var sooner = plan.LogBloodPressure(118, 76, new DateOnly(2026, 9, 30), new TimeOnly(8, 0));
        var repository = new InMemoryPlanRepository();
        await repository.SaveAsync(plan, CancellationToken.None);

        var timeline = await new PlanQueries(repository, new FakeClock(TestData.Today)).GetTimelineAsync();

        Assert.Equal([sooner, later], timeline!.Readings.Select(r => r.Id));
    }
}
