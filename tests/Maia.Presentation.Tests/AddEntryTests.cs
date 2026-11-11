using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Add;
using Maia.Presentation.Plan;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>"Add to your journey" (change request #5), at 24+3 on 30 Sep with the test template.</summary>
public sealed class AddEntryTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Opens_on_what_it_is_with_nothing_chosen_yet()
    {
        var vm = await OpenAsync();

        Assert.Equal((false, (EntryKind?)null, (AddWhen?)null, string.Empty), (vm.HasKind, vm.Kind, vm.When, vm.Name));
        Assert.False(vm.ShowSuggestions);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Add_opens_on_its_title_with_no_week_above_it()
    {
        // The week and the due date show only on Your week.
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "AddEntryPage.xaml"));
        var screen = page.Descendants().First(e => e.Name.LocalName == "VerticalStackLayout" && AccessibilityTests.Attr(e, "Motions.Rise") == "True");

        Assert.Equal("{loc:Translate Add_Title}", AccessibilityTests.Attr(screen.Elements().First(), "Text"));
        Assert.DoesNotContain(page.Descendants(), e => AccessibilityTests.Attr(e, "Text") == "{Binding Eyebrow}");
        Assert.Null(typeof(AddEntryViewModel).GetProperty("Eyebrow"));
    }

    // ---- What it is, and its name ----------------------------------------------------------

    [Fact]
    public async Task A_doctor_visit_starts_without_a_name_even_with_her_doctor_linked()
    {
        await LinkDoctorAsync();
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("DoctorVisit");
        vm.ChooseWhenCommand.Execute("NotBookedYet");

        Assert.Equal((true, true, string.Empty, false), (vm.HasKind, vm.IsDoctorVisit, vm.Name, vm.IsComplete));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Give it a name.", vm.ErrorMessage);
        Assert.Empty(_app.Repository.Stored!.DoctorVisits());
    }

    [Fact]
    public async Task A_visit_she_names_as_the_visit_with_her_doctor_is_booked_with_them()
    {
        var doctor = await LinkDoctorAsync();
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("DoctorVisit");

        vm.Name = "Visit with Dr. Eleni Papadaki";
        vm.ChooseWhenCommand.Execute("Booked");
        vm.Timing.Date = new DateTime(2026, 10, 7);
        vm.Timing.Time = new TimeSpan(10, 0, 0);
        await vm.SaveCommand.ExecuteAsync(null);

        var visit = Assert.Single(_app.Repository.Stored!.DoctorVisits());
        Assert.Equal(new Appointment(new DateOnly(2026, 10, 7), new TimeOnly(10, 0), with: doctor), visit.Appointment);
        Assert.Equal("Επίσκεψη: Dr. Eleni Papadaki", visit.Title.In(LocalizedText.Greek));
        Assert.Equal(
            "Visit with Dr. Eleni Papadaki added to your journey for Wed 7 Oct at 10:00.",
            _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_visit_she_names_herself_is_not_put_down_as_with_her_ob_gyn()
    {
        await LinkDoctorAsync();
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("DoctorVisit");

        vm.Name = "  Dentist check-up ";
        vm.ChooseWhenCommand.Execute("Booked");
        await vm.SaveCommand.ExecuteAsync(null);

        var visit = Assert.Single(_app.Repository.Stored!.DoctorVisits());
        Assert.Equal("Dentist check-up", visit.Title.In(LocalizedText.Greek));
        Assert.Null(visit.Appointment!.With);
    }

    [Fact]
    public async Task An_exam_offers_the_usual_extras()
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("Exam");

        Assert.Equal((true, string.Empty), (vm.ShowSuggestions, vm.Name));
        Assert.Equal(
            new[] { "+ Extra ultrasound", "+ Iron recheck", "+ Urine culture", "+ Doppler scan", "+ Thyroid test", "+ Blood pressure checks" },
            vm.Suggestions.Select(s => s.Label));
        Assert.Equal(AddEntryViewModel.ExtraKeys, vm.Suggestions.Select(s => s.ExtraKey));
    }

    [Fact]
    public async Task Suggestions_show_only_for_an_exam()
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("HospitalVisit");
        Assert.False(vm.ShowSuggestions);

        vm.ChooseKindCommand.Execute("Exam");
        Assert.True(vm.ShowSuggestions);
    }

    [Fact]
    public async Task Tapping_a_suggestion_names_the_exam_and_another_name_lets_go_of_it()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        var iron = vm.Suggestions.Single(s => s.Title == "Iron recheck");

        iron.ChooseCommand.Execute(null);
        Assert.Equal(("Iron recheck", true, "✓ Iron recheck"), (vm.Name, iron.IsChosen, iron.Label));

        vm.Name = "Iron recheck and ferritin";
        Assert.False(iron.IsChosen);

        vm.Name = "iron recheck";
        Assert.True(iron.IsChosen);
    }

    [Fact]
    public async Task A_usual_extra_she_books_is_added_as_a_new_entry_in_one_save()
    {
        var vm = await OpenAsync();
        var saves = _app.Repository.SaveCount;
        vm.ChooseKindCommand.Execute("Exam");
        vm.Suggestions.Single(s => s.Title == "Doppler scan").ChooseCommand.Execute(null);
        vm.ChooseWhenCommand.Execute("Booked");
        vm.Timing.Date = new DateTime(2026, 11, 12);
        vm.Timing.Time = new TimeSpan(9, 30, 0);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(saves + 1, _app.Repository.SaveCount);
        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal((EntryKind.Exam, CheckState.Booked, new Appointment(new DateOnly(2026, 11, 12), new TimeOnly(9, 30))), (added.Kind!.Value, added.State, added.Appointment));
        Assert.Equal(("Doppler scan", "Υπερηχογράφημα Doppler"), (added.Title.In(LocalizedText.English), added.Title.In(LocalizedText.Greek)));
        Assert.Equal("Doppler scan added to your journey for Thu 12 Nov at 09:30.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Typing_the_name_of_a_usual_extra_keeps_its_name_in_both_languages()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");

        vm.Name = "THYROID TEST ";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");
        await vm.SaveCommand.ExecuteAsync(null);

        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal(("Thyroid test", "Έλεγχος θυρεοειδούς"), (added.Title.In(LocalizedText.English), added.Title.In(LocalizedText.Greek)));
        Assert.Equal("Thyroid test added to your journey. It's waiting for a date.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_usual_extra_already_done_on_a_day_keeps_its_name_in_both_languages()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Suggestions.Single(s => s.Title == "Iron recheck").ChooseCommand.Execute(null);
        vm.ChooseWhenCommand.Execute("Done");
        vm.Timing.ChooseModeCommand.Execute("DateTime");
        vm.Timing.Date = new DateTime(2026, 9, 25);
        vm.Timing.Time = new TimeSpan(10, 0, 0);

        await vm.SaveCommand.ExecuteAsync(null);

        // Done on a day and time: kept as a booking whose time has passed, so it shows as done.
        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal(EntryKind.Exam, added.Kind!.Value);
        Assert.Equal(new Appointment(new DateOnly(2026, 9, 25), new TimeOnly(10, 0)), added.Appointment);
        Assert.Equal(CheckStatus.Done, _app.Repository.Stored!.StatusOf(added, TestData.Today, new TimeOnly(12, 0)));
        Assert.Equal(("Iron recheck", "Επανέλεγχος σιδήρου"), (added.Title.In(LocalizedText.English), added.Title.In(LocalizedText.Greek)));
        Assert.Equal("Iron recheck added to your journey, done on Fri 25 Sep.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task An_extra_her_plan_has_in_greek_is_not_offered_again_in_english()
    {
        await _app.CreatePlanAsync();
        using (new CultureScope("el-GR"))
        {
            var greek = await OpenAsync();
            greek.ChooseKindCommand.Execute("Exam");
            greek.Suggestions.Single(s => s.Title == "Επανέλεγχος σιδήρου").ChooseCommand.Execute(null);
            greek.ChooseWhenCommand.Execute("NotBookedYet");
            greek.Timing.ChooseModeCommand.Execute("Week");
            await greek.SaveCommand.ExecuteAsync(null);
            Assert.Equal("Επανέλεγχος σιδήρου: προστέθηκε στο πλάνο σου και περιμένει ημερομηνία.", _app.Notifier.Shown.Single());
        }

        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");

        Assert.DoesNotContain(vm.Suggestions, s => s.Title == "Iron recheck");
        Assert.Contains(vm.Suggestions, s => s.Title == "Thyroid test");
    }

    [Fact]
    public async Task A_hospital_visit_starts_without_a_name_and_maias_name_for_it_follows_her_language()
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("HospitalVisit");
        Assert.Equal(string.Empty, vm.Name);
        vm.Name = "Hospital visit";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");
        await vm.SaveCommand.ExecuteAsync(null);

        var visit = Assert.Single(_app.Repository.Stored!.Checks, c => c.Kind == EntryKind.HospitalVisit);
        Assert.Equal("Επίσκεψη σε νοσοκομείο", visit.Title.In(LocalizedText.Greek));

        vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("HospitalVisit");
        vm.Name = "Hospital tour";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains(_app.Repository.Stored!.Checks, c => c.Kind == EntryKind.HospitalVisit && c.Title.In(LocalizedText.Greek) == "Hospital tour");
    }

    [Fact]
    public async Task Something_else_is_in_her_own_words()
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("Other");
        Assert.Equal((true, string.Empty), (vm.IsOther, vm.Name));

        vm.Name = "Antenatal class";
        vm.ChooseWhenCommand.Execute("Booked");
        await vm.SaveCommand.ExecuteAsync(null);

        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal((EntryKind.Other, CheckState.Booked, "Antenatal class"), (added.Kind!.Value, added.State, added.Title.In(LocalizedText.English)));
    }

    [Fact]
    public async Task Another_kind_clears_the_name_a_chip_filled_in_but_never_hers()
    {
        await LinkDoctorAsync();
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("DoctorVisit");
        Assert.Equal(string.Empty, vm.Name);
        vm.ChooseKindCommand.Execute("Exam");
        Assert.Equal(string.Empty, vm.Name);

        vm.Suggestions.Single(s => s.Title == "Doppler scan").ChooseCommand.Execute(null);
        Assert.Equal("Doppler scan", vm.Name);
        vm.ChooseKindCommand.Execute("HospitalVisit");
        Assert.Equal(string.Empty, vm.Name);

        vm.Name = "Clinic tour";
        vm.ChooseKindCommand.Execute("Other");
        Assert.Equal("Clinic tour", vm.Name);
        vm.ChooseKindCommand.Execute("DoctorVisit");
        Assert.Equal("Clinic tour", vm.Name);
    }

    [Theory]
    [InlineData("Surgery")]
    [InlineData("7")]
    [InlineData("")]
    public async Task Something_that_is_not_a_kind_or_a_time_chooses_nothing(string choice)
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute(choice);
        vm.ChooseWhenCommand.Execute(choice);

        Assert.Equal(((EntryKind?)null, (AddWhen?)null), (vm.Kind, vm.When));
    }

    // ---- When -------------------------------------------------------------------------------

    [Fact]
    public async Task Booked_asks_only_for_a_day_and_a_time_starting_at_the_next_whole_hour()
    {
        await _app.CreatePlanAsync();
        _app.Clock.SetTime(TestData.Today, new TimeOnly(14, 37, 25));
        var vm = await OpenAsync();

        vm.ChooseWhenCommand.Execute("Booked");

        Assert.Equal((TimingMode?)TimingMode.DateTime, vm.Timing.Mode);
        Assert.False(vm.Timing.ShowModes);
        Assert.Equal((new DateTime(2026, 9, 30), new TimeSpan(15, 0, 0)), (vm.Timing.Date, vm.Timing.Time));

        // Booked from the first day of the pregnancy (past today too); a task as far back as a pregnancy goes.
        Assert.Equal(new DateTime(2026, 4, 12), vm.Timing.MinimumDate);
        Assert.Equal(new DateTime(2025, 11, 26), vm.EarliestTaskDate);

        // A trimester or a week is not a booking.
        vm.Timing.ChooseModeCommand.Execute("Week");
        Assert.Equal((TimingMode?)TimingMode.DateTime, vm.Timing.Mode);

        // The screen stayed open a while: "Booked" starts at the hour after she chooses it.
        _app.Clock.SetTime(TestData.Today, new TimeOnly(15, 5));
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.ChooseWhenCommand.Execute("Booked");
        Assert.Equal((new DateTime(2026, 9, 30), new TimeSpan(16, 0, 0)), (vm.Timing.Date, vm.Timing.Time));
    }

    [Fact]
    public async Task Already_done_offers_a_day_and_time_a_trimester_or_a_week()
    {
        var vm = await OpenAsync();

        vm.ChooseWhenCommand.Execute("Done");

        Assert.Equal((true, true, true), (vm.Timing.OffersDateTime, vm.Timing.OffersPeriod, vm.Timing.ShowModes));
        Assert.Null(vm.Timing.Mode);

        vm.Timing.ChooseModeCommand.Execute("Trimester");
        Assert.Equal((true, (Domain.Pregnancy.Trimester?)Domain.Pregnancy.Trimester.Second), (vm.Timing.IsTrimester, vm.Timing.Trimester));
        vm.Timing.ChooseModeCommand.Execute("Week");
        Assert.Equal((true, 24, "Week 24"), (vm.Timing.IsWeek, vm.Timing.Week, vm.Timing.WeekText));
        vm.Timing.ChooseModeCommand.Execute("DateTime");
        Assert.True(vm.Timing.IsDateTime);
    }

    [Fact]
    public async Task Not_booked_yet_offers_only_a_trimester_or_a_week()
    {
        var vm = await OpenAsync();

        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("DateTime");

        Assert.Equal((false, true), (vm.Timing.OffersDateTime, vm.Timing.OffersPeriod));
        Assert.Null(vm.Timing.Mode);
    }

    [Fact]
    public async Task Choosing_another_when_starts_afresh_but_choosing_the_same_one_keeps_it()
    {
        var vm = await OpenAsync();
        vm.ChooseWhenCommand.Execute("Booked");
        vm.Timing.Date = new DateTime(2026, 11, 12);
        vm.Timing.Time = new TimeSpan(9, 30, 0);

        vm.ChooseWhenCommand.Execute("Booked");
        Assert.Equal((new DateTime(2026, 11, 12), new TimeSpan(9, 30, 0)), (vm.Timing.Date, vm.Timing.Time));

        vm.ChooseWhenCommand.Execute("Done");
        Assert.Null(vm.Timing.Mode);
        vm.Timing.ChooseModeCommand.Execute("Week");
        vm.ChooseWhenCommand.Execute("Booked");

        Assert.Equal((TimingMode?)TimingMode.DateTime, vm.Timing.Mode);
        Assert.Equal((new DateTime(2026, 9, 30), new TimeSpan(13, 0, 0)), (vm.Timing.Date, vm.Timing.Time));
    }

    [Fact]
    public async Task The_week_goes_no_earlier_than_week_1_and_no_later_than_week_42()
    {
        var vm = await OpenAsync();
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");

        while (vm.Timing.EarlierWeekCommand.CanExecute(null))
        {
            vm.Timing.EarlierWeekCommand.Execute(null);
        }

        Assert.Equal(TimingDraft.FirstWeek, vm.Timing.Week);

        while (vm.Timing.LaterWeekCommand.CanExecute(null))
        {
            vm.Timing.LaterWeekCommand.Execute(null);
        }

        Assert.Equal(TimingDraft.LastWeek, vm.Timing.Week);
    }

    [Fact]
    public async Task Save_looks_ready_only_once_what_name_and_when_are_all_there()
    {
        var vm = await OpenAsync();
        Assert.False(vm.IsComplete);

        vm.ChooseKindCommand.Execute("Other");
        vm.Name = "Antenatal class";
        Assert.False(vm.IsComplete);

        vm.ChooseWhenCommand.Execute("NotBookedYet");
        Assert.False(vm.IsComplete);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.Timing.ChooseModeCommand.Execute("Trimester");
        Assert.True(vm.IsComplete);
        Assert.Contains(nameof(AddEntryViewModel.IsComplete), changed);

        vm.Name = "  ";
        Assert.False(vm.IsComplete);
    }

    [Fact]
    public async Task Each_choice_of_when_shows_only_its_own_fields()
    {
        var vm = await OpenAsync();

        vm.ChooseWhenCommand.Execute("Booked");
        Assert.Equal((true, false, false), (vm.IsBooked, vm.IsDone, vm.IsNotBookedYet));
        vm.ChooseWhenCommand.Execute("Done");
        Assert.Equal((false, true, false), (vm.IsBooked, vm.IsDone, vm.IsNotBookedYet));
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        Assert.Equal((false, false, true), (vm.IsBooked, vm.IsDone, vm.IsNotBookedYet));
    }

    [Fact]
    public async Task Not_booked_yet_adds_an_entry_waiting_in_its_trimester()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Name = "Blood count";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Trimester");
        vm.Timing.ChooseTrimesterCommand.Execute("Third");

        await vm.SaveCommand.ExecuteAsync(null);

        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal((EntryKind.Exam, CheckState.NotBooked, (Domain.Pregnancy.Trimester?)Domain.Pregnancy.Trimester.Third), (added.Kind!.Value, added.State, added.TrimesterOnly));
        Assert.Equal("Blood count added to your journey. It's waiting for a date.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Not_booked_yet_in_a_week_waits_in_that_week()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Name = "Blood count";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");
        vm.Timing.LaterWeekCommand.Execute(null);
        vm.Timing.LaterWeekCommand.Execute(null);

        await vm.SaveCommand.ExecuteAsync(null);

        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal((CheckState.NotBooked, 26, (Domain.Pregnancy.Trimester?)null), (added.State, added.Week, added.TrimesterOnly));
    }

    [Fact]
    public async Task Already_done_in_a_week_is_added_as_done_with_no_date()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Name = "Blood count";
        vm.ChooseWhenCommand.Execute("Done");
        vm.Timing.ChooseModeCommand.Execute("Week");
        vm.Timing.EarlierWeekCommand.Execute(null);

        await vm.SaveCommand.ExecuteAsync(null);

        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal((CheckState.Done, 23, (Appointment?)null), (added.State, added.Week, added.Appointment));
        Assert.Equal("Blood count added to your journey, already done.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task The_pickers_settling_in_on_a_phone_choose_nothing_for_her()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();

        // As the page binds, the pickers write back what they can show: at worst their earliest day, and midnight.
        vm.Timing.Date = new DateTime(1900, 1, 1);
        vm.Timing.Time = TimeSpan.Zero;
        await vm.LoadAsync();

        Assert.Null(vm.When);
        Assert.Null(vm.Timing.Mode);
        vm.ChooseWhenCommand.Execute("Booked");
        Assert.Equal((new DateTime(2026, 9, 30), new TimeSpan(13, 0, 0)), (vm.Timing.Date, vm.Timing.Time));
    }

    // ---- Saving -----------------------------------------------------------------------------

    [Fact]
    public async Task Save_says_calmly_what_is_missing_and_saves_nothing_until_it_is_there()
    {
        var vm = await OpenAsync();
        var saves = _app.Repository.SaveCount;

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Choose what you're adding.", vm.ErrorMessage);

        vm.ChooseKindCommand.Execute("Exam");
        Assert.Null(vm.ErrorMessage);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Give it a name.", vm.ErrorMessage);

        vm.Name = "   ";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Give it a name.", vm.ErrorMessage);

        vm.Name = new string('a', AddEntryViewModel.MaxNameLength + 1);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("That name is a little long. Keep it to 150 characters.", vm.ErrorMessage);

        vm.Name = "Blood count";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Choose when it is.", vm.ErrorMessage);

        // "Already done" with nothing chosen under it is not yet a when.
        vm.ChooseWhenCommand.Execute("Done");
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Choose when it is.", vm.ErrorMessage);

        Assert.Equal(saves, _app.Repository.SaveCount);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task A_booking_on_a_day_that_has_passed_is_saved_as_she_gave_it_and_is_done()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Other");
        vm.Name = "Antenatal class";
        vm.ChooseWhenCommand.Execute("Booked");
        vm.Timing.Date = new DateTime(2026, 9, 20);
        vm.Timing.Time = new TimeSpan(18, 0, 0);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        var added = Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal((CheckState.Booked, new Appointment(new DateOnly(2026, 9, 20), new TimeOnly(18, 0))), (added.State, added.Appointment));
        Assert.Equal("Antenatal class added to your journey, done on Sun 20 Sep.", _app.Notifier.Shown.Single());
    }

    [Theory]
    [InlineData(2026, 9, 30, 12, 1)]
    [InlineData(2026, 10, 1, 9, 0)]
    public async Task A_day_and_time_done_after_now_is_asked_again(int year, int month, int day, int hour, int minute)
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Other");
        vm.Name = "Antenatal class";
        vm.ChooseWhenCommand.Execute("Done");
        vm.Timing.ChooseModeCommand.Execute("DateTime");
        vm.Timing.Date = new DateTime(year, month, day);
        vm.Timing.Time = new TimeSpan(hour, minute, 0);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Choose a day and time that has passed.", vm.ErrorMessage);
        Assert.DoesNotContain(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
    }

    [Fact]
    public async Task Save_adds_one_entry_in_one_save_and_stays_on_add_for_the_next_one()
    {
        var vm = await OpenAsync();
        var saves = _app.Repository.SaveCount;
        vm.ChooseKindCommand.Execute("Exam");
        vm.Name = "Blood count";
        vm.ChooseWhenCommand.Execute("Booked");
        vm.Timing.Date = new DateTime(2026, 10, 1);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal(saves + 1, _app.Repository.SaveCount);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Null(_app.Focus.Take());
        Assert.Equal("Blood count added to your journey for Thu 1 Oct at 13:00.", _app.Notifier.Shown.Single());

        // Add is a tab, so the page stays: once saved, it starts afresh for the next one.
        Assert.Equal(((EntryKind?)null, (AddWhen?)null, string.Empty, false), (vm.Kind, vm.When, vm.Name, vm.IsComplete));
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failed_save_keeps_everything_she_chose_to_try_again()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Suggestions.Single(s => s.Title == "Iron recheck").ChooseCommand.Execute(null);
        vm.ChooseWhenCommand.Execute("Booked");
        vm.Timing.Date = new DateTime(2026, 10, 20);
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", vm.ErrorMessage);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal((EntryKind.Exam, "Iron recheck", AddWhen.Booked, new DateTime(2026, 10, 20)), (vm.Kind!.Value, vm.Name, vm.When!.Value, vm.Timing.Date));
        Assert.Empty(_app.Navigator.Visited);
        Assert.Equal("add.save", Assert.Single(_app.Errors.Reports).Operation);
        Assert.DoesNotContain(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public async Task Nothing_she_adds_ever_reaches_the_error_report()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Name = "Private test name";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.SaveCommand.ExecuteAsync(null);

        var report = Assert.Single(_app.Errors.Reports);
        Assert.DoesNotContain("Private test name", report.ToString(), StringComparison.Ordinal);
    }

    // ---- Loading ----------------------------------------------------------------------------

    [Fact]
    public async Task Without_a_plan_add_sends_her_to_setup()
    {
        var vm = _app.CreateAdd();

        await vm.LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task A_plan_that_cannot_be_read_just_now_is_said_calmly_and_nothing_can_be_saved()
    {
        await _app.CreatePlanAsync();
        _app.Repository.FailNextLoad = new IOException("storage busy");
        var vm = _app.CreateAdd();

        await vm.LoadAsync();

        Assert.Equal("Something went wrong. Please try again.", vm.ErrorMessage);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Equal("add.load", Assert.Single(_app.Errors.Reports).Operation);

        await vm.LoadAsync();
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Each_refresh_reads_the_plan_once()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();
        var loads = _app.Repository.LoadCount;

        await vm.LoadAsync();

        Assert.Equal(loads + 1, _app.Repository.LoadCount);
    }

    [Fact]
    public async Task Coming_back_keeps_what_she_chose_and_typed()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute("Exam");
        var iron = vm.Suggestions.Single(s => s.Title == "Iron recheck");
        iron.ChooseCommand.Execute(null);
        vm.ChooseWhenCommand.Execute("Booked");
        vm.Timing.Date = new DateTime(2026, 11, 12);
        var chips = vm.Suggestions.ToList();

        await vm.LoadAsync();

        Assert.Equal(chips, vm.Suggestions);
        Assert.True(iron.IsChosen);
        Assert.Equal((EntryKind.Exam, "Iron recheck", AddWhen.Booked, new DateTime(2026, 11, 12)), (vm.Kind!.Value, vm.Name, vm.When!.Value, vm.Timing.Date));
    }

    [Fact]
    public async Task In_greek_the_screen_and_its_names_read_in_greek()
    {
        await _app.CreatePlanAsync();
        using var greek = new CultureScope("el-GR");
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute("HospitalVisit");
        vm.Name = "Επίσκεψη σε νοσοκομείο";
        vm.ChooseWhenCommand.Execute("Done");
        vm.Timing.ChooseModeCommand.Execute("DateTime");
        await vm.SaveCommand.ExecuteAsync(null);

        var visit = Assert.Single(_app.Repository.Stored!.Checks, c => c.Kind == EntryKind.HospitalVisit);
        Assert.Equal("Hospital visit", visit.Title.In(LocalizedText.English));
        Assert.Equal("Επίσκεψη σε νοσοκομείο: προστέθηκε στο πλάνο σου, έγινε Τετ 30 Σεπ.", _app.Notifier.Shown.Single());
    }


    // ---- A task, from Add --------------------------------------------------------------------

    [Fact]
    public async Task The_task_pill_adds_a_task_without_a_date_for_a_trimester_or_week_as_today_does()
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);
        Assert.True(vm.IsTask);
        Assert.False(vm.HasKind);
        Assert.False(vm.TaskHasDate);
        Assert.False(vm.IsComplete);

        // Her words are not enough without a date: it needs a trimester or a week, and saving says so.
        vm.TaskTitle = "  Pack the hospital bag ";
        Assert.False(vm.IsComplete);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Choose when it is.", vm.ErrorMessage);
        Assert.Empty(_app.Repository.Stored!.Tasks);

        // A date makes it complete too; turned off again, the trimester she chooses does.
        vm.TaskHasDate = true;
        Assert.True(vm.IsComplete);
        vm.TaskHasDate = false;
        vm.TaskPeriod.ChooseModeCommand.Execute("Trimester");
        Assert.True(vm.IsComplete);
        vm.TaskPeriod.ChooseTrimesterCommand.Execute("AfterBirth");
        Assert.True(vm.TaskPeriod.IsAfterBirth);
        await vm.SaveCommand.ExecuteAsync(null);

        var task = Assert.Single(_app.Repository.Stored!.Tasks);
        Assert.Equal(("Pack the hospital bag", (DateOnly?)null, (TimeOnly?)null, (Trimester?)Trimester.AfterBirth), (task.Title, task.DueDate, task.DueTime, task.Trimester));
        Assert.Equal("Task added.", _app.Notifier.Shown.Single());
        Assert.Empty(_app.Navigator.Visited);
        Assert.DoesNotContain(_app.Repository.Stored!.Checks, c => c.Origin == CheckOrigin.AddedByParent);

        // Once saved, she stays on Add, which starts afresh.
        Assert.Equal((false, string.Empty, false), (vm.IsTask, vm.TaskTitle, vm.TaskHasDate));
    }

    [Fact]
    public async Task A_task_with_a_date_starts_at_now_and_says_when_as_on_your_week()
    {
        var vm = await OpenAsync();

        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);
        Assert.Equal((new DateTime(2026, 9, 30), new TimeSpan(12, 0, 0)), (vm.TaskDate, vm.TaskTime));
        vm.TaskTitle = "Call the clinic";
        vm.TaskHasDate = true;
        vm.TaskDate = new DateTime(2026, 10, 2);
        vm.TaskTime = new TimeSpan(9, 0, 0);
        await vm.SaveCommand.ExecuteAsync(null);

        var task = Assert.Single(_app.Repository.Stored!.Tasks);
        Assert.Equal((new DateOnly(2026, 10, 2), new TimeOnly(9, 0)), (task.DueDate!.Value, task.DueTime!.Value));
        Assert.Equal("Task added for Fri 2 Oct at 09:00.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_task_without_words_is_not_saved_and_she_is_told_calmly()
    {
        var vm = await OpenAsync();
        var saves = _app.Repository.SaveCount;
        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Give it a name.", vm.ErrorMessage);
        Assert.Equal(saves, _app.Repository.SaveCount);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public async Task Choosing_a_kind_after_the_task_pill_goes_back_to_an_entry()
    {
        var vm = await OpenAsync();
        vm.ChooseKindCommand.Execute(AddEntryViewModel.TaskChoice);
        vm.TaskTitle = "Call the clinic";

        vm.ChooseKindCommand.Execute("Exam");

        Assert.False(vm.IsTask);
        Assert.Equal(EntryKind.Exam, vm.Kind);
        Assert.False(vm.IsComplete);
    }

    [Fact]
    public void The_task_pill_sits_with_the_four_kinds_and_its_date_switch_starts_off()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "AddEntryPage.xaml"));
        var pills = page.Descendants().Where(e => AccessibilityTests.Attr(e, "Command") == "{Binding ChooseKindCommand}").ToList();

        // The four kinds, Task, then Blood pressure.
        Assert.Equal(6, pills.Count);
        Assert.Equal("{loc:Translate Week_TaskLabel}", AccessibilityTests.Attr(pills[4], "Text"));
        Assert.Equal("{x:Static vm:AddEntryViewModel.TaskChoice}", AccessibilityTests.Attr(pills[4], "CommandParameter"));
        Assert.Contains(page.Descendants(), e => e.Name.LocalName == "PillSwitch" && AccessibilityTests.Attr(e, "IsToggled") == "{Binding TaskHasDate}");
    }

    private async Task<CareContactId> LinkDoctorAsync()
    {
        if (_app.Repository.Stored is null)
        {
            await _app.CreatePlanAsync();
        }

        await _app.Plans.LinkContactsAsync([new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", null, null)]);
        return _app.Repository.Stored!.CareTeam.Contacts.Single().Id;
    }

    private async Task<AddEntryViewModel> OpenAsync()
    {
        if (_app.Repository.Stored is null)
        {
            await _app.CreatePlanAsync();
        }

        var vm = _app.CreateAdd();
        await vm.LoadAsync();
        return vm;
    }
}
