using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Application.Tests;

/// <summary>Which reminders a plan needs on the phone (review #22).</summary>
public sealed class ReminderPlannerTests
{
    private static readonly TimeZoneInfo Athens = FakeClock.Athens;
    private static readonly TimeSpan Summer = TimeSpan.FromHours(3);
    private static readonly TimeSpan Winter = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, Summer);
    private static readonly Appointment GlucoseSlot = new(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));

    private static ReminderSettings Chosen(ReminderPreference when = ReminderPreference.DayBefore, bool discreet = false) =>
        new() { When = when, DiscreetTitles = discreet };

    private static IReadOnlyList<PhoneReminder> For(PregnancyPlan plan, ReminderSettings? settings = null, DateTimeOffset? now = null) =>
        ReminderPlanner.For(plan, settings ?? Chosen(), now ?? Now, Athens);

    [Fact]
    public void A_booking_is_reminded_24_hours_before_and_two_hours_before_as_she_chose()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, GlucoseSlot);

        var reminders = For(plan, Chosen(ReminderPreference.Both)).Where(r => r.Key.StartsWith("check:", StringComparison.Ordinal)).ToList();

        Assert.Equal(
            new[]
            {
                ("check:ogtt:daybefore", new DateTimeOffset(2026, 10, 11, 8, 30, 0, Summer), "Glucose test", "Tomorrow at 08:30"),
                ("check:ogtt:soon", new DateTimeOffset(2026, 10, 12, 6, 30, 0, Summer), "Glucose test", "Today at 08:30"),
            },
            reminders.Select(r => (r.Key, r.At, r.Title, r.Text)));
        Assert.All(reminders, r => Assert.Equal("Maia reminder", r.LockScreenTitle));
        Assert.All(reminders, r => Assert.Equal("Appointment", r.DiscreetTitle));
    }

    [Theory]
    [InlineData(8, 30, "Today at 08:30")]
    [InlineData(2, 0, "Today at 02:00")]
    [InlineData(1, 30, "At 01:30")]
    [InlineData(0, 45, "Tomorrow at 00:45")]
    public void The_short_notice_words_stay_true_even_if_the_phone_brings_it_an_hour_late(int hour, int minute, string text)
    {
        var plan = TestData.Plan();
        plan.AddTask("Take the iron tablet", new DateOnly(2026, 10, 12), new TimeOnly(hour, minute));

        var soon = For(plan, Chosen(ReminderPreference.TwoHoursBefore)).Single(r => r.Key.EndsWith(":soon", StringComparison.Ordinal));

        Assert.Equal(text, soon.Text);
        Assert.DoesNotContain("2 hours", soon.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_day_booked_without_a_time_with_two_hours_chosen_rings_the_evening_before()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, GlucoseSlot);
        var visit = plan.AddEntry(EntryTitles.DoctorVisit((CareContact?)null), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 20))));

        var twoHours = For(plan, Chosen(ReminderPreference.TwoHoursBefore));
        var evening = For(plan).Single(r => r.Key.StartsWith($"check:{visit}", StringComparison.Ordinal));

        Assert.Equal(new[] { "check:ogtt:soon", $"check:{visit}:daybefore" }, twoHours.Select(r => r.Key));
        Assert.Equal((new DateTimeOffset(2026, 10, 19, 20, 0, 0, Summer), "Tomorrow"), (evening.At, evening.Text));
        Assert.Equal(evening.At, twoHours[1].At);
    }

    [Theory]
    [InlineData(EntryKind.DoctorVisit, "Visit with Dr. Eleni Papadaki")]
    [InlineData(EntryKind.HospitalVisit, "Hospital tour")]
    public void An_entry_she_booked_is_reminded_by_its_name_like_any_booking(EntryKind kind, string title)
    {
        var plan = TestData.Plan();
        var doctor = new CareContact(CareContactId.New(), CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567");
        plan.LinkContact(doctor);
        var id = plan.AddEntry(LocalizedText.Plain(title), kind, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0), with: doctor.Id)));

        var reminder = For(plan).Single(r => r.Key == $"check:{id}:daybefore");
        Assert.Equal((title, "Appointment", "Tomorrow at 11:00"), (reminder.Title, reminder.DiscreetTitle, reminder.Text));

        var discreet = For(plan, Chosen(discreet: true)).Single(r => r.Key == $"check:{id}:daybefore");
        Assert.Equal("Appointment", discreet.Title);
    }

    [Fact]
    public void Timed_tasks_are_reminded_and_plain_to_dos_are_not()
    {
        var plan = TestData.Plan();
        var call = plan.AddTask("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));
        plan.AddTask("Pack the hospital bag", trimester: Trimester.Third);
        var done = plan.AddTask("Send the form", new DateOnly(2026, 10, 3), new TimeOnly(9, 0));
        plan.SetTaskDone(done, done: true);

        var reminder = Assert.Single(For(plan), r => r.Key.StartsWith("task:", StringComparison.Ordinal));

        Assert.Equal(($"task:{call}:daybefore", "Call the clinic", "Reminder", "Tomorrow at 09:00"), (reminder.Key, reminder.Title, reminder.DiscreetTitle, reminder.Text));
    }

    [Fact]
    public void Discreet_titles_keep_every_name_off_the_notification()
    {
        var plan = TestData.Plan(new Dictionary<CheckId, bool> { [TestData.Growth] = true });
        plan.Book(TestData.Glucose, GlucoseSlot);
        plan.AddTask("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));

        var reminders = For(plan, Chosen(discreet: true));

        Assert.Equal(new[] { "Reminder", "Appointment" }, reminders.Select(r => r.Title));
        Assert.All(reminders, r => Assert.DoesNotContain("Glucose", r.Title + r.Text, StringComparison.Ordinal));
        Assert.All(reminders, r => Assert.DoesNotContain("Growth", r.Title + r.Text, StringComparison.Ordinal));
        Assert.All(reminders, r => Assert.DoesNotContain("clinic", r.Title + r.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void What_is_kept_for_after_a_restart_never_names_a_check_or_a_task()
    {
        var plan = TestData.Plan(new Dictionary<CheckId, bool> { [TestData.Growth] = true });
        plan.Book(TestData.Glucose, GlucoseSlot);
        plan.AddTask("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));

        var reminders = For(plan);

        Assert.Contains(reminders, r => r.Title == "Glucose test");
        Assert.All(reminders, r => Assert.DoesNotContain("Glucose", r.DiscreetTitle + r.DiscreetText, StringComparison.Ordinal));
        Assert.All(reminders, r => Assert.DoesNotContain("Growth", r.DiscreetTitle + r.DiscreetText, StringComparison.Ordinal));
        Assert.All(reminders, r => Assert.DoesNotContain("clinic", r.DiscreetTitle + r.DiscreetText, StringComparison.Ordinal));
    }

    [Fact]
    public void Nothing_rings_to_ask_her_to_book_an_entry_not_booked_yet()
    {
        // The growth scan's weeks (32-34) are ahead and an iron recheck waits in week 32: neither is booked, so no reminder.
        var plan = TestData.Plan(new Dictionary<CheckId, bool> { [TestData.Growth] = true });
        plan.AddEntry(LocalizedText.Plain("Iron recheck"), EntryKind.Exam, EntryWhen.NotBookedInWeek(32));
        plan.AddEntry(LocalizedText.Plain("Dentist"), EntryKind.DoctorVisit, EntryWhen.NotBookedIn(Trimester.Third));

        Assert.Empty(For(plan, Chosen(ReminderPreference.Both)));
        Assert.Empty(For(plan, Chosen(ReminderPreference.Both), new DateTimeOffset(2026, 11, 8, 8, 0, 0, Winter)));
    }

    [Fact]
    public void An_entry_done_or_moved_off_its_booking_rings_nothing()
    {
        var plan = TestData.Plan();
        var visit = plan.AddEntry(LocalizedText.Plain("Visit with my doctor"), EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2027, 1, 5), new TimeOnly(9, 30))));
        plan.Book(TestData.Glucose, GlucoseSlot);

        plan.Reschedule(visit, EntryWhen.NotBookedIn(Trimester.Third));
        plan.Reschedule(TestData.Glucose, EntryWhen.DoneInWeek(24));

        Assert.Empty(For(plan, Chosen(ReminderPreference.Both)));
    }

    [Fact]
    public void A_booking_whose_time_has_passed_rings_nothing()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, GlucoseSlot);

        Assert.Empty(For(plan, Chosen(ReminderPreference.Both), new DateTimeOffset(2026, 10, 12, 9, 0, 0, Summer)));
    }

    [Fact]
    public void A_booking_made_less_than_a_day_ahead_gets_no_reminder_with_24_hours_before_chosen()
    {
        // Booked at 14:00 for 08:30 tomorrow: 24 hours before has gone by, and nothing rings in its place.
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, GlucoseSlot);

        Assert.Empty(For(plan, now: new DateTimeOffset(2026, 10, 11, 14, 0, 0, Summer)));
    }

    [Fact]
    public void A_timed_task_made_less_than_a_day_ahead_gets_no_reminder_with_24_hours_before_chosen()
    {
        var plan = TestData.Plan();
        var task = plan.AddTask("Pack the hospital bag", new DateOnly(2026, 10, 12), new TimeOnly(9, 0));

        Assert.Empty(For(plan, now: new DateTimeOffset(2026, 10, 11, 22, 0, 0, Summer)));
        Assert.NotEqual(default, task);
    }

    [Fact]
    public void A_day_booked_without_a_time_after_its_evening_gets_no_reminder()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12)));

        Assert.Empty(For(plan, Chosen(ReminderPreference.Both), new DateTimeOffset(2026, 10, 11, 21, 0, 0, Summer)));
    }

    [Theory]
    [InlineData(11, 8, "check:ogtt:daybefore check:ogtt:soon")]
    [InlineData(11, 9, "check:ogtt:soon")]
    [InlineData(12, 7, "")]
    public void With_both_chosen_each_rings_only_while_it_is_still_ahead(int day, int hour, string keys)
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, GlucoseSlot);

        var reminders = For(plan, Chosen(ReminderPreference.Both), new DateTimeOffset(2026, 10, day, hour, 0, 0, Summer));

        Assert.Equal(keys, string.Join(' ', reminders.Select(r => r.Key)));
    }

    [Fact]
    public void Only_reminders_still_ahead_are_set_soonest_first_and_no_more_than_the_phone_allows()
    {
        var plan = TestData.Plan();
        for (var day = 0; day < ReminderPlanner.MaxSet + 10; day++)
        {
            plan.AddTask("Stretch", new DateOnly(2026, 9, 30).AddDays(day), new TimeOnly(18, 0));
        }

        var reminders = For(plan);

        // Today's task is less than a day ahead, so it gets none; tomorrow's, 24 hours before, is the soonest.
        Assert.Equal(ReminderPlanner.MaxSet, reminders.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 18, 0, 0, Summer), reminders[0].At);
        Assert.Equal(reminders.OrderBy(r => r.At), reminders);
    }

    [Fact]
    public void Times_are_home_times_wherever_the_phone_is()
    {
        var plan = TestData.Plan();
        plan.Book(TestData.Glucose, GlucoseSlot);
        var inLondon = new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.FromHours(1));

        var reminder = ReminderPlanner.For(plan, Chosen(), inLondon, Athens).Single(r => r.Key.StartsWith("check:", StringComparison.Ordinal));

        Assert.Equal(new DateTimeOffset(2026, 10, 11, 5, 30, 0, TimeSpan.Zero), reminder.At.ToUniversalTime());
    }

    [Fact]
    public void Reminders_speak_the_plans_language()
    {
        var plan = TestData.Plan();
        plan.ChangeLanguage(LocalizedText.Greek);
        plan.Book(TestData.Glucose, GlucoseSlot);

        var reminder = For(plan).Single(r => r.Key.StartsWith("check:", StringComparison.Ordinal));

        Assert.Equal(("Καμπύλη σακχάρου", "Αύριο στις 08:30", "Ραντεβού", "Υπενθύμιση από τη Maia"), (reminder.Title, reminder.Text, reminder.DiscreetTitle, reminder.LockScreenTitle));
    }
}

/// <summary>Reminders on this phone through the use cases: turned on and off, after each change, and on failure (review #22).</summary>
public sealed class PhoneRemindersTests : IDisposable
{
    private static readonly Appointment GlucoseSlot = new(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));

    private readonly FakeClock _clock = new(TestData.Today);
    private readonly InMemoryPlanRepository _plans = new();
    private readonly RecordingErrorReporter _errors = new();
    private readonly FakeReminderScheduler _scheduler = new();
    private readonly InMemoryReminderSettingsStore _settings = new();
    private readonly PhoneReminders _reminders;
    private readonly PlanService _service;

    public PhoneRemindersTests()
    {
        _reminders = new PhoneReminders(_scheduler, _settings, _plans, _clock, _errors);
        _service = new PlanService(_plans, new StubTemplateCatalog(), new InMemoryAttachmentStore(), new RecordingDataKey(), new InMemoryBackupFormat(), _clock, _errors, _reminders);
    }

    public void Dispose() => _service.Dispose();

    private async Task CreateAsync() =>
        await _service.CreatePlanAsync(new CreatePlanRequest(TestData.DueDate, "gr", LocalizedText.English));

    private Task SettledAsync() => _reminders.WhenIdleAsync();

    [Fact]
    public async Task Reminders_are_off_until_she_turns_them_on()
    {
        await CreateAsync();
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();
        Assert.Empty(_scheduler.Set);

        await _reminders.SetOnAsync(true);

        Assert.True(await _reminders.IsOnAsync());
        Assert.Equal("Glucose test", Assert.Single(_scheduler.Set).Title);
    }

    [Fact]
    public async Task Every_saved_change_sets_them_again()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);

        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();
        Assert.Equal("check:ogtt:daybefore", Assert.Single(_scheduler.Set).Key);

        await _service.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 14), new TimeOnly(8, 30)));
        await SettledAsync();
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 8, 30, 0, TimeSpan.FromHours(3)), Assert.Single(_scheduler.Set).At);

        await _service.EditEntryAsync(TestData.Glucose, "Glucose test", EntryWhen.NotBookedIn(Trimester.Second));
        await SettledAsync();
        Assert.Empty(_scheduler.Set);
    }

    [Fact]
    public async Task Renaming_a_booked_entry_sets_its_reminder_again_under_the_new_name()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);

        await _service.EditEntryAsync(TestData.Glucose, "Sugar curve", EntryWhen.Booked(GlucoseSlot));
        await SettledAsync();

        Assert.Equal("Sugar curve", Assert.Single(_scheduler.Set).Title);
    }

    [Fact]
    public async Task Deleting_a_booked_entry_takes_its_reminders_with_it()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        var visit = await _service.AddEntryAsync("Dentist", EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0))));
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();
        Assert.Equal(2, _scheduler.Set.Count);

        await _service.DeleteEntryAsync(visit);
        await SettledAsync();

        Assert.Equal("check:ogtt:daybefore", Assert.Single(_scheduler.Set).Key);
    }

    [Fact]
    public async Task A_plan_she_has_just_set_up_rings_nothing_until_she_books()
    {
        await CreateAsync();

        await _reminders.SetOnAsync(true);

        Assert.Empty(_scheduler.Set);
    }

    [Fact]
    public async Task A_change_that_leaves_her_plan_as_it_was_saves_nothing_and_sets_nothing_again()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        var task = await _service.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 12), new TimeOnly(9, 0));
        await _service.SetTaskDoneAsync(task, done: true);
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        var question = await _service.AddQuestionAsync("Is swimming fine?");
        await SettledAsync();
        var (saves, replaced) = (_plans.SaveCount, _scheduler.Replaced);

        await _service.SetTaskDoneAsync(task, done: true);
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await _service.EditQuestionAsync(question, "Is swimming fine?");
        await SettledAsync();

        Assert.Equal((saves, replaced), (_plans.SaveCount, _scheduler.Replaced));
    }

    [Fact]
    public async Task Her_questions_results_and_care_team_do_not_set_them_again()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();
        var replaced = _scheduler.Replaced;

        var question = await _service.AddQuestionAsync("Is swimming fine?");
        await _service.SetQuestionAnsweredAsync(question, answered: true);
        var result = await _service.AttachFileAsync(new FileToAttach("Glucose.pdf", "application/pdf", new byte[1024]));
        await _service.RenameResultAsync(result, "Glucose curve");
        await _service.RemoveResultAsync(result);
        await _service.LinkContactsAsync([new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", Label: null, DeviceContactId: null)]);
        await SettledAsync();

        Assert.Equal(replaced, _scheduler.Replaced);
        Assert.Equal("check:ogtt:daybefore", Assert.Single(_scheduler.Set).Key);
    }

    [Fact]
    public async Task Clearing_done_tasks_sets_them_again_as_deleting_a_task_does()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        var call = await _service.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 12), new TimeOnly(9, 0));
        var form = await _service.AddTaskAsync("Send the form", new DateOnly(2026, 10, 13), new TimeOnly(9, 0));
        await _service.SetTaskDoneAsync(form, done: true);
        await SettledAsync();
        var replaced = _scheduler.Replaced;

        Assert.Equal(1, await _service.ClearDoneTasksAsync());
        await SettledAsync();

        Assert.True(_scheduler.Replaced > replaced);
        Assert.NotEmpty(_scheduler.Set);
        Assert.All(_scheduler.Set, r => Assert.StartsWith(ReminderTimes.KeyFor(call), r.Key, StringComparison.Ordinal));
        Assert.Equal(call, Assert.Single(_plans.Stored!.Tasks).Id);
    }

    [Fact]
    public async Task Turning_them_off_cancels_every_one()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();

        await _reminders.SetOnAsync(false);

        Assert.Empty(_scheduler.Set);
    }

    [Fact]
    public async Task Deleting_her_data_cancels_every_reminder()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();

        await _service.DeleteEverythingAsync();
        await SettledAsync();

        Assert.Empty(_scheduler.Set);
    }

    [Fact]
    public async Task Changing_her_reminder_choices_sets_them_again()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();

        await _reminders.SaveSettingsAsync(new ReminderSettings { When = ReminderPreference.Both, DiscreetTitles = true });

        Assert.Equal(new[] { "Appointment", "Appointment" }, _scheduler.Set.Select(r => r.Title));
        Assert.Equal(new ReminderSettings { When = ReminderPreference.Both, DiscreetTitles = true }, await _reminders.GetSettingsAsync());
    }

    [Fact]
    public async Task A_booking_made_abroad_rings_at_its_time_at_home()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        _clock.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();

        Assert.Equal(new DateTimeOffset(2026, 10, 11, 8, 30, 0, TimeSpan.FromHours(3)), Assert.Single(_scheduler.Set).At);
    }

    [Fact]
    public async Task A_failure_is_reported_by_code_and_the_next_change_tries_again()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        _scheduler.FailNext = new InvalidOperationException("alarm service");

        await _service.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        await SettledAsync();

        var report = Assert.Single(_errors.Reports);
        Assert.Equal("reminders.set", report.Operation);
        Assert.Empty(_scheduler.Set);

        await _service.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));
        await SettledAsync();
        Assert.Equal(2, _scheduler.Set.Count);
    }

    private sealed class StubTemplateCatalog : IPlanTemplateCatalog
    {
        public IReadOnlyList<string> SupportedCountries => ["GR"];

        public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken) => Task.FromResult(TestData.Template());
    }

    [Fact]
    public async Task Changes_in_quick_succession_are_set_from_the_latest_plan()
    {
        await CreateAsync();
        await _reminders.SetOnAsync(true);
        var replacedBefore = _scheduler.Replaced;

        var changes = Enumerable.Range(1, 5).Select(day => _service.AddTaskAsync("Stretch", new DateOnly(2026, 10, day), new TimeOnly(18, 0))).ToList();
        await Task.WhenAll(changes);
        await SettledAsync();

        Assert.Equal(5, _scheduler.Set.Count);
        Assert.InRange(_scheduler.Replaced - replacedBefore, 1, 5);
    }
}

/// <summary>What the phone keeps to set reminders again after a restart (review #22 follow-up).</summary>
public sealed class KeptRemindersTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.FromHours(3));

    private static PhoneReminder Reminder(string key, int hoursAhead, string title = "Glucose test") =>
        new(key, Now.AddHours(hoursAhead), title, "Tomorrow at 08:30", "Appointment", "Tomorrow at 08:30", "Maia reminder");

    [Fact]
    public void Reminders_are_numbered_in_turn_never_from_what_they_are_for()
    {
        var nipt = KeptReminders.Number([Reminder("check:nipt:daybefore", 1), Reminder("check:nipt:soon", 2)], next: 7);
        var ogtt = KeptReminders.Number([Reminder("check:ogtt:daybefore", 1), Reminder("check:ogtt:soon", 2)], next: 7);

        Assert.Equal(new[] { 7, 8 }, nipt.Numbered.Select(n => n.Number));
        Assert.Equal(nipt.Numbered.Select(n => n.Number), ogtt.Numbered.Select(n => n.Number));
        Assert.Equal(9, nipt.Next);
        Assert.Equal(new[] { 9 }, KeptReminders.Number([Reminder("check:nipt:daybefore", 1)], nipt.Next).Numbered.Select(n => n.Number));
    }

    [Fact]
    public void Setting_them_again_leaves_unchanged_ones_alone_and_numbers_only_the_new_ones()
    {
        var (set, next) = KeptReminders.Number([Reminder("check:ogtt:daybefore", 1), Reminder("check:nipt:daybefore", 2), Reminder("task:1:soon", 3)], next: 7);

        var change = KeptReminders.Change(
            set,
            [Reminder("check:ogtt:daybefore", 1), Reminder("check:nipt:daybefore", 5), Reminder("task:2:soon", 4)],
            next);

        Assert.Equal(new[] { (7, "check:ogtt:daybefore") }, change.Keep.Select(k => (k.Number, k.Reminder.Key)));
        Assert.Equal(new[] { 8, 9 }, change.Cancel);
        Assert.Equal(new[] { (10, "check:nipt:daybefore"), (11, "task:2:soon") }, change.Add.Select(a => (a.Number, a.Reminder.Key)));
        Assert.Equal(12, change.Next);
        Assert.Equal(new[] { 7, 10, 11 }, change.All.Select(a => a.Number));
    }

    [Fact]
    public void Setting_the_same_reminders_again_changes_nothing()
    {
        var (set, next) = KeptReminders.Number([Reminder("check:ogtt:daybefore", 1), Reminder("check:ogtt:soon", 2)], next: 1);

        var change = KeptReminders.Change(set, [Reminder("check:ogtt:daybefore", 1), Reminder("check:ogtt:soon", 2)], next);

        Assert.Empty(change.Cancel);
        Assert.Empty(change.Add);
        Assert.Equal(set, change.Keep);
        Assert.Equal(next, change.Next);
    }

    [Fact]
    public void Numbering_starts_again_from_one_before_it_runs_out()
    {
        var (numbered, next) = KeptReminders.Number([Reminder("a", 1), Reminder("b", 2)], KeptReminders.LastNumber - 1);

        Assert.Equal(new[] { 1, 2 }, numbered.Select(n => n.Number));
        Assert.Equal(3, next);
        Assert.Equal(1, KeptReminders.Number([Reminder("a", 1)], next: 0).Numbered[0].Number);
    }

    [Fact]
    public void The_kept_schedule_holds_only_numbers_times_and_plain_words()
    {
        var plan = TestData.Plan(new Dictionary<CheckId, bool> { [TestData.Nipt] = true });
        plan.Book(TestData.Nipt, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        plan.AddTask("Call the clinic", new DateOnly(2026, 10, 2), new TimeOnly(9, 0));
        var reminders = ReminderPlanner.For(plan, new ReminderSettings { When = ReminderPreference.Both }, Now, FakeClock.Athens);
        Assert.Contains(reminders, r => r.Title == "NIPT");

        var kept = KeptReminders.Write(KeptReminders.Number(reminders, next: 1).Numbered.Select(n => n.Kept));

        Assert.DoesNotContain("nipt", kept, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clinic", kept, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("check:", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("task:", kept, StringComparison.Ordinal);
        Assert.Equal(
            reminders.Select((r, i) => (i + 1, r.At, r.DiscreetTitle, r.DiscreetText, r.LockScreenTitle)),
            KeptReminders.Read(kept).Select(k => (k.Number, k.At, k.Title, k.Text, k.LockScreenTitle)));
    }

    [Fact]
    public void A_kept_line_that_cannot_be_read_is_left_out()
    {
        var kept = KeptReminders.Write([new KeptReminder(3, Now, "Appointment", "Tomorrow at 08:30", "Maia reminder")]);

        var read = KeptReminders.Read(kept + "\nnot a reminder\n4\t99999999999999999\ta\tb\tc");

        Assert.Equal(3, Assert.Single(read).Number);
        Assert.Empty(KeptReminders.Read(null));
    }
}
