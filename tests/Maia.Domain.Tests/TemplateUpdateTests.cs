using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>A plan made from an older version of its template takes the newer one's wording (ADR 0003).</summary>
public sealed class TemplateUpdateTests
{
    [Fact]
    public void Only_a_newer_version_of_the_same_template_changes_the_plan()
    {
        var plan = TestData.Plan();
        var current = TestData.Template();

        Assert.False(plan.UpdateFromTemplate(current));
        Assert.False(plan.UpdateFromTemplate(new PlanTemplate("another", "GR", 5, ReviewStatus.ClinicallyReviewed, Renamed(current))));
        Assert.Empty(plan.DequeueEvents());
        Assert.Equal(new TemplateReference("test-gr", 1), plan.Template);
    }

    [Fact]
    public void A_newer_version_renames_checks_and_is_remembered()
    {
        var plan = TestData.Plan();
        var revision = plan.Revision;

        Assert.True(plan.UpdateFromTemplate(new PlanTemplate("test-gr", "GR", 2, ReviewStatus.ClinicallyReviewed, Renamed(TestData.Template()))));

        Assert.Equal("Glucose tolerance test", plan.GetCheck(TestData.Glucose).Title.In("en"));
        Assert.Equal(new TemplateReference("test-gr", 2), plan.Template);
        Assert.Equal(new PlanTemplateUpdated(1, 2), Assert.Single(plan.DequeueEvents()));
        Assert.True(plan.Revision > revision);
    }

    [Fact]
    public void Weeks_move_only_for_checks_she_has_not_acted_on()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        plan.Reschedule(TestData.Booking, EntryWhen.DoneInWeek(9));
        var moved = TestData.Template().Checks.Select(c => new CheckTemplate(c.Id, c.Title, c.Description, c.Category, new WeekWindow(c.Window.FromWeek + 1, c.Window.ToWeek + 1), c.IncludedByDefault));

        plan.UpdateFromTemplate(new PlanTemplate("test-gr", "GR", 2, ReviewStatus.ClinicallyReviewed, moved));

        Assert.Equal(WeekWindow.OfWeek(26), plan.GetCheck(TestData.Glucose).Window);
        Assert.Equal(WeekWindow.OfWeek(9), plan.GetCheck(TestData.Booking).Window);
        Assert.Equal(new WeekWindow(26, 26), plan.GetCheck(TestData.Checkup25).Window);
        Assert.Equal(new WeekWindow(21, 25), plan.GetCheck(TestData.Anomaly).Window);
        Assert.Equal(new WeekWindow(41, 41), plan.GetCheck(TestData.DueDateMilestone).Window);
        Assert.Equal(plan.Checks.OrderBy(c => c.Window.FromWeek).Select(c => c.Id), plan.Checks.Select(c => c.Id));
    }

    [Fact]
    public void A_check_she_renamed_keeps_her_name()
    {
        var plan = TestData.Plan();
        plan.Rename(TestData.Glucose, LocalizedText.Plain("My sugar test"));

        plan.UpdateFromTemplate(new PlanTemplate("test-gr", "GR", 2, ReviewStatus.ClinicallyReviewed, Renamed(TestData.Template())));

        Assert.Equal("My sugar test", plan.GetCheck(TestData.Glucose).Title.In("en"));
    }

    [Fact]
    public void A_check_she_renamed_to_a_name_in_both_languages_keeps_it()
    {
        var plan = TestData.Plan();
        plan.Rename(TestData.Glucose, TestData.Text("Sugar test", "Τεστ σακχάρου"));

        plan.UpdateFromTemplate(new PlanTemplate("test-gr", "GR", 2, ReviewStatus.ClinicallyReviewed, Renamed(TestData.Template())));

        Assert.Equal("Sugar test", plan.GetCheck(TestData.Glucose).Title.In("en"));
    }

    [Fact]
    public void A_check_she_moved_to_a_trimester_stays_there()
    {
        var plan = TestData.Plan();
        plan.Reschedule(TestData.Glucose, EntryWhen.NotBookedIn(Trimester.Third));
        var moved = TestData.Template().Checks.Select(c => new CheckTemplate(c.Id, c.Title, c.Description, c.Category, new WeekWindow(c.Window.FromWeek + 1, c.Window.ToWeek + 1), c.IncludedByDefault));

        plan.UpdateFromTemplate(new PlanTemplate("test-gr", "GR", 2, ReviewStatus.ClinicallyReviewed, moved));

        var glucose = plan.GetCheck(TestData.Glucose);
        Assert.Equal((Trimester.Third, WeekWindow.OfTrimester(Trimester.Third)), (glucose.TrimesterOnly!.Value, glucose.Window));
    }

    [Fact]
    public void A_check_a_newer_version_adds_does_not_join_her_journey_without_her()
    {
        var plan = TestData.Plan();
        var ids = plan.Checks.Select(c => c.Id).ToList();

        var changed = plan.UpdateFromTemplate(Version(2, TestData.Template().Checks.Append(GbsSwab())));

        Assert.False(changed);
        Assert.Equal(ids, plan.Checks.Select(c => c.Id));
        Assert.Equal(new TemplateReference("test-gr", 2), plan.Template);
        Assert.Equal(new PlanTemplateUpdated(1, 2), Assert.Single(plan.DequeueEvents()));
    }

    [Fact]
    public void A_check_one_version_adds_takes_the_wording_of_the_next_once_she_has_chosen_it()
    {
        var plan = TestData.Plan();
        var second = Version(2, TestData.Template().Checks.Append(GbsSwab()));
        plan.UpdateFromTemplate(second);
        plan.ApplySetupChoices(second, new Dictionary<CheckId, bool> { [Gbs] = true }, TestData.Today);

        Assert.True(plan.UpdateFromTemplate(Version(3, TestData.Template().Checks.Append(GbsSwab("Group B strep swab")))));

        var swab = Assert.Single(plan.Checks, c => c.Id == Gbs);
        Assert.Equal("Group B strep swab", swab.Title.In("en"));
        Assert.Equal((CheckOrigin.Template, CheckState.NotBooked), (swab.Origin, swab.State));
    }

    [Fact]
    public void A_milestone_a_newer_version_adds_shows_on_her_timeline()
    {
        var plan = TestData.Plan();
        var term = new CheckId("full-term");
        var milestone = new CheckTemplate(
            term, TestData.Text("Full term", "Τελειόμηνη κύηση"), TestData.Text("From 37 weeks.", "Από τις 37 εβδομάδες."), CheckCategory.Milestone, new WeekWindow(37, 37), includedByDefault: true);

        Assert.True(plan.UpdateFromTemplate(Version(2, TestData.Template().Checks.Append(milestone))));

        Assert.True(plan.GetCheck(term).IsMilestone);
        Assert.Equal(new[] { term, TestData.DueDateMilestone }, plan.Checks.TakeLast(2).Select(c => c.Id));
    }

    [Fact]
    public void A_newer_category_is_taken_and_what_she_switched_off_stays_off()
    {
        var plan = TestData.Plan();

        plan.UpdateFromTemplate(Version(2, TestData.Template().Checks.Select(c =>
            c.Id == TestData.Anomaly ? With(c, CheckCategory.IfAdvised)
            : c.Id == TestData.Nipt ? new CheckTemplate(c.Id, c.Title, c.Description, CheckCategory.Routine, c.Window, includedByDefault: true)
            : c)));

        Assert.Equal(CheckCategory.IfAdvised, plan.GetCheck(TestData.Anomaly).Category);
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Nipt);
    }

    [Fact]
    public void A_check_becomes_a_milestone_only_while_she_has_not_acted_on_it()
    {
        var plan = TestData.Plan();
        var booking = new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));
        plan.Book(TestData.Glucose, booking);

        Assert.True(plan.UpdateFromTemplate(Version(
            2, TestData.Template().Checks.Select(c => c.Id == TestData.Glucose || c.Id == TestData.Checkup25 ? With(c, CheckCategory.Milestone, "A date") : c))));

        var glucose = plan.GetCheck(TestData.Glucose);
        Assert.False(glucose.IsMilestone);
        Assert.Equal(CheckState.Booked, glucose.State);
        Assert.Equal(booking, glucose.Appointment);
        Assert.Equal("Glucose test", glucose.Title.In("en"));
        var checkup = plan.GetCheck(TestData.Checkup25);
        Assert.True(checkup.IsMilestone);
        Assert.Equal(CheckState.NotBooked, checkup.State);
        Assert.Equal("A date", checkup.Title.In("en"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_milestone_a_newer_version_turns_into_a_check_stays_a_milestone(bool includedByDefault)
    {
        var plan = TestData.Plan();

        Assert.False(plan.UpdateFromTemplate(Version(2, TestData.Template().Checks.Select(c => c.Id == TestData.DueDateMilestone
            ? new CheckTemplate(c.Id, TestData.Text("Exam", "Εξέταση"), c.Description, CheckCategory.Routine, c.Window, includedByDefault)
            : c))));

        var due = plan.GetCheck(TestData.DueDateMilestone);
        Assert.True(due.IsMilestone);
        Assert.Equal(CheckCategory.Milestone, due.Category);
        Assert.Equal("Due date", due.Title.In("en"));
        Assert.Equal(new TemplateReference("test-gr", 2), plan.Template);
    }

    [Fact]
    public void A_check_a_newer_version_drops_stays_in_her_plan_as_it_is()
    {
        var plan = TestData.Plan();
        var booking = new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));
        plan.Book(TestData.Glucose, booking);

        plan.UpdateFromTemplate(Version(2, TestData.Template().Checks.Where(c => c.Id != TestData.Glucose)));

        Assert.Equal(booking, plan.GetCheck(TestData.Glucose).Appointment);
        Assert.Equal(new TemplateReference("test-gr", 2), plan.Template);
    }

    private static readonly CheckId Gbs = new("gbs");

    private static PlanTemplate Version(int version, IEnumerable<CheckTemplate> checks) =>
        new("test-gr", "GR", version, ReviewStatus.ClinicallyReviewed, checks);

    private static CheckTemplate GbsSwab(string english = "GBS swab") =>
        new(Gbs, TestData.Text(english, "Καλλιέργεια GBS"), TestData.Text("A quick swab.", "Ένα γρήγορο δείγμα."), CheckCategory.Routine, new WeekWindow(36, 37), includedByDefault: true);

    private static CheckTemplate With(CheckTemplate check, CheckCategory category, string? english = null) =>
        new(check.Id, english is null ? check.Title : TestData.Text(english, english), check.Description, category, check.Window, check.IncludedByDefault);

    private static IEnumerable<CheckTemplate> Renamed(PlanTemplate template) =>
        template.Checks.Select(c => c.Id == TestData.Glucose
            ? new CheckTemplate(c.Id, TestData.Text("Glucose tolerance test", "Καμπύλη σακχάρου"), c.Description, c.Category, c.Window, c.IncludedByDefault)
            : c);
}
