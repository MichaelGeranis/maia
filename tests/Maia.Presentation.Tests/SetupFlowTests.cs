using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Domain.Care;
using Maia.Domain.Plans;
using Maia.Domain.Templates;
using Maia.Presentation.Care;
using Maia.Presentation.Localization;
using Maia.Presentation.Setup;
using Maia.Presentation.Startup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Setup from step 1 to her plan: dates the pickers offer, choices kept through Android closing Maia, and a finish that
/// never keeps a plan from an earlier choice (review #67, #82).
/// </summary>
public sealed class SetupFlowTests : IDisposable
{
    private static readonly PickedContact Doctor = new("Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1");

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    // ---- Step 1: her language ----------------------------------------------------------------

    [Fact]
    public void Step_one_starts_in_the_language_maia_shows()
    {
        var english = StepOne();
        Assert.True(english.IsEnglish);
        Assert.False(english.IsGreek);

        using var greek = new CultureScope("el-GR");
        var vm = StepOne();
        Assert.True(vm.IsGreek);
        Assert.False(vm.IsEnglish);
    }

    [Fact]
    public async Task Choosing_greek_on_step_one_redraws_maia_in_greek_and_comes_back_with_her_date()
    {
        var languages = new RecordingLanguageSwitcher();
        var vm = StepOne(languages);
        vm.Entry.DueDate = new DateTime(2027, 2, 3);

        await vm.ChooseLanguageCommand.ExecuteAsync(Text.Greek);

        Assert.Equal(new[] { Text.Greek }, languages.Switched);
        Assert.Equal(LanguageReturn.Setup, languages.ReturnedTo);

        // As the redrawn app does: setup resumes on step 1, on the date she chose.
        var reopened = new SetupSession(_app.Drafts, _app.Errors);
        Assert.Equal(SetupStep.DueDate, await reopened.RestoreAsync());
        Assert.Equal(new DateOnly(2027, 2, 3), reopened.DueDate);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public async Task Choosing_the_language_already_shown_changes_nothing()
    {
        var languages = new RecordingLanguageSwitcher();
        var vm = StepOne(languages);

        await vm.ChooseLanguageCommand.ExecuteAsync(Text.English);

        Assert.Empty(languages.Switched);
        Assert.Null(await new SetupSession(_app.Drafts, _app.Errors).RestoreAsync());
    }

    // ---- Step 1: the dates each picker offers ----------------------------------------------

    [Fact]
    public void Each_picker_offers_only_the_dates_setup_accepts()
    {
        var vm = StepOne();

        Assert.Equal(new DateTime(2026, 9, 2), vm.Entry.DueMinimumDate);
        Assert.Equal(new DateTime(2027, 7, 7), vm.Entry.DueMaximumDate);
        Assert.Equal(new DateTime(2025, 11, 26), vm.Entry.LastPeriodMinimumDate);
        Assert.Equal(new DateTime(2026, 9, 30), vm.Entry.LastPeriodMaximumDate);
    }

    [Theory]
    [InlineData(280, true)]
    [InlineData(-28, true)]
    [InlineData(281, false)]
    [InlineData(-29, false)]
    public void A_due_date_fits_from_forty_weeks_ahead_to_four_weeks_ago(int daysFromToday, bool fits)
    {
        var vm = StepOne();

        vm.Entry.DueDate = TestData.Today.AddDays(daysFromToday).ToDateTime(TimeOnly.MinValue);

        Assert.Equal(fits, vm.Entry.IsValid);
        Assert.Equal(fits, vm.ContinueCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-308, true)]
    [InlineData(1, false)]
    [InlineData(-309, false)]
    public void A_last_period_fits_from_today_back_to_forty_four_weeks(int daysFromToday, bool fits)
    {
        var vm = StepOne();
        vm.Entry.UseLastPeriod = true;

        vm.Entry.LastPeriodDate = TestData.Today.AddDays(daysFromToday).ToDateTime(TimeOnly.MinValue);

        Assert.Equal(fits, vm.Entry.IsValid);
    }

    [Fact]
    public void Switching_between_due_date_and_last_period_keeps_the_same_pregnancy()
    {
        var vm = StepOne();
        vm.Entry.DueDate = TestData.DueDate.ToDateTime(TimeOnly.MinValue);
        var summary = vm.Entry.Summary;

        vm.Entry.ToggleDateModeCommand.Execute(null);

        Assert.Equal(new DateTime(2026, 4, 12), vm.Entry.LastPeriodDate);
        Assert.Equal(TestData.DueDate, vm.Entry.EffectiveDueDate);
        Assert.Equal(summary, vm.Entry.Summary);

        vm.Entry.LastPeriodDate = new DateTime(2026, 4, 19);
        vm.Entry.ToggleDateModeCommand.Execute(null);

        Assert.Equal(TestData.DueDate.AddDays(7).ToDateTime(TimeOnly.MinValue), vm.Entry.DueDate);
        Assert.Equal(TestData.DueDate.AddDays(7), vm.Entry.EffectiveDueDate);
    }

    // ---- Choices kept through Android closing Maia ---------------------------------------------

    [Fact]
    public async Task Her_choices_come_back_at_the_step_she_was_on_after_android_closes_maia()
    {
        var one = StepOne();
        one.Entry.DueDate = TestData.DueDate.ToDateTime(TimeOnly.MinValue);
        await one.ContinueCommand.ExecuteAsync(null);
        var two = new SetupPlanViewModel(_app.Templates, _app.Setup, _app.Clock, _app.Navigator);
        await two.LoadAsync();
        two.Groups.SelectMany(g => g).Single(o => o.Id == TestData.Growth).IsIncluded = true;
        await two.ContinueCommand.ExecuteAsync(null);
        var three = StepThree();
        await three.LoadAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await three.PickCommand.ExecuteAsync(null);
        await three.ToggleRemindersCommand.ExecuteAsync(null);

        // Android closes Maia; it starts again with an empty session over the same phone.
        using var again = new TestApp();
        foreach (var (name, draft) in _app.Drafts.Drafts)
        {
            again.Drafts.Drafts[name] = draft;
        }

        await Startup(again).OpenAsync();

        Assert.Equal(new[] { "setup", "setup-plan", "setup-care" }, again.Navigator.Visited);
        Assert.Equal(TestData.DueDate, again.Setup.DueDate);
        Assert.True(again.Setup.IncludeOverrides[TestData.Growth]);
        Assert.Equal(Doctor, Assert.Single(again.Setup.ObGyns));
        Assert.True(again.Setup.RemindMe);
        Assert.Empty(again.Errors.Reports);
    }

    [Fact]
    public async Task An_old_draft_whose_due_date_no_longer_fits_goes_back_to_step_one()
    {
        _app.Setup.DueDate = TestData.Today.AddDays(-40);
        await _app.Setup.SaveAsync(SetupStep.Care);

        await Startup(_app).OpenAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Without_a_draft_setup_starts_at_step_one()
    {
        await Startup(_app).OpenAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.Null(_app.Setup.DueDate);
    }

    [Fact]
    public async Task A_contact_picked_as_android_closed_maia_is_added_when_she_comes_back()
    {
        _app.Setup.DueDate = TestData.DueDate;
        _app.Contacts.Interrupted = Doctor;
        var vm = StepThree();

        await vm.LoadAsync();

        Assert.Equal("Dr. Eleni Papadaki", Assert.Single(vm.ObGyns).Name);
        Assert.Equal(Doctor, Assert.Single(_app.Setup.ObGyns));
        Assert.Contains("setup", _app.Drafts.Drafts.Keys);
    }

    [Fact]
    public async Task A_draft_that_cannot_be_kept_never_stops_setup()
    {
        _app.Drafts.FailWrites = new IOException("disk full");
        var vm = StepOne();
        vm.Entry.DueDate = TestData.DueDate.ToDateTime(TimeOnly.MinValue);

        await vm.ContinueCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "setup-plan" }, _app.Navigator.Visited);
        Assert.Equal("setup.draft_save", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task Once_her_plan_is_made_nothing_of_setup_is_kept()
    {
        var vm = await StepThreeWithDueDateAsync();

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Empty(_app.Drafts.Drafts);
        Assert.Null(_app.Setup.DueDate);
    }

    [Fact]
    public async Task A_draft_left_behind_goes_once_her_plan_opens()
    {
        await _app.CreatePlanAsync();
        _app.Drafts.Drafts["setup"] = [1, 2, 3];

        await Startup(_app).OpenAsync();

        Assert.Empty(_app.Drafts.Drafts);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
    }

    // ---- Remind me (owner decision: no calendar sync, reminders from Maia only) -----------------

    [Fact]
    public async Task Remind_me_asks_for_notifications_and_turns_reminders_on_with_her_plan()
    {
        var vm = await StepThreeWithDueDateAsync();
        Assert.True(vm.CanRemind);
        Assert.False(vm.RemindMe);

        await vm.ToggleRemindersCommand.ExecuteAsync(null);

        Assert.True(vm.RemindMe);
        Assert.Equal(1, _app.NotificationAccess.Requests);
        Assert.False(_app.ReminderSettings.IsOn);

        await vm.StartCommand.ExecuteAsync(null);
        await _app.Reminders.WhenIdleAsync();

        Assert.True(_app.ReminderSettings.IsOn);
        Assert.Equal("Your journey is ready.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task If_notifications_are_refused_remind_me_stays_off_and_says_calmly_where_to_allow_them()
    {
        _app.NotificationAccess.WillGrant = false;
        var vm = await StepThreeWithDueDateAsync();

        await vm.ToggleRemindersCommand.ExecuteAsync(null);

        Assert.False(vm.RemindMe);
        Assert.Equal("Maia can't show notifications yet. You can allow them for Maia in your phone's settings.", vm.Notice);

        await vm.StartCommand.ExecuteAsync(null);
        Assert.False(_app.ReminderSettings.IsOn);
    }

    [Fact]
    public async Task Turning_remind_me_off_again_asks_nothing_and_skip_for_now_leaves_reminders_off()
    {
        var vm = await StepThreeWithDueDateAsync();
        await vm.ToggleRemindersCommand.ExecuteAsync(null);
        await vm.ToggleRemindersCommand.ExecuteAsync(null);
        Assert.False(vm.RemindMe);
        Assert.Equal(1, _app.NotificationAccess.Requests);

        await vm.ToggleRemindersCommand.ExecuteAsync(null);
        await vm.SkipCommand.ExecuteAsync(null);

        Assert.False(_app.ReminderSettings.IsOn);
    }

    [Fact]
    public async Task Where_the_phone_sets_no_reminders_remind_me_is_not_offered()
    {
        _app.NotificationAccess.IsSupported = false;

        var vm = await StepThreeWithDueDateAsync();

        Assert.False(vm.CanRemind);
    }

    [Fact]
    public async Task A_draft_saved_with_the_old_calendar_choice_still_comes_back_with_remind_me_off()
    {
        _app.Drafts.Drafts["setup"] = System.Text.Encoding.UTF8.GetBytes(
            """{"version":1,"step":"Care","dueDate":"2027-01-17","includeOverrides":{},"obGyns":[],"connectCalendar":true}""");

        Assert.Equal(SetupStep.Care, await _app.Setup.RestoreAsync());

        Assert.Equal(TestData.DueDate, _app.Setup.DueDate);
        Assert.False(_app.Setup.RemindMe);
        Assert.Empty(_app.Errors.Reports);
    }

    // ---- Finishing -------------------------------------------------------------------------

    [Fact]
    public async Task If_her_doctor_cannot_be_linked_the_plan_still_opens_and_says_where_to_add_them()
    {
        var vm = await StepThreeWithDueDateAsync();
        _app.Setup.ObGyns.Add(new PickedContact(new string('x', CareContact.MaxFieldLength + 1), "+30 690 000 4567", null, null));

        await vm.StartCommand.ExecuteAsync(null);

        Assert.NotNull(_app.Repository.Stored);
        Assert.Empty(_app.Repository.Stored.CareTeam.Contacts);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Your journey is ready. Your doctor couldn't be added this time; you can add them in Settings.", _app.Notifier.Shown.Single());
        Assert.Equal("setup.contacts", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task If_reminders_cannot_be_turned_on_the_plan_still_opens_with_her_doctor()
    {
        _app.Setup.DueDate = TestData.DueDate;
        _app.Setup.ObGyns.Add(Doctor);
        _app.Setup.RemindMe = true;
        var reminders = new PhoneReminders(_app.ReminderScheduler, new BrokenReminderSettings(), _app.Repository, _app.Clock, _app.Errors);
        var vm = new SetupCareViewModel(_app.Setup, _app.Plans, _app.Contacts, reminders, _app.NotificationAccess, _app.Navigator, _app.Notifier, _app.Errors);
        await vm.LoadAsync();

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Single(_app.Repository.Stored!.CareTeam.Contacts);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Your journey is ready.", _app.Notifier.Shown.Single());
        Assert.Equal("setup.reminders", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_plan_made_by_an_earlier_try_takes_the_choices_she_made_since()
    {
        await _app.Plans.CreatePlanAsync(new CreatePlanRequest(TestData.DueDate, "GR", Text.English));
        var vm = await StepThreeWithDueDateAsync();
        _app.Setup.DueDate = TestData.DueDate.AddDays(7);
        _app.Setup.IncludeOverrides[TestData.Anomaly] = false;

        await vm.SkipCommand.ExecuteAsync(null);

        var plan = _app.Repository.Stored!;
        Assert.Equal(TestData.DueDate.AddDays(7), plan.Dating.DueDate);
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Anomaly);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task If_the_plan_cannot_be_made_she_stays_on_step_three_with_her_choices()
    {
        var vm = await StepThreeWithDueDateAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await vm.PickCommand.ExecuteAsync(null);
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Null(_app.Repository.Stored);
        Assert.Equal("Something went wrong. Please try again.", vm.Notice);
        Assert.False(vm.IsBusy);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Equal(Doctor, Assert.Single(_app.Setup.ObGyns));
        Assert.Contains("setup", _app.Drafts.Drafts.Keys);
        Assert.Equal("setup.finish", Assert.Single(_app.Errors.Reports).Operation);
    }

    // ---- A newer template ------------------------------------------------------------------

    [Fact]
    public async Task When_maia_ships_newer_wording_her_plan_takes_it_and_she_is_told_calmly()
    {
        var current = TestData.Template();
        var renamed = current.Checks.Select(c => c.Id == TestData.Glucose
            ? new CheckTemplate(c.Id, TestData.Text("Glucose tolerance test", "Καμπύλη σακχάρου"), c.Description, c.Category, c.Window, c.IncludedByDefault)
            : c);
        using var app = new TestApp(new PlanTemplate(current.Id, current.CountryCode, 2, ReviewStatus.ClinicallyReviewed, renamed, "Dr. Maria Example, OB-GYN", new DateOnly(2026, 9, 1)));
        await app.Repository.SaveAsync(TestData.Plan(), CancellationToken.None);

        await Startup(app).OpenAsync();

        Assert.Equal("Glucose tolerance test", app.Repository.Stored!.GetCheck(TestData.Glucose).Title.In("en"));
        Assert.Equal(new[] { "main" }, app.Navigator.Visited);
        Assert.Equal("Maia has updated how your checks are described. Your bookings and results stay as they are.", app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Without_newer_wording_nothing_is_said()
    {
        await _app.CreatePlanAsync();

        await Startup(_app).OpenAsync();

        Assert.Empty(_app.Notifier.Shown);
    }

    private static StartupViewModel Startup(TestApp app) =>
        new(app.Queries, app.Plans, app.Setup, app.Navigator, app.Notifier, app.Clock, app.Errors, new Localization.PlanLanguageSync(app.Plans, app.Errors), app.Shares, app.Viewer);

    private SetupViewModel StepOne(RecordingLanguageSwitcher? languages = null) => new(_app.Clock, _app.Setup, _app.Navigator, languages ?? new RecordingLanguageSwitcher());

    private SetupCareViewModel StepThree() => _app.CreateSetupCare();

    private async Task<SetupCareViewModel> StepThreeWithDueDateAsync()
    {
        _app.Setup.DueDate = TestData.DueDate;
        var vm = StepThree();
        await vm.LoadAsync();
        return vm;
    }

    /// <summary>Reminder settings the phone can't save just now.</summary>
    private sealed class BrokenReminderSettings : IReminderSettingsStore
    {
        public Task<bool> IsOnAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public Task SetOnAsync(bool enabled, CancellationToken cancellationToken) => Task.FromException(new IOException("preferences unavailable"));

        public Task<ReminderSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(ReminderSettings.Default);

        public Task SaveAsync(ReminderSettings settings, CancellationToken cancellationToken) => Task.FromException(new IOException("preferences unavailable"));
    }
}
