using System.Xml.Linq;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Plan;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Journey shows the pregnancy's weeks: each week has its heading, "Week 25" and its days, and one with nothing in it
/// says so. Her tasks stand on their day, at their week, or first in their trimester. Today is 24+3 (Wed 30 Sep 2026),
/// due Sun 17 Jan.
/// </summary>
public sealed class JourneyWeekTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Every_week_from_the_first_to_the_due_date_has_its_heading_and_a_row_with_only_a_trimester_none()
    {
        await _app.CreatePlanAsync();
        var early = await _app.Plans.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.Second));
        var bag = await _app.Plans.AddTaskAsync("Pack the hospital bag", trimester: Trimester.Third);
        var classes = await _app.Plans.AddTaskAsync("Book the antenatal class", week: 28);
        var pram = await _app.Plans.AddTaskAsync("Order the pram", week: 30);
        var form = await _app.Plans.AddTaskAsync("Register the birth", trimester: Trimester.AfterBirth);
        var vm = await OpenAsync();

        Assert.Equal(
            [
                "section:trimester1", "week:8", "check:booking", .. Weeks(9, 13),
                "section:trimester2", "check:" + early, .. Weeks(14, 19), "week:20", "check:anomaly", .. Weeks(21, 23),
                "week:24", "check:ogtt", "here", "week:25", "check:checkup-25", .. Weeks(26, 27),
                "section:trimester3", "task:" + bag, "week:28", "task:" + classes, "week:29", "week:30", "task:" + pram,
                .. Weeks(31, 39), "week:40", "check:due-date",
                "section:after_birth", "task:" + form,
            ],
            vm.Rows.Select(r => r.Key));
        Assert.Equal(
            Enumerable.Range(8, 33).Where(w => w is not (8 or 20 or 24 or 25 or 28 or 30 or 40)),
            vm.Rows.OfType<WeekRow>().Where(w => w.IsEmpty).Select(w => w.Week));
    }

    [Fact]
    public async Task A_week_reads_its_number_and_its_days_and_an_empty_one_says_so()
    {
        await _app.CreatePlanAsync();

        var vm = await OpenAsync();

        var thisWeek = Week(vm, 24);
        Assert.Equal(("Week 24", "27 Sep – 3 Oct", false), (thisWeek.Title, thisWeek.Dates, thisWeek.IsEmpty));
        var next = Week(vm, 25);
        Assert.Equal(("Week 25", "4–10 Oct", "Week 25, 4–10 Oct"), (next.Title, next.Dates, next.SpokenName));
        Assert.True(Week(vm, 26).IsEmpty);
    }

    [Fact]
    public async Task After_birth_only_the_weeks_with_something_in_them_show()
    {
        await _app.CreatePlanAsync();
        var visit = await _app.Plans.AddEntryAsync("Six-week check", EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(43));

        var vm = await OpenAsync();

        Assert.Equal(
            ["section:after_birth", "week:43", "check:" + visit],
            vm.Rows.Select(r => r.Key).SkipWhile(k => k != "section:after_birth"));
    }

    [Fact]
    public async Task You_are_here_comes_first_under_its_week_when_nothing_else_is_there()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.DeleteEntryAsync(TestData.Glucose);

        var vm = await OpenAsync();

        var here = vm.Rows.IndexOf(vm.Rows.Single(r => r.Key == "here"));
        Assert.Equal("week:24", vm.Rows[here - 1].Key);
    }

    [Fact]
    public async Task A_task_says_its_week_or_trimester_and_one_saved_with_neither_stays_off_the_timeline()
    {
        await _app.CreatePlanAsync();
        await _app.Repository.SaveAsync(_app.Repository.Stored!.WithTasks([(null, null, false)]), CancellationToken.None);
        var bag = await _app.Plans.AddTaskAsync("Pack the hospital bag", trimester: Trimester.Third);
        var classes = await _app.Plans.AddTaskAsync("Book the antenatal class", week: 28);
        var form = await _app.Plans.AddTaskAsync("Register the birth", trimester: Trimester.AfterBirth);

        var vm = await OpenAsync();

        Assert.Equal(3, vm.Rows.OfType<TaskRow>().Count());
        Assert.Equal("3rd trimester", TaskOn(vm, bag).When);
        Assert.Equal("Pack the hospital bag, 3rd trimester", TaskOn(vm, bag).SpokenName);
        Assert.Equal("Week 28", TaskOn(vm, classes).When);
        Assert.Equal("After birth", TaskOn(vm, form).When);
    }

    [Fact]
    public async Task In_greek_the_weeks_and_trimesters_read_in_greek()
    {
        await _app.CreatePlanAsync();
        var form = await _app.Plans.AddTaskAsync("Δήλωση γέννησης", trimester: Trimester.AfterBirth);
        using var greek = new CultureScope("el-GR");

        var vm = await OpenAsync();

        Assert.Equal("Μετά τη γέννα", TaskOn(vm, form).When);
        Assert.Equal("Εβδομάδα 25, 4–10 Οκτ", Week(vm, 25).SpokenName);
    }

    [Fact]
    public void A_week_heading_shows_its_number_a_hairline_its_days_and_nothing_this_week_when_empty()
    {
        var page = XDocument.Load(Repo.App("Views", "PlanPage.xaml"));
        var template = page.Descendants().Single(e => e.Name.LocalName == "DataTemplate" && AccessibilityTests.Attr(e, "Key") == "WeekTemplate");
        var texts = template.Descendants().Select(e => AccessibilityTests.Attr(e, "Text")).OfType<string>();

        Assert.Equal(new[] { "{Binding Title}", "{Binding Dates}", "{loc:Translate Plan_EmptyWeek}" }, texts);
        Assert.Contains(template.Descendants(), e => AccessibilityTests.Attr(e, "SemanticProperties.Description") == "{Binding SpokenName}");
        Assert.Contains(template.Descendants(), e => AccessibilityTests.Attr(e, "IsVisible") == "{Binding IsEmpty}");
        Assert.DoesNotContain("WeekMark", File.ReadAllText(Repo.App("Views", "PlanPage.xaml")), StringComparison.Ordinal);
    }

    private static IEnumerable<string> Weeks(int from, int to) => Enumerable.Range(from, to - from + 1).Select(w => "week:" + w);

    private static WeekRow Week(PlanViewModel vm, int week) => vm.Rows.OfType<WeekRow>().Single(r => r.Week == week);

    private async Task<PlanViewModel> OpenAsync()
    {
        var vm = new PlanViewModel(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);
        await vm.LoadAsync();
        return vm;
    }

    private static TaskRow TaskOn(PlanViewModel vm, TaskId id) => vm.Rows.OfType<TaskRow>().Single(r => r.Id == id);
}
