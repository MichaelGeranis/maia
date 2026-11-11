using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Application.Tests;

/// <summary>
/// Changes to a plan that don't come from a tap on a check: a newer template (ADR 0003), setup finished again on a plan
/// it already made, and the names Maia gives the exams and visits she adds.
/// </summary>
public sealed class PlanUpdateTests : IDisposable
{
    private readonly FakeClock _clock = new(TestData.Today);
    private readonly InMemoryPlanRepository _plans = new();
    private readonly InMemoryAttachmentStore _attachments = new();
    private readonly RecordingErrorReporter _errors = new();
    private readonly ShippedTemplates _templates = new();
    private readonly PlanService _service;

    public PlanUpdateTests()
    {
        _service = new PlanService(_plans, _templates, _attachments, new RecordingDataKey(), new InMemoryBackupFormat(), _clock, _errors);
    }

    public void Dispose() => _service.Dispose();

    private Task<PregnancyPlan> CreateAsync(IReadOnlyDictionary<CheckId, bool>? overrides = null) =>
        _service.CreatePlanAsync(new CreatePlanRequest(TestData.DueDate, "GR", LocalizedText.English, overrides));

    // ---- A newer template -----------------------------------------------------------------

    [Fact]
    public async Task A_plan_takes_the_wording_and_weeks_of_a_newer_template_only_for_checks_she_has_not_acted_on()
    {
        await CreateAsync();
        var checkupSlot = new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30));
        await _service.BookCheckAsync(TestData.Checkup25, checkupSlot);
        await _service.EditEntryAsync(TestData.Anomaly, "Anomaly scan", EntryWhen.DoneInWeek(22));
        _templates.Template = Version2(ReviewStatus.ClinicallyReviewed);

        Assert.True(await _service.UpdateFromTemplateAsync());

        var plan = _plans.Stored!;
        Assert.Equal(new TemplateReference("test-gr", 2), plan.Template);
        var glucose = plan.GetCheck(TestData.Glucose);
        Assert.Equal("Glucose tolerance test", glucose.Title.In("en"));
        Assert.Equal("Καμπύλη σακχάρου (OGTT)", glucose.Title.In("el"));
        Assert.Equal("Fasting, about two hours.", glucose.Description!.In("en"));
        Assert.Equal(new WeekWindow(25, 28), glucose.Window);

        // Done and booked: they stay where she put them.
        Assert.Equal((CheckState.Done, 22), (plan.GetCheck(TestData.Anomaly).State, plan.GetCheck(TestData.Anomaly).Week));
        var checkup = plan.GetCheck(TestData.Checkup25);
        Assert.Equal((checkupSlot, 25), (checkup.Appointment, checkup.Week));

        // She left the growth scan off at setup, so a newer template does not bring it.
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Growth);
    }

    [Fact]
    public async Task A_check_a_newer_template_adds_does_not_appear_on_her_journey()
    {
        await CreateAsync();
        var current = TestData.Template();
        var gbs = new CheckTemplate(
            new CheckId("gbs"), TestData.Text("GBS swab", "Καλλιέργεια GBS"), TestData.Text("A quick swab.", "Ένα γρήγορο δείγμα."), CheckCategory.Routine, new WeekWindow(36, 37), includedByDefault: true);
        _templates.Template = new PlanTemplate(current.Id, current.CountryCode, 2, ReviewStatus.ClinicallyReviewed, current.Checks.Append(gbs), "Dr. Maria Example, OB-GYN", new DateOnly(2026, 9, 1));

        Assert.False(await _service.UpdateFromTemplateAsync());

        var queries = new PlanQueries(_plans, _clock);
        Assert.Equal(2, _plans.Stored!.Template.Version);
        Assert.DoesNotContain(_plans.Stored!.Checks, c => c.Id == gbs.Id);
        Assert.DoesNotContain((await queries.GetTimelineAsync())!.Sections.SelectMany(s => s.Checks), v => v.Check.Id == gbs.Id);
    }

    [Fact]
    public async Task A_milestone_a_newer_template_adds_shows_on_her_timeline()
    {
        await CreateAsync();
        var current = TestData.Template();
        var term = new CheckTemplate(
            new CheckId("full-term"), TestData.Text("Full term", "Τελειόμηνη"), TestData.Text("From here on.", "Από εδώ και πέρα."), CheckCategory.Milestone, new WeekWindow(37, 37), includedByDefault: true);
        _templates.Template = new PlanTemplate(current.Id, current.CountryCode, 2, ReviewStatus.ClinicallyReviewed, current.Checks.Append(term), "Dr. Maria Example, OB-GYN", new DateOnly(2026, 9, 1));

        Assert.True(await _service.UpdateFromTemplateAsync());

        var view = (await new PlanQueries(_plans, _clock).GetTimelineAsync())!.Sections.SelectMany(s => s.Checks).Single(v => v.Check.Id == term.Id);
        Assert.Equal((CheckStatus.Milestone, "Full term"), (view.Status, view.Title));
    }

    [Fact]
    public async Task A_milestone_a_newer_template_turns_into_a_check_stays_a_milestone_with_no_reminder()
    {
        await CreateAsync();
        var current = TestData.Template();
        var exam = current.Checks.Select(c => c.Id == TestData.DueDateMilestone
            ? new CheckTemplate(c.Id, TestData.Text("Term check", "Έλεγχος τελειόμηνης"), c.Description, CheckCategory.Routine, c.Window, includedByDefault: true)
            : c);
        _templates.Template = new PlanTemplate(current.Id, current.CountryCode, 2, ReviewStatus.ClinicallyReviewed, exam, "Dr. Maria Example, OB-GYN", new DateOnly(2026, 9, 1));
        var now = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.FromHours(3));

        Assert.False(await _service.UpdateFromTemplateAsync());

        var dueDate = _plans.Stored!.GetCheck(TestData.DueDateMilestone);
        Assert.Equal(2, _plans.Stored!.Template.Version);
        Assert.True(dueDate.IsMilestone);
        Assert.Equal("Due date", dueDate.Title.In("en"));
        Assert.DoesNotContain(ReminderPlanner.For(_plans.Stored!, ReminderSettings.Default, now, FakeClock.Athens), r => r.Text.Contains("Term check", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Her_own_checks_stay_as_she_named_them()
    {
        await CreateAsync();
        var own = await _service.AddEntryAsync("Iron infusion", EntryKind.Exam, EntryWhen.NotBookedIn(Trimester.Third));
        _templates.Template = Version2(ReviewStatus.ClinicallyReviewed);

        await _service.UpdateFromTemplateAsync();

        Assert.Equal("Iron infusion", _plans.Stored!.GetCheck(own).Title.In("en"));
    }

    [Fact]
    public async Task Nothing_is_saved_when_no_newer_template_ships()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;

        Assert.False(await _service.UpdateFromTemplateAsync());

        Assert.Equal(saves, _plans.SaveCount);
        Assert.Equal(1, _plans.Stored!.Template.Version);
    }

    [Fact]
    public async Task A_release_build_never_brings_draft_wording_into_her_plan()
    {
        await CreateAsync();
        _templates.Template = Version2(ReviewStatus.Draft);
        _templates.RefusesDrafts = true;

        Assert.False(await _service.UpdateFromTemplateAsync());

        Assert.Equal(1, _plans.Stored!.Template.Version);
        Assert.Equal("Glucose test", _plans.Stored!.GetCheck(TestData.Glucose).Title.In("en"));
    }

    [Fact]
    public async Task A_plan_made_from_a_draft_keeps_working_on_a_build_that_refuses_drafts()
    {
        var reviewed = TestData.Template();
        _templates.Template = new PlanTemplate(reviewed.Id, reviewed.CountryCode, 1, ReviewStatus.Draft, reviewed.Checks);
        await CreateAsync();
        _templates.RefusesDrafts = true;

        Assert.False(await _service.UpdateFromTemplateAsync());
        await _service.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));

        Assert.Equal(CheckState.Booked, _plans.Stored!.GetCheck(TestData.Glucose).State);
    }

    [Fact]
    public async Task A_backup_from_an_older_template_is_restored_with_the_newer_wording()
    {
        await CreateAsync();
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.DeleteEverythingAsync();
        _templates.Template = Version2(ReviewStatus.ClinicallyReviewed);
        file.Position = 0;

        var restored = await _service.RestoreBackupAsync(file, "sea-breeze-17");

        Assert.Equal(2, restored.Template.Version);
        Assert.Equal("Glucose tolerance test", _plans.Stored!.GetCheck(TestData.Glucose).Title.In("en"));
    }

    [Fact]
    public async Task A_backup_restored_on_a_build_that_refuses_drafts_keeps_its_own_wording()
    {
        await CreateAsync();
        using var file = new MemoryStream();
        await _service.ExportBackupAsync(file, "sea-breeze-17");
        await _service.DeleteEverythingAsync();
        _templates.Template = Version2(ReviewStatus.Draft);
        _templates.RefusesDrafts = true;
        file.Position = 0;

        var restored = await _service.RestoreBackupAsync(file, "sea-breeze-17");

        Assert.Equal(1, restored.Template.Version);
        Assert.Equal("Glucose test", _plans.Stored!.GetCheck(TestData.Glucose).Title.In("en"));
    }

    // ---- Setup finished again ---------------------------------------------------------------

    [Fact]
    public async Task Finishing_setup_again_brings_the_plan_in_line_with_her_new_choices()
    {
        // A check still to come: one whose weeks have passed is not in a plan made today.
        await CreateAsync(new Dictionary<CheckId, bool> { [TestData.Glucose] = true });
        var newDue = TestData.DueDate.AddDays(7);

        await _service.ApplySetupAsync(new CreatePlanRequest(
            newDue,
            "GR",
            LocalizedText.English,
            new Dictionary<CheckId, bool> { [TestData.Glucose] = false, [TestData.Growth] = true, [TestData.DueDateMilestone] = false }));

        var plan = _plans.Stored!;
        Assert.Equal(newDue, plan.Dating.DueDate);
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Glucose);
        Assert.Equal(CheckState.NotBooked, plan.GetCheck(TestData.Growth).State);

        // The due date is not hers to switch off.
        Assert.True(plan.GetCheck(TestData.DueDateMilestone).IsMilestone);
    }

    [Fact]
    public async Task Finishing_setup_again_does_not_bring_a_check_whose_weeks_have_passed()
    {
        await CreateAsync();

        await _service.ApplySetupAsync(new CreatePlanRequest(
            TestData.DueDate, "GR", LocalizedText.English, new Dictionary<CheckId, bool> { [TestData.Nipt] = true, [TestData.Booking] = true }));

        Assert.DoesNotContain(_plans.Stored!.Checks, c => c.Id == TestData.Nipt || c.Id == TestData.Booking);
    }

    [Fact]
    public async Task Finishing_setup_again_with_the_same_choices_saves_nothing()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;

        await _service.ApplySetupAsync(new CreatePlanRequest(
            TestData.DueDate, "GR", LocalizedText.English, new Dictionary<CheckId, bool> { [TestData.Glucose] = true, [TestData.Growth] = false }));

        Assert.Equal(saves, _plans.SaveCount);
    }

    [Fact]
    public async Task Finishing_setup_again_leaves_an_entry_she_added_on_her_journey()
    {
        await CreateAsync();
        var own = await _service.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.Third));

        await _service.ApplySetupAsync(new CreatePlanRequest(
            TestData.DueDate, "GR", LocalizedText.English, new Dictionary<CheckId, bool> { [TestData.Glucose] = false }));

        Assert.Contains(_plans.Stored!.Checks, c => c.Id == own);
    }

    [Fact]
    public async Task Finishing_setup_again_leaves_a_booked_check_booked()
    {
        await CreateAsync();
        await _service.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));

        await _service.ApplySetupAsync(new CreatePlanRequest(
            TestData.DueDate, "GR", LocalizedText.English, new Dictionary<CheckId, bool> { [TestData.Glucose] = false }));

        Assert.Equal(CheckState.Booked, _plans.Stored!.GetCheck(TestData.Glucose).State);
    }

    // ---- Names Maia gives what she adds ------------------------------------------------------

    [Fact]
    public async Task A_suggested_exam_keeps_its_name_in_both_languages()
    {
        await CreateAsync();

        await _service.AddEntryAsync(TestData.Text("Iron recheck", "Επανέλεγχος σιδήρου"), EntryKind.Exam, EntryWhen.NotBookedIn(Trimester.Third));
        await _service.ChangeLanguageAsync(LocalizedText.Greek);

        var added = Assert.Single(_plans.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal("Iron recheck", added.Title.In("en"));
        Assert.Equal("Επανέλεγχος σιδήρου", added.Title.In(_plans.Stored!.Language));
    }

    [Fact]
    public async Task With_no_doctor_linked_a_visit_is_an_appointment_that_follows_her_language()
    {
        await CreateAsync();

        await _service.AddEntryAsync(
            EntryTitles.DoctorVisit((string?)null), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0))));
        await _service.ChangeLanguageAsync(LocalizedText.Greek);

        var visit = Assert.Single(_plans.Stored!.DoctorVisits());
        Assert.Equal(("Appointment", "Ραντεβού"), (visit.Title.In(LocalizedText.English), visit.Title.In(LocalizedText.Greek)));
    }

    /// <summary>The test template's next version: new wording for the glucose test, and new weeks for four checks.</summary>
    private static PlanTemplate Version2(ReviewStatus status)
    {
        var current = TestData.Template();
        var checks = current.Checks.Select(c => c.Id.Value switch
        {
            "ogtt" => new CheckTemplate(c.Id, TestData.Text("Glucose tolerance test", "Καμπύλη σακχάρου (OGTT)"), TestData.Text("Fasting, about two hours.", "Νηστεία, περίπου δύο ώρες."), c.Category, new WeekWindow(25, 28), c.IncludedByDefault),
            "anomaly" => new CheckTemplate(c.Id, c.Title, c.Description, c.Category, new WeekWindow(19, 23), c.IncludedByDefault),
            "checkup-25" => new CheckTemplate(c.Id, c.Title, c.Description, c.Category, new WeekWindow(26, 26), c.IncludedByDefault),
            "growth" => new CheckTemplate(c.Id, c.Title, c.Description, c.Category, new WeekWindow(30, 34), c.IncludedByDefault),
            _ => c,
        });
        return status == ReviewStatus.ClinicallyReviewed
            ? new PlanTemplate(current.Id, current.CountryCode, 2, status, checks, "Dr. Maria Example, OB-GYN", new DateOnly(2026, 9, 1))
            : new PlanTemplate(current.Id, current.CountryCode, 2, status, checks);
    }

    /// <summary>The templates a build ships; a release build refuses a draft, as the real catalog does.</summary>
    private sealed class ShippedTemplates : IPlanTemplateCatalog
    {
        public PlanTemplate Template { get; set; } = TestData.Template();

        public bool RefusesDrafts { get; set; }

        public IReadOnlyList<string> SupportedCountries => [Template.CountryCode];

        public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken) =>
            RefusesDrafts && Template.ReviewStatus == ReviewStatus.Draft
                ? throw new DomainException("template.not_reviewed", "Not reviewed.")
                : Task.FromResult(Template);
    }
}
