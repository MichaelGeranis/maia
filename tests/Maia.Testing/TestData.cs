using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;

namespace Maia.Testing;

/// <summary>A small, stable template for tests, independent of the shipped content.</summary>
public static class TestData
{
    /// <summary>Today in most tests: 30 Sep 2026, with the due date 17 Jan 2027 that is 24+3.</summary>
    public static readonly DateOnly Today = new(2026, 9, 30);

    public static readonly DateOnly DueDate = new(2027, 1, 17);

    public static readonly CheckId Booking = new("booking");
    public static readonly CheckId Anomaly = new("anomaly");
    public static readonly CheckId Glucose = new("ogtt");
    public static readonly CheckId Checkup25 = new("checkup-25");
    public static readonly CheckId Growth = new("growth");
    public static readonly CheckId Nipt = new("nipt");
    public static readonly CheckId DueDateMilestone = new("due-date");

    public static PlanTemplate Template() => new(
        "test-gr",
        "GR",
        version: 1,
        ReviewStatus.ClinicallyReviewed,
        [
            Check(Booking, "Booking visit", "Επίσκεψη πρώτης καταγραφής", CheckCategory.Routine, 8, 10),
            Check(Nipt, "NIPT", "NIPT", CheckCategory.Offered, 10, 14, includedByDefault: false),
            Check(Anomaly, "Anomaly scan", "Υπερηχογράφημα β' επιπέδου", CheckCategory.Routine, 20, 24),
            Check(Glucose, "Glucose test", "Καμπύλη σακχάρου", CheckCategory.Routine, 24, 28),
            Check(Checkup25, "Check-up", "Επίσκεψη", CheckCategory.Routine, 25, 25),
            Check(Growth, "Growth scan", "Υπερηχογράφημα ανάπτυξης", CheckCategory.IfAdvised, 32, 34, includedByDefault: false),
            Check(DueDateMilestone, "Due date", "Πιθανή ημερομηνία τοκετού", CheckCategory.Milestone, 40, 40),
        ]);

    public static PregnancyPlan Plan(IReadOnlyDictionary<CheckId, bool>? overrides = null) =>
        PregnancyPlan.Create(
            PlanId.New(),
            PregnancyDating.FromDueDate(DueDate),
            Template(),
            LocalizedText.English,
            new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.FromHours(3)),
            overrides);

    public static LocalizedText Text(string english, string greek) =>
        new(new Dictionary<string, string> { [LocalizedText.English] = english, [LocalizedText.Greek] = greek });

    private static CheckTemplate Check(
        CheckId id,
        string english,
        string greek,
        CheckCategory category,
        int from,
        int to,
        bool includedByDefault = true) =>
        new(id, Text(english, greek), Text(english + " details", greek), category, new WeekWindow(from, to), includedByDefault);
}

/// <summary>Ways into a plan that tests use often.</summary>
public static class TestPlans
{
    /// <summary>
    /// The plan with <paramref name="tasks"/> besides those it has, rebuilt as a stored plan is read, so it can go past
    /// the number she can add herself (<see cref="PregnancyPlan.MaxTasks"/>): a restored backup can bring that many.
    /// Each is "Task 1", "Task 2"…, due when its tuple says, ticked or not.
    /// </summary>
    public static PregnancyPlan WithTasks(this PregnancyPlan plan, IEnumerable<(DateOnly? Day, TimeOnly? Time, bool Done)> tasks)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(tasks);
        var added = tasks.Select((t, i) => PregnancyPlan.RehydrateTask(TaskId.New(), $"Task {i + 1}", t.Day, t.Time, t.Done));
        return PregnancyPlan.Rehydrate(
            plan.Id,
            plan.Dating,
            plan.Template,
            plan.Language,
            plan.CreatedAt,
            plan.Checks,
            plan.Tasks.Concat(added).ToList(),
            plan.CareTeam,
            plan.Revision,
            plan.Questions,
            plan.Results,
            timeZoneId: plan.TimeZoneId,
            readings: plan.BloodPressure);
    }

    /// <summary>Her doctor visits: entries she added as a visit with a doctor (her next appointment after a visit, say).</summary>
    public static IReadOnlyList<PlannedCheck> DoctorVisits(this PregnancyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Checks.Where(c => c.Kind == EntryKind.DoctorVisit).ToList();
    }
}
