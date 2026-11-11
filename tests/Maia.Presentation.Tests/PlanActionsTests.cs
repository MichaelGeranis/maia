using Maia.Application.Abstractions;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Plan;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Journey: the timeline, and entries that expand in place. Not booked: "Book" and "Delete". Booked or done: "Edit" and
/// "Delete". Today is 24+3 (Wed 30 Sep 2026, 12:00).
/// </summary>
public sealed class PlanActionsTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Shows_checks_by_trimester_with_a_you_are_here_marker()
    {
        var vm = await OpenPlanAsync();

        Assert.Equal(
            new[]
            {
                "section:trimester1", "check:booking",
                "section:trimester2", "check:anomaly", "check:ogtt", "here", "check:checkup-25",
                "section:trimester3", "check:due-date",
            },
            vm.Rows.Where(r => r is not WeekRow).Select(r => r.Key));
        Assert.Equal("You are here", vm.Rows.OfType<HereRow>().Single().Text);
    }

    [Fact]
    public async Task The_timeline_line_runs_from_each_trimesters_first_row_to_its_last()
    {
        var vm = await OpenPlanAsync();
        var second = vm.Rows.SkipWhile(r => r.Key != "section:trimester2").Skip(1).TakeWhile(r => r is not SectionRow).ToList();

        Assert.Equal(("week:14", "week:27"), (second[0].Key, second[^1].Key));
        Assert.False(second[0].LineAbove);
        Assert.False(second[^1].LineBelow);
        Assert.All(second.Skip(1), r => Assert.True(r.LineAbove));
        Assert.All(second.SkipLast(1), r => Assert.True(r.LineBelow));
    }

    [Fact]
    public async Task Each_entry_says_its_trimester_and_its_week_or_usual_weeks()
    {
        var vm = await OpenPlanAsync();

        Assert.Equal("Probably done", Row(vm, TestData.Booking).StatusText);
        Assert.Equal("1st trimester · Weeks 8–10", Row(vm, TestData.Booking).Caption);
        Assert.Equal("Not booked", Row(vm, TestData.Glucose).StatusText);
        Assert.Equal("NotBooked", Row(vm, TestData.Glucose).StatusKind);
        Assert.Equal("2nd trimester · Weeks 24–28", Row(vm, TestData.Glucose).Caption);
        Assert.Equal("2nd trimester · Week 25", Row(vm, TestData.Checkup25).Caption);
        Assert.Equal("Sun 17 Jan", Row(vm, TestData.DueDateMilestone).Caption);
        Assert.Equal("Glucose test details", Row(vm, TestData.Glucose).Note);
    }

    [Fact]
    public async Task An_entry_given_only_its_trimester_says_no_week()
    {
        await _app.CreatePlanAsync();
        var thyroid = await _app.Plans.AddEntryAsync("Thyroid test", EntryKind.Exam, EntryWhen.DoneIn(Trimester.First));
        var vm = await OpenPlanAsync();

        Assert.Equal("1st trimester", Row(vm, thyroid).Caption);
        Assert.Equal("Done", Row(vm, thyroid).StatusText);
        Assert.True(Row(vm, thyroid).IsDone);
    }

    [Fact]
    public async Task Only_one_card_is_open_at_a_time_and_the_due_date_never_opens()
    {
        var vm = await OpenPlanAsync();

        Row(vm, TestData.Glucose).ToggleCommand.Execute(null);
        Row(vm, TestData.Checkup25).ToggleCommand.Execute(null);
        Row(vm, TestData.DueDateMilestone).ToggleCommand.Execute(null);

        Assert.False(Row(vm, TestData.Glucose).IsExpanded);
        Assert.True(Row(vm, TestData.Checkup25).IsExpanded);
        Assert.False(Row(vm, TestData.DueDateMilestone).IsExpanded);

        Row(vm, TestData.Checkup25).ToggleCommand.Execute(null);
        Assert.False(Row(vm, TestData.Checkup25).IsExpanded);
    }

    // ---- Book ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_not_booked_entry_opens_on_book_and_delete()
    {
        var vm = await OpenPlanAsync();

        var glucose = Open(vm, TestData.Glucose);

        Assert.True(glucose.CanBook);
        Assert.Equal(CheckPanel.Actions, glucose.Panel);
        Assert.True(glucose.ShowsActions);
        Assert.False(glucose.IsChanging);
        Assert.False(glucose.IsConfirmingDelete);
    }

    [Fact]
    public async Task Book_asks_only_for_a_day_and_a_time_starting_at_the_next_whole_hour()
    {
        var vm = await OpenPlanAsync();
        var checkup = Open(vm, TestData.Checkup25);

        checkup.StartBookCommand.Execute(null);

        Assert.Equal((CheckPanel.Booking, true, true, false), (checkup.Panel, checkup.IsBooking, checkup.IsChanging, checkup.ShowsActions));
        Assert.Equal("Book", checkup.SaveLabel);
        Assert.Equal(TimingMode.DateTime, checkup.Timing.Mode);
        Assert.False(checkup.Timing.ShowModes);
        Assert.Equal(new DateTime(2026, 9, 30), checkup.Timing.Date);
        Assert.Equal(new TimeSpan(13, 0, 0), checkup.Timing.Time);

        // A day that has passed can be booked too, back to the first day of the pregnancy.
        Assert.Equal(new DateTime(2026, 4, 12), checkup.Timing.MinimumDate);
    }

    [Fact]
    public async Task Book_starts_at_the_hour_after_she_taps_it()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);

        _app.Clock.SetTime(TestData.Today.AddDays(1), new TimeOnly(8, 15));
        await vm.LoadAsync();
        glucose.StartBookCommand.Execute(null);

        Assert.Equal((new DateTime(2026, 10, 1), new TimeSpan(9, 0, 0)), (glucose.Timing.Date, glucose.Timing.Time));
    }

    [Fact]
    public async Task Booking_saves_folds_the_card_and_says_so()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);

        glucose.Timing.Date = new DateTime(2026, 10, 12);
        glucose.Timing.Time = new TimeSpan(8, 30, 0);
        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)), Stored(TestData.Glucose).Appointment);
        Assert.Equal(CheckState.Booked, Stored(TestData.Glucose).State);
        Assert.False(glucose.IsExpanded);
        Assert.Equal(CheckPanel.Actions, glucose.Panel);
        Assert.Equal("Booked", glucose.StatusText);
        Assert.False(glucose.CanBook);
        Assert.Equal("2nd trimester · Week 26 · Mon 12 Oct, 08:30", glucose.Caption);
        Assert.Equal("Glucose test booked for Mon 12 Oct at 08:30.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_booking_on_a_day_that_has_passed_shows_as_done()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);

        glucose.Timing.Date = new DateTime(2026, 9, 28);
        glucose.Timing.Time = new TimeSpan(9, 0, 0);
        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Equal((CheckState.Booked, new Appointment(new DateOnly(2026, 9, 28), new TimeOnly(9, 0))), (Stored(TestData.Glucose).State, Stored(TestData.Glucose).Appointment));
        Assert.Equal(("Done", "Done", true, false), (glucose.StatusText, glucose.StatusKind, glucose.IsDone, glucose.CanBook));
        Assert.Equal("2nd trimester · Week 24 · Mon 28 Sep, 09:00", glucose.Caption);
        Assert.Equal("Glucose test done on Mon 28 Sep.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_booking_turns_done_once_its_time_has_passed_without_her()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 1), new TimeOnly(10, 0)));

        _app.Clock.SetTime(new DateOnly(2026, 10, 1), new TimeOnly(9, 59));
        Assert.Equal("Booked", Row(await OpenPlanAsync(), TestData.Checkup25).StatusText);

        _app.Clock.SetTime(new DateOnly(2026, 10, 1), new TimeOnly(10, 0));
        Assert.Equal("Done", Row(await OpenPlanAsync(), TestData.Checkup25).StatusText);
    }

    [Fact]
    public async Task With_reminders_on_a_booking_s_line_still_ends_at_its_day_and_time()
    {
        RemindersOn(ReminderPreference.DayBefore);
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);

        // The button just says "Book".
        Assert.Equal("Book", glucose.SaveLabel);

        glucose.Timing.Date = new DateTime(2026, 10, 12);
        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Equal("2nd trimester · Week 26 · Mon 12 Oct, 13:00", glucose.Caption);
        Assert.Equal("Glucose test booked for Mon 12 Oct at 13:00.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_booking_that_is_done_names_no_reminder()
    {
        RemindersOn(ReminderPreference.Both);
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Anomaly, new Appointment(new DateOnly(2026, 9, 28), new TimeOnly(9, 0)));

        var vm = await OpenPlanAsync();

        Assert.Equal("2nd trimester · Week 24 · Mon 28 Sep, 09:00", Row(vm, TestData.Anomaly).Caption);
    }

    [Fact]
    public async Task With_reminders_off_a_booking_names_no_reminder()
    {
        _app.ReminderSettings.Settings = new ReminderSettings { When = ReminderPreference.Both };
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));

        var vm = await OpenPlanAsync();

        Assert.Equal("2nd trimester · Week 26 · Mon 12 Oct, 08:30", Row(vm, TestData.Glucose).Caption);
    }

    [Fact]
    public async Task A_booking_made_less_than_a_day_ahead_with_24_hours_chosen_names_no_reminder()
    {
        // 02:15 on Thu 1 Oct: she books the check-up for 10:00 that morning. 24 hours before has gone by, and nothing
        // rings in its place, so the line promises none.
        RemindersOn(ReminderPreference.DayBefore);
        await _app.CreatePlanAsync();
        _app.Clock.SetTime(new DateOnly(2026, 10, 1), new TimeOnly(2, 15));
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 1), new TimeOnly(10, 0)));

        var vm = await OpenPlanAsync();

        Assert.Equal("2nd trimester · Week 24 · Thu 1 Oct, 10:00", Row(vm, TestData.Checkup25).Caption);
    }

    [Theory]
    [InlineData(ReminderPreference.DayBefore, 9)]
    [InlineData(ReminderPreference.DayBefore, 11)]
    [InlineData(ReminderPreference.Both, 9)]
    [InlineData(ReminderPreference.TwoHoursBefore, 11)]
    public async Task A_bookings_line_names_no_reminder_whichever_she_chose(ReminderPreference reminders, int hour)
    {
        RemindersOn(reminders);
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 1), new TimeOnly(10, 0)));
        _app.Clock.SetTime(TestData.Today, new TimeOnly(hour, 0));

        var vm = await OpenPlanAsync();

        Assert.Equal("2nd trimester · Week 24 · Thu 1 Oct, 10:00", Row(vm, TestData.Checkup25).Caption);
    }

    [Fact]
    public async Task A_day_booked_without_a_time_reads_just_its_day()
    {
        RemindersOn(ReminderPreference.TwoHoursBefore);
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6)));

        var vm = await OpenPlanAsync();

        Assert.Equal("2nd trimester · Week 25 · Tue 6 Oct", Row(vm, TestData.Checkup25).Caption);
    }

    [Fact]
    public async Task In_greek_a_bookings_line_names_no_reminder_either()
    {
        RemindersOn(ReminderPreference.Both);
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 1), new TimeOnly(10, 0)));
        _app.Clock.SetTime(TestData.Today, new TimeOnly(21, 0));
        using var greek = new CultureScope("el-GR");

        var vm = await OpenPlanAsync();

        Assert.Equal("2ο τρίμηνο · Εβδομάδα 24 · Πέμ 1 Οκτ, 10:00", Row(vm, TestData.Checkup25).Caption);
    }

    private void RemindersOn(ReminderPreference when)
    {
        _app.ReminderSettings.IsOn = true;
        _app.ReminderSettings.Settings = new ReminderSettings { When = when };
    }

    // ---- Edit ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_booked_entry_opens_on_edit_and_delete()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = await OpenPlanAsync();

        var checkup = Open(vm, TestData.Checkup25);

        Assert.False(checkup.CanBook);
        Assert.True(checkup.ShowsActions);
    }

    [Fact]
    public async Task Editing_a_booking_starts_from_it_keeps_its_place_and_asks_only_a_day_and_a_time()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30), "Clinic"));
        var vm = await OpenPlanAsync();
        var checkup = Open(vm, TestData.Checkup25);

        checkup.StartEditCommand.Execute(null);

        Assert.Equal((CheckPanel.Editing, true, true), (checkup.Panel, checkup.IsEditing, checkup.IsChanging));
        Assert.Equal("Save", checkup.SaveLabel);
        Assert.Equal("Check-up", checkup.EditName);
        Assert.Equal(TimingMode.DateTime, checkup.Timing.Mode);
        Assert.False(checkup.Timing.ShowModes);
        Assert.Equal((new DateTime(2026, 10, 6), new TimeSpan(10, 30, 0)), (checkup.Timing.Date, checkup.Timing.Time));

        checkup.Timing.Time = new TimeSpan(11, 0, 0);
        await checkup.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(11, 0), "Clinic"), Stored(TestData.Checkup25).Appointment);
        Assert.False(checkup.IsExpanded);
        Assert.Equal("Check-up booked for Tue 6 Oct at 11:00.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Edit_renames_an_entry_in_her_own_words_and_says_it_is_updated()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = await OpenPlanAsync();
        var checkup = Open(vm, TestData.Checkup25);
        checkup.StartEditCommand.Execute(null);

        checkup.EditName = "  Check-up with Dr Papadaki ";
        await checkup.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Check-up with Dr Papadaki", Stored(TestData.Checkup25).Title.In(LocalizedText.English));
        Assert.Equal("Check-up with Dr Papadaki", Row(vm, TestData.Checkup25).Title);

        // Only the name changed: it says so, not that it was booked again.
        Assert.Equal("Check-up with Dr Papadaki updated.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task An_edit_without_a_name_cannot_be_saved()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = await OpenPlanAsync();
        var checkup = Open(vm, TestData.Checkup25);
        checkup.StartEditCommand.Execute(null);
        var revision = _app.Repository.Stored!.Revision;

        checkup.EditName = "   ";

        Assert.False(checkup.SaveCommand.CanExecute(null));
        Assert.Equal(revision, _app.Repository.Stored!.Revision);
        checkup.EditName = "Check-up";
        Assert.True(checkup.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_goes_back_to_the_buttons_with_nothing_saved()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = await OpenPlanAsync();
        var checkup = Open(vm, TestData.Checkup25);
        checkup.StartEditCommand.Execute(null);
        checkup.EditName = "Something else";
        checkup.Timing.Date = new DateTime(2026, 10, 20);
        var revision = _app.Repository.Stored!.Revision;

        checkup.CancelEditCommand.Execute(null);

        Assert.Equal((CheckPanel.Actions, true), (checkup.Panel, checkup.IsExpanded));
        Assert.Equal(revision, _app.Repository.Stored!.Revision);
        Assert.Equal("Check-up", Stored(TestData.Checkup25).Title.In(LocalizedText.English));
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task A_done_entry_can_be_edited_to_only_its_trimester_or_its_week()
    {
        await _app.CreatePlanAsync();
        var thyroid = await _app.Plans.AddEntryAsync("Thyroid test", EntryKind.Exam, EntryWhen.DoneInWeek(20));
        var vm = await OpenPlanAsync();
        var row = Open(vm, thyroid);
        Assert.False(row.CanBook);

        row.StartEditCommand.Execute(null);

        Assert.True(row.Timing.ShowModes);
        Assert.Equal((TimingMode.Week, 20), (row.Timing.Mode, row.Timing.Week));

        row.Timing.ChooseModeCommand.Execute("Trimester");
        Assert.Equal(Trimester.Second, row.Timing.Trimester);
        row.Timing.ChooseTrimesterCommand.Execute("First");
        await row.SaveCommand.ExecuteAsync(null);

        var stored = Stored(thyroid);
        Assert.Equal((CheckState.Done, (Trimester?)Trimester.First, (Appointment?)null), (stored.State, stored.TrimesterOnly, stored.Appointment));
        Assert.Equal("1st trimester", Row(vm, thyroid).Caption);
        Assert.Equal("Done", Row(vm, thyroid).StatusText);
        Assert.Equal("Thyroid test updated.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_done_entry_moved_to_another_week_stays_done()
    {
        await _app.CreatePlanAsync();
        var thyroid = await _app.Plans.AddEntryAsync("Thyroid test", EntryKind.Exam, EntryWhen.DoneIn(Trimester.First));
        var vm = await OpenPlanAsync();
        var row = Open(vm, thyroid);
        row.StartEditCommand.Execute(null);
        Assert.Equal((TimingMode.Trimester, (Trimester?)Trimester.First), (row.Timing.Mode, row.Timing.Trimester));

        // The week starts at the first of the trimester it stood in.
        row.Timing.ChooseModeCommand.Execute("Week");
        Assert.Equal(1, row.Timing.Week);
        row.Timing.LaterWeekCommand.Execute(null);
        row.Timing.LaterWeekCommand.Execute(null);
        await row.SaveCommand.ExecuteAsync(null);

        var stored = Stored(thyroid);
        Assert.Equal((CheckState.Done, (Trimester?)null), (stored.State, stored.TrimesterOnly));
        Assert.Equal("1st trimester · Week 3", Row(vm, thyroid).Caption);
    }

    [Fact]
    public async Task A_check_whose_weeks_have_passed_reads_probably_done_and_is_edited_not_booked()
    {
        var vm = await OpenPlanAsync();

        var booking = Open(vm, TestData.Booking);

        Assert.Equal("Probably done", booking.StatusText);
        Assert.True(booking.IsDone);
        Assert.False(booking.CanBook);

        booking.StartEditCommand.Execute(null);
        Assert.True(booking.Timing.ShowModes);
        Assert.Equal((TimingMode.Week, 8), (booking.Timing.Mode, booking.Timing.Week));
    }

    [Fact]
    public async Task An_entry_she_moves_goes_to_the_week_of_its_new_day()
    {
        await _app.CreatePlanAsync();
        var visit = await _app.Plans.AddEntryAsync("Visit with my doctor", EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 20), new TimeOnly(11, 0))));
        var vm = await OpenPlanAsync();
        Assert.Equal(
            new[] { "section:trimester2", "check:anomaly", "check:ogtt", "here", "check:checkup-25", "check:" + visit, "section:trimester3", "check:due-date" },
            vm.Rows.Where(r => r is not WeekRow).Select(r => r.Key).Skip(2));

        var card = Open(vm, visit);
        card.StartEditCommand.Execute(null);
        card.Timing.Date = new DateTime(2027, 1, 5);
        await card.SaveCommand.ExecuteAsync(null);

        Assert.Equal(
            new[] { "section:trimester2", "check:anomaly", "check:ogtt", "here", "check:checkup-25", "section:trimester3", "check:" + visit, "check:due-date" },
            vm.Rows.Where(r => r is not WeekRow).Select(r => r.Key).Skip(2));
        Assert.Equal("3rd trimester · Week 38 · Tue 5 Jan, 11:00", Row(vm, visit).Caption);
    }

    [Fact]
    public async Task An_entry_she_added_never_reads_probably_done()
    {
        await _app.CreatePlanAsync();
        var iron = await _app.Plans.AddEntryAsync("Iron recheck", EntryKind.Exam, EntryWhen.NotBookedInWeek(24));
        _app.Clock.SetToday(TestData.Today.AddDays(28));
        var vm = await OpenPlanAsync();

        var row = Open(vm, iron);

        Assert.Equal("Not booked", row.StatusText);
        Assert.Equal("2nd trimester · Week 24", row.Caption);
        Assert.Equal("Exam or test", row.KindText);
        Assert.True(row.CanBook);
    }

    // ---- Delete -------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_asks_once_more_and_keep_it_changes_nothing()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);

        glucose.AskDeleteCommand.Execute(null);

        Assert.Equal((CheckPanel.ConfirmDelete, true, false), (glucose.Panel, glucose.IsConfirmingDelete, glucose.ShowsActions));
        Assert.Equal("Delete Glucose test from your journey?", glucose.DeleteQuestion);

        glucose.KeepItCommand.Execute(null);

        Assert.Equal(CheckPanel.Actions, glucose.Panel);
        Assert.Contains(_app.Repository.Stored!.Checks, c => c.Id == TestData.Glucose);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Delete_takes_the_entry_off_her_journey_for_good()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.AskDeleteCommand.Execute(null);

        await glucose.DeleteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(_app.Repository.Stored!.Checks, c => c.Id == TestData.Glucose);
        Assert.DoesNotContain(vm.Rows, r => r.Key == "check:ogtt");
        Assert.Equal("Glucose test deleted from your journey.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_booked_or_done_entry_can_be_deleted_too()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Anomaly, new Appointment(new DateOnly(2026, 9, 28), new TimeOnly(9, 0)));
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = await OpenPlanAsync();

        var anomaly = Open(vm, TestData.Anomaly);
        anomaly.AskDeleteCommand.Execute(null);
        await anomaly.DeleteCommand.ExecuteAsync(null);
        var checkup = Open(vm, TestData.Checkup25);
        checkup.AskDeleteCommand.Execute(null);
        await checkup.DeleteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(_app.Repository.Stored!.Checks, c => c.Id == TestData.Anomaly || c.Id == TestData.Checkup25);
        Assert.Equal(new[] { "Anomaly scan deleted from your journey.", "Check-up deleted from your journey." }, _app.Notifier.Shown);
    }

    [Fact]
    public async Task A_card_on_its_own_reloads_its_screen_once_after_a_change()
    {
        await _app.CreatePlanAsync();
        var reloads = 0;
        var host = new CheckRowHost(_app.Plans, _app.Clock, _app.Errors, _app.Notifier, () => { reloads++; return Task.CompletedTask; }, _ => { });
        var glucose = Stored(TestData.Glucose);
        var row = new CheckRow(host, TestData.Glucose);
        row.Show(new CheckRowData(new Maia.Application.Plans.CheckView(glucose, CheckStatus.NotBooked, "Glucose test"), "Glucose test", _app.Repository.Stored!.Dating, TestData.Today));
        Assert.True(row.CanBook);

        row.AskDeleteCommand.Execute(null);
        await row.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, reloads);
        Assert.Equal("Glucose test deleted from your journey.", _app.Notifier.Shown.Single());
    }

    // ---- Reloads and failures -----------------------------------------------------------------

    [Fact]
    public async Task An_open_card_takes_up_a_booking_made_elsewhere_but_keeps_her_own_draft()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        var checkup = Row(vm, TestData.Checkup25);
        glucose.StartBookCommand.Execute(null);
        glucose.Timing.Time = new TimeSpan(10, 0, 0);

        // After your visit books it while the card is open underneath.
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        await vm.LoadAsync();

        Assert.True(glucose.IsExpanded);
        Assert.Equal("Booked", glucose.StatusText);
        Assert.True(glucose.IsBooking);
        Assert.Equal(new TimeSpan(10, 0, 0), glucose.Timing.Time);
        Assert.False(checkup.IsExpanded);
    }

    [Fact]
    public async Task A_change_updates_rows_in_place_so_the_list_keeps_its_place()
    {
        var vm = await OpenPlanAsync();
        var rows = vm.Rows.ToList();
        var changes = 0;
        vm.Rows.CollectionChanged += (_, _) => changes++;

        var glucose = Open(vm, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);
        glucose.Timing.Date = new DateTime(2026, 9, 28);
        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, changes);
        Assert.Equal(rows, vm.Rows);
    }

    [Fact]
    public async Task A_failed_save_keeps_the_card_open_and_reports_only_the_operation()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);
        _app.Repository.FailNextSave = new IOException("Disk full.");

        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.True(glucose.IsExpanded);
        Assert.True(glucose.IsBooking);
        Assert.Equal("Something went wrong. Please try again.", glucose.ErrorMessage);
        Assert.Equal("plan.book", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Empty(_app.Notifier.Shown);
        Assert.Equal(CheckState.NotBooked, Stored(TestData.Glucose).State);
    }

    [Fact]
    public async Task A_failed_delete_keeps_the_entry_and_reports_only_the_operation()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.AskDeleteCommand.Execute(null);
        _app.Repository.FailNextSave = new IOException("Disk full.");

        await glucose.DeleteCommand.ExecuteAsync(null);

        Assert.True(glucose.IsExpanded);
        Assert.Equal("Something went wrong. Please try again.", glucose.ErrorMessage);
        Assert.Equal("plan.delete", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Contains(_app.Repository.Stored!.Checks, c => c.Id == TestData.Glucose);
    }

    [Fact]
    public async Task Trying_again_after_a_failed_save_books_it_once()
    {
        var vm = await OpenPlanAsync();
        var glucose = Open(vm, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);
        glucose.Timing.Date = new DateTime(2026, 10, 12);
        _app.Repository.FailNextSave = new IOException("Disk full.");
        await glucose.SaveCommand.ExecuteAsync(null);

        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Null(glucose.ErrorMessage);
        Assert.Equal(new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(13, 0)), Stored(TestData.Glucose).Appointment);
        Assert.Equal("Glucose test booked for Mon 12 Oct at 13:00.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Opens_on_the_check_chosen_on_another_screen_and_leaves_it_closed()
    {
        await _app.CreatePlanAsync();
        _app.Focus.Request(TestData.Glucose);
        var vm = NewPlan();
        PlanRow? scrolledTo = null;
        vm.ScrollRequested += (_, row) => scrolledTo = row;

        await vm.LoadAsync();

        Assert.False(Row(vm, TestData.Glucose).IsExpanded);
        Assert.Same(Row(vm, TestData.Glucose), scrolledTo);
        await vm.LoadAsync();
        Assert.False(Row(vm, TestData.Glucose).IsExpanded);
    }

    [Fact]
    public async Task An_entry_asked_for_shows_on_journey_scrolled_to_and_closed()
    {
        await _app.CreatePlanAsync();
        _app.Focus.Request(await _app.Plans.AddEntryAsync("Antenatal class", EntryKind.Other, EntryWhen.NotBookedInWeek(24)));
        var vm = NewPlan();
        PlanRow? scrolledTo = null;
        vm.ScrollRequested += (_, row) => scrolledTo = row;

        await vm.LoadAsync();

        var added = vm.Rows.OfType<CheckRow>().Single(r => r.Title == "Antenatal class");
        Assert.Same(added, scrolledTo);
        Assert.False(added.IsExpanded);
        Assert.DoesNotContain(vm.Rows.OfType<CheckRow>(), r => r.IsExpanded);
    }

    [Fact]
    public void Journeys_header_has_no_add_and_no_calendar_button()
    {
        var page = File.ReadAllText(Repo.App("Views", "PlanPage.xaml"));

        Assert.DoesNotContain("{Binding AddCommand}", page, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCalendarCommand", page, StringComparison.Ordinal);
        Assert.DoesNotContain("IconCalendar}", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_plan_it_goes_to_setup()
    {
        await NewPlan().LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Reads_in_greek_on_greek_phones()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        using var greek = new CultureScope("el-GR");
        var vm = await OpenPlanAsync();

        var checkup = Open(vm, TestData.Checkup25);
        checkup.StartEditCommand.Execute(null);

        Assert.Equal("Αποθήκευση", checkup.SaveLabel);
        Assert.Equal("Κλεισμένο", checkup.StatusText);
        Assert.Equal("Δεν έχει κλειστεί", Row(vm, TestData.Glucose).StatusText);
        Assert.Equal("Είσαι εδώ", vm.Rows.OfType<HereRow>().Single().Text);
        Row(vm, TestData.Glucose).StartBookCommand.Execute(null);
        Assert.Equal("Κλείσε", Row(vm, TestData.Glucose).SaveLabel);
    }

    private PlanViewModel NewPlan() =>
        new(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);

    private async Task<PlanViewModel> OpenPlanAsync()
    {
        if (_app.Repository.Stored is null)
        {
            await _app.CreatePlanAsync();
        }

        var vm = NewPlan();
        await vm.LoadAsync();
        return vm;
    }

    private static CheckRow Row(PlanViewModel vm, CheckId id) => vm.Rows.OfType<CheckRow>().Single(r => r.Id == id);

    private static CheckRow Open(PlanViewModel vm, CheckId id)
    {
        var row = Row(vm, id);
        if (!row.IsExpanded)
        {
            row.ToggleCommand.Execute(null);
        }

        return row;
    }

    private PlannedCheck Stored(CheckId id) => _app.Repository.Stored!.GetCheck(id);
}
