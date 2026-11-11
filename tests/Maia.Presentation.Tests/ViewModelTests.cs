using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Presentation.Plan;
using Maia.Presentation.Setup;
using Maia.Presentation.Week;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class SetupViewModelTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();
    private readonly SetupSession _session = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private SetupViewModel Create() => new(_app.Clock, _session, _app.Navigator, new RecordingLanguageSwitcher());

    [Fact]
    public void Summarises_how_far_along_she_is()
    {
        var vm = Create();

        vm.Entry.DueDate = TestData.DueDate.ToDateTime(TimeOnly.MinValue);

        Assert.True(vm.Entry.IsValid);
        Assert.Equal("You're 24 weeks and 3 days today. Due Sun 17 Jan.", vm.Entry.Summary);
    }

    [Fact]
    public void Works_out_the_due_date_from_the_last_period()
    {
        var vm = Create();

        vm.Entry.UseLastPeriod = true;
        vm.Entry.LastPeriodDate = new DateTime(2026, 4, 12);

        Assert.Equal(TestData.DueDate, vm.Entry.EffectiveDueDate);
    }

    [Fact]
    public void Blocks_dates_that_do_not_fit_a_current_pregnancy()
    {
        var vm = Create();

        vm.Entry.DueDate = new DateTime(2027, 12, 1);

        Assert.False(vm.Entry.IsValid);
        Assert.False(vm.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public void Continue_comes_back_once_the_date_fits_again()
    {
        var vm = Create();
        vm.Entry.DueDate = new DateTime(2027, 12, 1);
        var changes = 0;
        vm.ContinueCommand.CanExecuteChanged += (_, _) => changes++;

        vm.Entry.DueDate = TestData.DueDate.ToDateTime(TimeOnly.MinValue);

        Assert.True(changes > 0);
        Assert.True(vm.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_privacy_notice_is_one_tap_from_the_first_step()
    {
        var vm = Create();

        await vm.OpenPrivacyCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "privacy" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task The_terms_of_use_are_one_tap_from_the_first_step()
    {
        var vm = Create();

        await vm.OpenTermsCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "terms" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Continue_remembers_the_due_date_and_moves_on()
    {
        var vm = Create();
        vm.Entry.DueDate = TestData.DueDate.ToDateTime(TimeOnly.MinValue);

        await vm.ContinueCommand.ExecuteAsync(null);

        Assert.Equal(TestData.DueDate, _session.DueDate);
        Assert.Equal(new[] { "setup-plan" }, _app.Navigator.Visited);
    }
}

public sealed class SetupPlanViewModelTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();
    private readonly SetupSession _session = new() { DueDate = TestData.DueDate };

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private SetupPlanViewModel Create() => new(_app.Templates, _session, _app.Clock, _app.Navigator);

    [Fact]
    public async Task Shows_only_the_checks_still_to_come_by_trimester_without_the_due_date_marker()
    {
        var vm = Create();

        await vm.LoadAsync();

        // At week 24 the first trimester has passed: her past starts empty, so its checks aren't offered at all.
        Assert.Equal(new[] { "2nd trimester", "3rd trimester" }, vm.Groups.Select(g => g.Title));
        Assert.DoesNotContain(vm.Groups.SelectMany(g => g), o => o.Id == TestData.DueDateMilestone);
        Assert.DoesNotContain(vm.Groups.SelectMany(g => g), o => o.Id == TestData.Booking);
        Assert.DoesNotContain(vm.Groups.SelectMany(g => g), o => o.Tag == "Probably done");
    }

    [Fact]
    public async Task A_new_plan_leaves_the_checks_whose_weeks_have_passed_out_and_keeps_the_due_date()
    {
        using var app = new TestApp();
        await app.CreatePlanThroughSetupAsync();

        var plan = await app.Plans.GetPlanAsync();
        var age = plan.AgeOn(TestData.Today);
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Booking);
        Assert.DoesNotContain(plan.Checks, c => !c.IsMilestone && c.Window.HasPassed(age));
        Assert.Contains(plan.Checks, c => c.Id == TestData.DueDateMilestone);
    }

    [Fact]
    public void Setup_opens_on_a_due_date_40_weeks_away()
    {
        using var app = new TestApp();
        var vm = new SetupViewModel(app.Clock, app.Setup, app.Navigator, new RecordingLanguageSwitcher());

        Assert.Equal(app.Clock.Today.AddDays(40 * 7).ToDateTime(TimeOnly.MinValue), vm.Entry.DueDate);
    }

    [Fact]
    public async Task Switches_update_the_choices()
    {
        var vm = Create();
        await vm.LoadAsync();

        vm.Groups.SelectMany(g => g).Single(o => o.Id == TestData.Growth).IsIncluded = true;

        Assert.True(_session.IncludeOverrides[TestData.Growth]);
    }

    [Fact]
    public async Task Looks_good_keeps_the_choices_and_moves_on_to_step_three()
    {
        var vm = Create();
        await vm.LoadAsync();
        vm.Groups.SelectMany(g => g).Single(o => o.Id == TestData.Anomaly).IsIncluded = false;

        await vm.ContinueCommand.ExecuteAsync(null);

        Assert.Null(_app.Repository.Stored);
        Assert.False(_session.IncludeOverrides[TestData.Anomaly]);
        Assert.Equal(new[] { "setup-care" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Without_a_due_date_it_goes_back_to_step_one()
    {
        _session.Reset();
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.Empty(vm.Groups);
    }

    [Fact]
    public async Task Unreviewed_templates_are_explained_not_crashed()
    {
        using var app = new TestApp();
        var vm = new SetupPlanViewModel(new ReviewGateCatalog(), _session, app.Clock, app.Navigator);

        await vm.LoadAsync();

        Assert.Equal("The typical journey isn't ready yet", vm.Title);
        Assert.Equal("This journey is waiting for clinical review and can't be used yet.", vm.ErrorMessage);
        Assert.Equal("Used Maia on another phone? You can bring your journey here with a backup.", vm.ErrorHelp);
        Assert.True(vm.IsBlocked);
        Assert.False(vm.ContinueCommand.CanExecute(null));
        Assert.Empty(vm.Groups);
    }

    [Fact]
    public async Task Unreviewed_templates_get_a_fitting_heading_in_greek_too()
    {
        using var greek = new CultureScope("el-GR");
        var vm = new SetupPlanViewModel(new ReviewGateCatalog(), _session, _app.Clock, _app.Navigator);

        await vm.LoadAsync();

        Assert.Equal("Το συνηθισμένο πλάνο δεν είναι ακόμη έτοιμο", vm.Title);
    }

    [Fact]
    public async Task Unreviewed_templates_still_offer_to_restore_a_backup()
    {
        var vm = new SetupPlanViewModel(new ReviewGateCatalog(), _session, _app.Clock, _app.Navigator);
        await vm.LoadAsync();

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "restore" }, _app.Navigator.Visited);
        Assert.Null(_app.Repository.Stored);
    }

    [Fact]
    public async Task A_suggested_plan_is_not_blocked()
    {
        var vm = Create();

        await vm.LoadAsync();

        Assert.False(vm.IsBlocked);
        Assert.Equal("A typical journey", vm.Title);
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.ErrorHelp);
        Assert.True(vm.ContinueCommand.CanExecute(null));
    }

    private sealed class ReviewGateCatalog : Maia.Application.Abstractions.IPlanTemplateCatalog
    {
        public IReadOnlyList<string> SupportedCountries => ["GR"];

        public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken) =>
            throw new DomainException("template.not_reviewed", "Draft");
    }
}

public sealed class WeekViewModelTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private WeekViewModel Create() => _app.CreateWeek();

    /// <summary>Links Dr. Eleni Papadaki as her OB-GYN.</summary>
    private async Task<CareContactId> LinkDoctorAsync()
    {
        await _app.Plans.LinkContactsAsync([new ContactToLink(Maia.Domain.Care.CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", null, null)]);
        return _app.Repository.Stored!.CareTeam.Contacts.Single().Id;
    }

    [Fact]
    public async Task Next_up_is_her_next_booking_even_with_a_timed_task_before_it()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));
        var vm = Create();

        await vm.LoadAsync();

        Assert.True(vm.HasNextUp);
        Assert.Equal(("Check-up", "Tue 6 Oct at 10:30"), (vm.NextUpTitle, vm.NextUpWhen));

        // The task stays on the card, among this week's to-dos.
        Assert.Contains(vm.Tasks, t => t.Title == "Call the clinic");
    }

    [Fact]
    public async Task Next_up_is_an_entry_she_added_like_any_booking()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        await _app.Plans.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 3), new TimeOnly(17, 0))));
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal(("Dentist", "Sat 3 Oct at 17:00"), (vm.NextUpTitle, vm.NextUpWhen));

        // Not with her OB-GYN: Maia doesn't know who this doctor is, so it says nothing rather than the wrong name.
        Assert.False(vm.HasNextUpWith);
    }

    [Fact]
    public async Task Next_up_moves_on_once_todays_booking_time_has_passed()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        await _app.Plans.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(TestData.Today, new TimeOnly(17, 0))));
        var vm = Create();

        _app.Clock.SetTime(TestData.Today, new TimeOnly(16, 59));
        await vm.LoadAsync();
        Assert.Equal("Dentist", vm.NextUpTitle);

        _app.Clock.SetTime(TestData.Today, new TimeOnly(17, 1));
        await vm.LoadAsync();
        Assert.Equal(("Check-up", "Tue 6 Oct at 10:30"), (vm.NextUpTitle, vm.NextUpWhen));
    }

    [Fact]
    public async Task A_check_from_her_plan_says_who_only_once_she_links_her_ob_gyn()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = Create();

        await vm.LoadAsync();
        Assert.Equal((false, string.Empty), (vm.HasNextUpWith, vm.NextUpWith));

        await LinkDoctorAsync();
        await vm.LoadAsync();
        Assert.Equal((true, "with Dr. Eleni Papadaki"), (vm.HasNextUpWith, vm.NextUpWith));
    }

    [Theory]
    [InlineData(EntryKind.Exam)]
    [InlineData(EntryKind.HospitalVisit)]
    [InlineData(EntryKind.Other)]
    public async Task An_exam_a_hospital_visit_or_something_else_she_added_says_nothing_about_who_it_is_with(EntryKind kind)
    {
        await _app.CreatePlanAsync();
        await LinkDoctorAsync();
        await _app.Plans.AddEntryAsync("Growth scan", kind, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 3), new TimeOnly(9, 0))));
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal("Growth scan", vm.NextUpTitle);
        Assert.Equal((false, string.Empty), (vm.HasNextUpWith, vm.NextUpWith));
    }

    [Fact]
    public async Task A_doctor_visit_with_her_doctor_says_so_unless_its_name_already_does()
    {
        await _app.CreatePlanAsync();
        var doctor = await LinkDoctorAsync();
        var named = EntryTitles.DoctorVisit("Dr. Eleni Papadaki");
        await _app.Plans.AddEntryAsync(named, EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 3), new TimeOnly(9, 0), with: doctor)));
        var vm = Create();

        await vm.LoadAsync();
        Assert.Equal(("Visit with Dr. Eleni Papadaki", false), (vm.NextUpTitle, vm.HasNextUpWith));

        await _app.Plans.AddEntryAsync("Check-up", EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 2), new TimeOnly(9, 0), with: doctor)));
        await vm.LoadAsync();
        Assert.Equal(("Check-up", true, "with Dr. Eleni Papadaki"), (vm.NextUpTitle, vm.HasNextUpWith, vm.NextUpWith));
    }

    [Fact]
    public async Task A_timed_task_says_nothing_about_who_it_is_with()
    {
        await _app.CreatePlanAsync();
        await LinkDoctorAsync();
        await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal((false, string.Empty), (vm.HasNextUpWith, vm.NextUpWith));
    }

    [Fact]
    public async Task Without_a_plan_the_week_sends_her_to_setup()
    {
        await Create().LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Shows_where_she_is_and_what_is_next()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Anomaly, new Appointment(new DateOnly(2026, 9, 2), new TimeOnly(9, 0)));
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal("Week 24 + 3", vm.WeekLabel);
        Assert.Equal("2nd trimester", vm.TrimesterText);
        Assert.Equal("Due Sun 17 Jan", vm.DueText);
        Assert.Equal(171 / 280d, vm.PregnancyProgress, 3);

        Assert.True(vm.HasNextUp);
        Assert.Equal("Check-up", vm.NextUpTitle);
        Assert.Equal("Tue 6 Oct at 10:30", vm.NextUpWhen);
    }

    [Fact]
    public void Your_week_has_no_needs_booking_card_and_no_plan_stats()
    {
        // Change request #1: the first check to book wasn't the full picture, and the plan's counts weren't wanted.
        var page = Repo.CodeOf(Repo.App("Views", "WeekPage.xaml"));
        var keys = Localization.Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture);

        Assert.DoesNotContain("Week_NeedsBooking", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Week_YourPlan", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Segments", page, StringComparison.Ordinal);
        Assert.DoesNotContain(keys, key => key is "Week_NeedsBooking" or "Week_BookBefore" or "Week_Book" or "Week_YourPlan" or "Week_SeeAll"
            || key.StartsWith("Week_Legend", StringComparison.Ordinal));
    }

    [Fact]
    public void Your_week_has_no_greeting_and_opens_on_its_title_under_the_header()
    {
        // The owner's third round: fewer words. "Here's your week" comes straight after the wordmark and week pill.
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "WeekPage.xaml"));
        var screen = page.Descendants().First(e => e.Name.LocalName == "VerticalStackLayout" && AccessibilityTests.Attr(e, "Motions.Rise") == "True");
        var keys = Localization.Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("{loc:Translate Week_Title}", AccessibilityTests.Attr(screen.Elements().ElementAt(1), "Text"));
        Assert.DoesNotContain(page.Descendants(), e => AccessibilityTests.Attr(e, "Text") == "{Binding Greeting}");
        Assert.Null(typeof(WeekViewModel).GetProperty("Greeting"));
        Assert.DoesNotContain(keys, key => key.StartsWith("Week_Good", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_booking_whose_day_has_passed_is_not_next_up()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Anomaly, new Appointment(new DateOnly(2026, 9, 28), new TimeOnly(9, 0)));
        var vm = Create();

        await vm.LoadAsync();

        Assert.False(vm.HasNextUp);
    }

    [Fact]
    public async Task With_nothing_booked_the_hero_says_so()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        await vm.LoadAsync();

        Assert.False(vm.HasNextUp);
        Assert.Equal("Nothing booked yet", vm.NextUpTitle);
    }

    [Fact]
    public async Task Timed_tasks_say_when()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Buy a pillow", new DateOnly(2026, 10, 2));
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal(new[] { "Thu 1 Oct · 09:00", "Fri 2 Oct" }, vm.Tasks.Select(t => t.When));
    }

    [Fact]
    public async Task Ticking_a_task_strikes_it_through_and_keeps_its_row()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Buy a pillow", new DateOnly(2026, 10, 2));
        var vm = Create();
        await vm.LoadAsync();

        await vm.Tasks[0].ToggleCommand.ExecuteAsync(null);

        Assert.True(vm.Tasks[0].IsDone);
        Assert.Equal(2, vm.Tasks.Count);
        Assert.True(_app.Repository.Stored!.Tasks.Single(t => t.Title == "Call the clinic").IsDone);

        await vm.Tasks[0].ToggleCommand.ExecuteAsync(null);

        Assert.False(vm.Tasks[0].IsDone);
        Assert.False(_app.Repository.Stored!.Tasks.Single(t => t.Title == "Call the clinic").IsDone);
    }

    [Fact]
    public async Task A_new_task_has_no_date_until_she_turns_it_on_then_starts_at_now()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();

        vm.OpenTaskFormCommand.Execute(null);
        Assert.False(vm.NewTaskHasDate);
        vm.NewTaskHasDate = true;
        Assert.Equal((new DateTime(2026, 9, 30), new TimeSpan(12, 0, 0)), (vm.NewTaskDate, vm.NewTaskTime));
        Assert.False(vm.AddTaskCommand.CanExecute(null));

        vm.NewTaskTitle = "  Call the clinic about results ";
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.False(vm.IsTaskFormOpen);
        Assert.Equal("Call the clinic about results", vm.Tasks.Single().Title);
        Assert.Equal("Wed 30 Sep · 12:00", vm.Tasks.Single().When);
        Assert.Equal("Task added for Wed 30 Sep at 12:00.", _app.Notifier.Shown.Single());
        Assert.Empty(_app.Notifier.Notices);
    }

    [Fact]
    public async Task A_new_task_without_a_date_asks_for_a_trimester_or_a_week_and_has_no_date_or_time()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();
        vm.OpenTaskFormCommand.Execute(null);
        vm.NewTaskTitle = "Pack the hospital bag";

        // Off as the form opens, with no trimester or week chosen: saving says what is missing and adds nothing.
        Assert.False(vm.NewTaskHasDate);
        Assert.Null(vm.NewTaskPeriod.Mode);
        await vm.AddTaskCommand.ExecuteAsync(null);
        Assert.Equal("Choose when it is.", vm.TaskFormError);
        Assert.Empty(_app.Repository.Stored!.Tasks);
        Assert.True(vm.IsTaskFormOpen);

        // The trimester starts at hers (the 2nd); she chooses the third.
        vm.NewTaskPeriod.ChooseModeCommand.Execute("Trimester");
        Assert.True(vm.NewTaskPeriod.IsSecondTrimester);
        vm.NewTaskPeriod.ChooseTrimesterCommand.Execute("Third");
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Null(vm.TaskFormError);
        var task = Assert.Single(_app.Repository.Stored!.Tasks);
        Assert.Equal(("Pack the hospital bag", (DateOnly?)null, (TimeOnly?)null, (Trimester?)Trimester.Third), (task.Title, task.DueDate, task.DueTime, task.Trimester));

        // Not this week's, so not a row: it is in All tasks, and the toast says it was added.
        Assert.Empty(vm.Tasks);
        Assert.Equal("All tasks", vm.AllTasksText);
        Assert.Equal("Task added.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task The_task_form_closes_without_adding_anything()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();
        vm.OpenTaskFormCommand.Execute(null);
        vm.NewTaskTitle = "Call the clinic";

        vm.CloseTaskFormCommand.Execute(null);

        Assert.False(vm.IsTaskFormOpen);
        Assert.Empty(_app.Repository.Stored!.Tasks);
        vm.OpenTaskFormCommand.Execute(null);
        Assert.Equal(string.Empty, vm.NewTaskTitle);
        Assert.False(vm.NewTaskHasDate);
    }

    [Fact]
    public async Task Tapping_a_task_opens_it_to_change_its_title_and_time()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var vm = Create();
        await vm.LoadAsync();
        var row = vm.Tasks.Single();

        row.EditCommand.Execute(null);
        Assert.True(row.IsEditing);
        Assert.Equal(("Call clinic", true, new DateTime(2026, 10, 1), new TimeSpan(9, 0, 0)), (row.EditTitle, row.EditHasDate, row.EditDate, row.EditTime));

        row.EditTitle = " Call the clinic about results ";
        row.EditDate = new DateTime(2026, 10, 2);
        row.EditTime = new TimeSpan(10, 30, 0);
        await row.SaveCommand.ExecuteAsync(null);

        Assert.False(row.IsEditing);
        Assert.Same(row, vm.Tasks.Single());
        Assert.Equal("Call the clinic about results", row.Title);
        Assert.Equal("Fri 2 Oct · 10:30", row.When);
        Assert.Equal("Task updated.", _app.Notifier.Shown.Single());
        var saved = _app.Repository.Stored!.Tasks.Single();
        Assert.Equal((new DateOnly(2026, 10, 2), new TimeOnly(10, 30)), (saved.DueDate!.Value, saved.DueTime!.Value));
    }

    [Fact]
    public async Task Taking_the_date_off_a_task_leaves_it_undated()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var vm = Create();
        await vm.LoadAsync();
        var row = vm.Tasks.Single();

        row.EditCommand.Execute(null);
        row.EditHasDate = false;

        // Without a date it needs a week or a trimester: until she chooses one, saving says so and keeps it open.
        await row.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Choose when it is.", row.EditError);
        Assert.True(row.IsEditing);
        row.EditPeriod.ChooseModeCommand.Execute("Week");
        Assert.Equal("Week 24", row.EditPeriod.WeekText);
        row.EditPeriod.LaterWeekCommand.Execute(null);
        await row.SaveCommand.ExecuteAsync(null);

        // With no date it is no longer this week's: it leaves the rows for All tasks.
        Assert.Null(row.EditError);
        Assert.Empty(vm.Tasks);
        Assert.Equal("All tasks", vm.AllTasksText);
        Assert.Equal("Task updated.", _app.Notifier.Shown.Single());
        var saved = _app.Repository.Stored!.Tasks.Single();
        Assert.Equal(((DateOnly?)null, (int?)25), (saved.DueDate, saved.Week));
    }

    [Fact]
    public async Task Closing_a_task_editor_lets_the_page_put_the_keyboard_away()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        var vm = Create();
        await vm.LoadAsync();
        var closed = 0;
        vm.EditorClosed += (_, _) => closed++;
        var row = vm.Tasks.Single();

        row.EditCommand.Execute(null);
        row.CancelCommand.Execute(null);
        row.CancelCommand.Execute(null);
        Assert.Equal(1, closed);

        row.EditCommand.Execute(null);
        row.EditTitle = "Call the clinic";
        await row.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, closed);
    }

    [Fact]
    public async Task Deleting_a_task_asks_once_more_then_takes_it_off_the_list()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Buy a pillow", new DateOnly(2026, 10, 2));
        var vm = Create();
        await vm.LoadAsync();
        var call = vm.Tasks[0];
        var pillow = vm.Tasks[1];

        call.EditCommand.Execute(null);
        call.AskDeleteCommand.Execute(null);
        Assert.True(call.IsConfirmingDelete);
        call.KeepItCommand.Execute(null);
        Assert.False(call.IsConfirmingDelete);
        Assert.Equal(2, _app.Repository.Stored!.Tasks.Count);

        call.AskDeleteCommand.Execute(null);
        await call.DeleteCommand.ExecuteAsync(null);

        Assert.Same(pillow, Assert.Single(vm.Tasks));
        Assert.Equal("Task deleted.", _app.Notifier.Shown.Single());
        Assert.Equal("Buy a pillow", Assert.Single(_app.Repository.Stored!.Tasks).Title);
    }

    [Fact]
    public async Task Only_one_task_is_open_at_a_time_and_the_add_form_gives_way()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        await _app.Plans.AddTaskAsync("Buy a pillow", new DateOnly(2026, 10, 2));
        var vm = Create();
        await vm.LoadAsync();

        vm.OpenTaskFormCommand.Execute(null);
        vm.Tasks[0].EditCommand.Execute(null);
        Assert.False(vm.IsTaskFormOpen);

        vm.Tasks[1].EditCommand.Execute(null);
        Assert.Equal(new[] { false, true }, vm.Tasks.Select(t => t.IsEditing));

        vm.OpenTaskFormCommand.Execute(null);
        Assert.All(vm.Tasks, t => Assert.False(t.IsEditing));

        vm.Tasks[0].EditCommand.Execute(null);
        vm.Tasks[0].EditCommand.Execute(null);
        Assert.False(vm.Tasks[0].IsEditing);
    }

    [Fact]
    public async Task Your_week_lists_this_weeks_tasks_and_one_all_tasks_line()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        await _app.Plans.AddTaskAsync("Order the pram", new DateOnly(2026, 11, 20), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Buy a pillow", new DateOnly(2026, 10, 6), new TimeOnly(18, 0));
        var ticked = await _app.Plans.AddTaskAsync("Book the antenatal class", new DateOnly(2026, 10, 2));
        await _app.Plans.SetTaskDoneAsync(ticked, true);
        var vm = Create();

        await vm.LoadAsync();

        // Today and the six days after it are this week; an overdue task is never listed above them.
        Assert.Equal(new[] { "Call clinic", "Buy a pillow" }, vm.Tasks.Select(t => t.Title));
        Assert.True(vm.HasThisWeek);
        Assert.Equal("All tasks", vm.AllTasksText);
        Assert.Equal("All tasks", vm.AllTasksText);
        Assert.False(vm.CanShowAll);
        Assert.False(vm.HasNothingThisWeek);
    }

    [Fact]
    public async Task The_cards_lines_read_without_counts_in_english_and_greek()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1));
        await _app.Plans.AddTaskAsync("Order the pram", new DateOnly(2026, 11, 20));
        await _app.Plans.AddTaskAsync("Send the form", new DateOnly(2026, 9, 28));
        var vm = Create();

        await vm.LoadAsync();
        Assert.Equal("All tasks", vm.AllTasksText);

        using (new CultureScope("el-GR"))
        {
            await vm.LoadAsync();
            Assert.Equal("Όλες οι εργασίες", vm.AllTasksText);
        }

        Assert.Null(typeof(WeekViewModel).GetProperty("ThisWeekCount"));
        Assert.Null(typeof(WeekViewModel).GetProperty("DaysToGoText"));
        Assert.Null(typeof(WeekViewModel).GetProperty("NextUpEyebrow"));
    }

    [Fact]
    public async Task With_nothing_due_this_week_the_card_says_so_and_still_offers_all_tasks()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Order the pram", new DateOnly(2026, 11, 20));
        var vm = Create();

        await vm.LoadAsync();

        Assert.Empty(vm.Tasks);
        Assert.True(vm.HasNothingThisWeek);
        Assert.False(vm.HasThisWeek);
        Assert.Equal("All tasks", vm.AllTasksText);
    }

    [Fact]
    public async Task Hundreds_of_tasks_never_build_more_than_twenty_five_rows_on_your_week()
    {
        // A restored backup can hold far more tasks than she can add herself; Your week is the first screen, and each
        // row is real views on the phone, so the card stays this size whatever the plan holds.
        await _app.CreatePlanAsync();
        var many = Enumerable.Range(0, 900).Select(i => (i % 5) switch
        {
            0 => ((DateOnly?)TestData.Today.AddDays(i % 7), (TimeOnly?)new TimeOnly(9, 0), false),
            1 => (TestData.Today.AddDays(-1 - (i % 90)), null, false),
            2 => (TestData.Today.AddDays(7 + (i % 100)), null, false),
            3 => (null, null, false),
            _ => (TestData.Today.AddDays(i % 30), null, true),
        });
        await _app.Repository.SaveAsync(_app.Repository.Stored!.WithTasks(many), CancellationToken.None);
        var vm = Create();
        var rows = new HashSet<TaskItem>();
        vm.Tasks.CollectionChanged += (_, e) => rows.UnionWith(e.NewItems?.OfType<TaskItem>() ?? []);

        await vm.LoadAsync();
        Assert.Equal(WeekViewModel.CollapsedRows, vm.Tasks.Count);
        Assert.Equal((true, "Show all"), (vm.CanShowAll, vm.ShowAllText));
        Assert.Equal("All tasks", vm.AllTasksText);

        vm.ShowAllCommand.Execute(null);
        await vm.LoadAsync();
        Assert.Equal(WeekViewModel.ExpandedRows, vm.Tasks.Count);
        Assert.False(vm.CanShowAll);

        // Twenty task rows were ever made, and the card has one line besides: All tasks.
        Assert.Equal(WeekViewModel.ExpandedRows, rows.Count);
    }

    [Fact]
    public async Task Show_all_opens_the_card_in_place_and_it_folds_back_when_she_leaves()
    {
        await _app.CreatePlanAsync();
        for (var i = 0; i < 7; i++)
        {
            await _app.Plans.AddTaskAsync($"Task {i + 1}", TestData.Today.AddDays(i));
        }

        var vm = Create();
        await vm.LoadAsync();
        Assert.Equal((5, true, "Show all"), (vm.Tasks.Count, vm.CanShowAll, vm.ShowAllText));
        var first = vm.Tasks.ToList();
        await vm.Tasks[0].ToggleCommand.ExecuteAsync(null);

        vm.ShowAllCommand.Execute(null);

        // The rows she had stay as she left them (the first one ticked), and two more follow.
        Assert.Equal(7, vm.Tasks.Count);
        Assert.Equal(first, vm.Tasks.Take(5));
        Assert.True(vm.Tasks[0].IsDone);
        Assert.False(vm.CanShowAll);

        vm.Leave();

        Assert.Equal(first, vm.Tasks);
        Assert.True(vm.CanShowAll);
    }

    [Fact]
    public async Task All_tasks_opens_your_tasks_from_the_top()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();

        await vm.OpenTasksCommand.ExecuteAsync(null);

        Assert.Equal("tasks", _app.Navigator.Visited[^1]);
        Assert.Null(_app.TasksFocus.Take());
    }

    [Fact]
    public async Task A_task_added_for_this_week_shows_in_the_card_and_glows_once()
    {
        await _app.CreatePlanAsync();
        for (var i = 0; i < 6; i++)
        {
            await _app.Plans.AddTaskAsync($"Task {i + 1}", TestData.Today.AddDays(i), new TimeOnly(9, 0));
        }

        var vm = Create();
        await vm.LoadAsync();
        vm.OpenTaskFormCommand.Execute(null);
        vm.NewTaskTitle = "Pack the hospital bag";
        vm.NewTaskHasDate = true;
        vm.NewTaskDate = new DateTime(2026, 10, 6);
        vm.NewTaskTime = new TimeSpan(20, 0, 0);

        await vm.AddTaskCommand.ExecuteAsync(null);

        // It comes last this week, past the first five rows: the card opens so she sees it.
        var added = vm.Tasks.Single(t => t.Title == "Pack the hospital bag");
        Assert.Equal(7, vm.Tasks.Count);
        Assert.True(added.IsNew);
        Assert.Equal(1, vm.Tasks.Count(t => t.IsNew));

        await vm.LoadAsync();
        Assert.False(added.IsNew);
    }

    [Fact]
    public async Task A_task_added_for_later_is_in_all_tasks_and_the_toast_says_when()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();
        vm.OpenTaskFormCommand.Execute(null);
        vm.NewTaskTitle = "Order the pram";
        vm.NewTaskHasDate = true;
        vm.NewTaskDate = new DateTime(2026, 11, 20);

        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Empty(vm.Tasks);
        Assert.Equal("All tasks", vm.AllTasksText);
        Assert.Equal("Task added for Fri 20 Nov at 12:00.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_full_list_is_said_calmly_without_the_success_tick_and_says_where_to_clear_it()
    {
        await _app.CreatePlanAsync();
        var full = Enumerable.Repeat(((DateOnly?)null, (TimeOnly?)null, true), Maia.Domain.Plans.PregnancyPlan.MaxTasks);
        await _app.Repository.SaveAsync(_app.Repository.Stored!.WithTasks(full), CancellationToken.None);
        var vm = Create();
        await vm.LoadAsync();
        vm.OpenTaskFormCommand.Execute(null);
        vm.NewTaskTitle = "Call the clinic";
        vm.NewTaskPeriod.ChooseModeCommand.Execute("Week");

        await vm.AddTaskCommand.ExecuteAsync(null);

        var notice = Assert.Single(_app.Notifier.Notices);
        Assert.Equal("Your list is full: Maia keeps up to 500 tasks, done ones too. Open “All tasks” to clear the done ones.", notice);
        Assert.True(vm.IsTaskFormOpen);
    }

    [Fact]
    public async Task Until_its_first_load_your_week_shows_neither_nothing_booked_nor_an_empty_list()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        Assert.False(vm.IsLoaded);
        Assert.Equal(string.Empty, vm.NextUpTitle);

        await vm.LoadAsync();

        Assert.True(vm.IsLoaded);
        Assert.Equal("Nothing booked yet", vm.NextUpTitle);

        // On screen, the next-up card and the task card wait for it; calm shapes, with no words, stand in their place.
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "WeekPage.xaml"));
        var waiting = page.Descendants().Where(e => AccessibilityTests.Attr(e, "IsVisible") == "{Binding IsLoaded, Converter={StaticResource InvertedBool}}").ToList();
        var placeholder = Assert.Single(waiting);
        Assert.Equal("{loc:Translate Week_Loading}", AccessibilityTests.Attr(placeholder, "SemanticProperties.Description"));
        Assert.DoesNotContain(placeholder.Descendants(), e => AccessibilityTests.Attr(e, "Text") is not null);
        var loaded = page.Descendants().Where(e => AccessibilityTests.Attr(e, "IsVisible") == "{Binding IsLoaded}").SelectMany(e => e.Descendants()).ToList();
        Assert.Contains(loaded, e => AccessibilityTests.Attr(e, "Text") == "{Binding NextUpTitle}");
        Assert.Contains(loaded, e => AccessibilityTests.Attr(e, "Text") == "{loc:Translate Week_NoTasks}");
        Assert.Contains(loaded, e => AccessibilityTests.Attr(e, "Text") == "{Binding WeekLabel}");
    }

    [Fact]
    public async Task Next_up_never_shows_a_task_even_one_due_later_today()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call the clinic", TestData.Today, new TimeOnly(16, 0));
        _app.Clock.SetTime(TestData.Today, new TimeOnly(15, 0));
        var vm = Create();

        await vm.LoadAsync();

        Assert.False(vm.HasNextUp);
        Assert.Equal("Nothing booked yet", vm.NextUpTitle);
        Assert.Contains(vm.Tasks, t => t.Title == "Call the clinic");
    }

    [Fact]
    public async Task Coming_back_keeps_the_rows_she_sees_and_only_a_new_task_is_added()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        await _app.Plans.AddTaskAsync("Buy a pillow", new DateOnly(2026, 10, 3));
        var vm = Create();
        await vm.LoadAsync();
        var rows = vm.Tasks.ToList();
        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        vm.Tasks.CollectionChanged += (_, e) => changes.Add(e.Action);

        await vm.LoadAsync();
        Assert.Equal(rows, vm.Tasks);
        Assert.Empty(changes);

        vm.OpenTaskFormCommand.Execute(null);
        vm.NewTaskTitle = "Order the pram";
        vm.NewTaskHasDate = true;
        vm.NewTaskDate = new DateTime(2026, 10, 2);
        await vm.AddTaskCommand.ExecuteAsync(null);

        Assert.Equal(new[] { System.Collections.Specialized.NotifyCollectionChangedAction.Add }, changes);
        Assert.Same(rows[0], vm.Tasks[0]);
        Assert.Contains(rows[1], vm.Tasks);
    }

    [Fact]
    public async Task Tapping_next_up_opens_journey_on_that_booking()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = Create();
        await vm.LoadAsync();

        await vm.OpenNextUpCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "plan" }, _app.Navigator.Visited);
        Assert.Equal(TestData.Checkup25, _app.Focus.Take());
    }

    [Fact]
    public async Task With_nothing_booked_next_up_opens_journey_at_today()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();

        await vm.OpenNextUpCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "plan" }, _app.Navigator.Visited);
        Assert.False(_app.Focus.IsPending);
    }

    [Fact]
    public void Below_this_weeks_rows_is_one_all_tasks_line_not_a_line_per_group()
    {
        var week = System.Xml.Linq.XDocument.Load(Repo.App("Views", "WeekPage.xaml"));
        var opens = week.Descendants().Where(e => e.Name.LocalName == "TapGestureRecognizer"
            && AccessibilityTests.Attr(e, "Command") == "{Binding OpenTasksCommand}").ToList();

        var line = Assert.Single(opens).Parent!.Parent!;
        Assert.Equal("Grid", line.Name.LocalName);
        Assert.Null(AccessibilityTests.Attr(line, "IsVisible"));
        Assert.Contains(line.Descendants(), e => AccessibilityTests.Attr(e, "Text") == "{Binding AllTasksText}");
        string[] gone = ["{Binding HasLater}", "{Binding HasNoDate}", "{Binding HasEarlier}", "{Binding HasDone}"];
        Assert.DoesNotContain(week.Descendants(), e => gone.Contains(AccessibilityTests.Attr(e, "IsVisible")));
    }

    [Fact]
    public void The_next_up_card_is_one_tap_to_journey_with_no_call_or_questions_button_on_it()
    {
        var week = System.Xml.Linq.XDocument.Load(Repo.App("Views", "WeekPage.xaml"));
        var nextUp = week.Descendants().Single(e => e.Name.LocalName == "Border"
            && e.Descendants().Any(d => AccessibilityTests.Attr(d, "Text") == "{Binding NextUpTitle}"));

        var tap = Assert.Single(nextUp.Descendants(), e => e.Name.LocalName == "TapGestureRecognizer");
        Assert.Equal("{Binding OpenNextUpCommand}", AccessibilityTests.Attr(tap, "Command"));
        Assert.Equal("Button", AccessibilityTests.Attr(nextUp, "Accessible.Role"));
        Assert.DoesNotContain(nextUp.Descendants(), e => AccessibilityTests.Attr(e, "Data") == "{StaticResource IconPhone}");
    }

    [Fact]
    public async Task A_refresh_reads_the_plan_once()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync([new ContactToLink(Maia.Domain.Care.CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1")]);
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = Create();
        var before = _app.Repository.LoadCount;

        await vm.LoadAsync();

        Assert.Equal(before + 1, _app.Repository.LoadCount);
        Assert.Equal("with Dr. Eleni Papadaki", vm.NextUpWith);
    }

    [Fact]
    public async Task A_plan_worth_keeping_with_no_backup_suggests_one_calmly()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        _app.Clock.SetToday(TestData.Today.AddDays(7));
        var vm = Create();

        await vm.LoadAsync();

        Assert.True(vm.ShowBackupReminder);
        Assert.Equal("Your journey lives only on this phone. A backup file keeps it and your results safe for a new phone.", vm.BackupReminderText);
        await vm.BackUpCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "settings" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_new_plan_or_one_with_nothing_of_her_own_is_not_met_with_a_backup_reminder()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        _app.Clock.SetToday(TestData.Today.AddDays(30));
        await vm.LoadAsync();
        Assert.False(vm.ShowBackupReminder);

        _app.Clock.SetToday(TestData.Today);
        await _app.Plans.AddQuestionAsync("Is swimming fine?");
        await vm.LoadAsync();
        Assert.False(vm.ShowBackupReminder);
    }

    [Fact]
    public async Task Not_now_puts_the_backup_reminder_away_for_a_month()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        _app.Clock.SetToday(TestData.Today.AddDays(8));
        var vm = Create();
        await vm.LoadAsync();

        vm.DismissBackupReminderCommand.Execute(null);

        Assert.False(vm.ShowBackupReminder);
        Assert.Equal(_app.Clock.UtcNow, _app.BackupHistory.ReminderDismissed);
        _app.Clock.SetToday(TestData.Today.AddDays(8 + 29));
        await vm.LoadAsync();
        Assert.False(vm.ShowBackupReminder);
        _app.Clock.SetToday(TestData.Today.AddDays(8 + 30));
        await vm.LoadAsync();
        Assert.True(vm.ShowBackupReminder);
    }

    [Fact]
    public async Task A_recent_backup_keeps_the_reminder_away_and_one_a_month_old_brings_it_back()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        _app.BackupHistory.BackupMade(_app.Clock.UtcNow);
        var vm = Create();

        _app.Clock.SetToday(TestData.Today.AddDays(20));
        await vm.LoadAsync();
        Assert.False(vm.ShowBackupReminder);

        _app.Clock.SetToday(TestData.Today.AddDays(31));
        await vm.LoadAsync();
        Assert.True(vm.ShowBackupReminder);
        Assert.Equal("Your last backup is from 30 Sep. A new one keeps everything added since.", vm.BackupReminderText);
    }

    [Fact]
    public async Task Open_questions_show_as_ready_on_the_tile_and_not_on_next_up()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddQuestionAsync("Is it still safe to cycle to work?");
        await _app.Plans.AddQuestionAsync("When should the hospital bag be ready?");
        var answered = await _app.Plans.AddQuestionAsync("Can we talk through pain relief options?");
        await _app.Plans.SetQuestionAnsweredAsync(answered, true);
        var vm = Create();

        await vm.LoadAsync();

        // Questions are tied to no appointment: the Next up card has no "Questions ready" pill.
        Assert.Null(typeof(WeekViewModel).GetProperty("HasQuestions"));
        Assert.Equal("Ready when you need them", vm.QuestionsTileText);
    }

    [Fact]
    public async Task With_nothing_to_ask_the_tile_invites_one()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal("Jot one down when it comes up", vm.QuestionsTileText);
    }

    [Fact]
    public async Task A_visit_before_the_next_booked_check_comes_first()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        await _app.Plans.AddEntryAsync(EntryTitles.DoctorVisit((string?)null), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 2), new TimeOnly(11, 0))));
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal("Appointment", vm.NextUpTitle);
        Assert.Equal("Fri 2 Oct at 11:00", vm.NextUpWhen);
    }

    [Fact]
    public async Task The_shortcuts_open_questions_and_breathe()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        await vm.OpenQuestionsCommand.ExecuteAsync(null);
        await vm.BreatheCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "questions", "breathe" }, _app.Navigator.Visited);
    }
}
