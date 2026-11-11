using System.Xml.Linq;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Localization;
using Maia.Presentation.Plan;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Journey opens at today, can show her dated tasks on their day, and says what each entry she added is. Today is 24+3
/// (Wed 30 Sep 2026), due Sun 17 Jan.
/// </summary>
public sealed class JourneyTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();
    private readonly KeptJourneySettings _settings = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Opening_journey_brings_you_are_here_near_the_top_once_it_has_loaded()
    {
        await _app.CreatePlanAsync();
        var vm = NewJourney();
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);

        vm.ShowToday();
        Assert.Empty(scrolledTo);
        await vm.LoadAsync();

        // The check just before "You are here" starts the screen, so she sees where she is and what led up to it.
        Assert.Equal("check:ogtt", Assert.Single(scrolledTo).Key);
        Assert.Equal("here", vm.Rows[vm.Rows.IndexOf(scrolledTo[0]) + 1].Key);
    }

    [Fact]
    public async Task Opening_journey_again_goes_back_to_today_at_once()
    {
        var vm = await OpenJourneyAsync();
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);

        vm.ShowToday();

        Assert.Equal("check:ogtt", Assert.Single(scrolledTo).Key);
    }

    [Fact]
    public async Task A_reload_after_a_change_keeps_her_place()
    {
        var vm = await OpenJourneyAsync();
        vm.ShowToday();
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);

        var glucose = Row(vm, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);
        await glucose.SaveCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        Assert.Empty(scrolledTo);
    }

    [Fact]
    public async Task Coming_straight_back_from_a_screen_opened_over_journey_keeps_her_place()
    {
        var vm = await OpenJourneyAsync();
        vm.Arrive(from: null);
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);
        var add = new object();

        vm.Leave(coveringScreen: add);
        vm.Arrive(from: add);

        Assert.Empty(scrolledTo);
    }

    [Fact]
    public async Task Opening_journey_after_leaving_add_for_another_tab_goes_back_to_today()
    {
        var vm = await OpenJourneyAsync();
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);
        var add = new object();
        var week = new object();

        // Journey, then Add over it, then Week from Add's bar (Journey hears nothing), then Journey from the bar.
        vm.Leave(coveringScreen: add);
        vm.Arrive(from: week);

        Assert.Equal("check:ogtt", Assert.Single(scrolledTo).Key);

        // Add is forgotten once Journey shows: a later return from another tab opens at today too.
        vm.Leave(coveringScreen: null);
        vm.Arrive(from: add);

        Assert.Equal(2, scrolledTo.Count);
    }

    [Fact]
    public async Task A_check_chosen_on_another_screen_wins_over_today_and_stays_closed()
    {
        await _app.CreatePlanAsync();
        var vm = NewJourney();
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);
        _app.Focus.Request(TestData.Checkup25);

        vm.ShowToday();
        await vm.LoadAsync();

        Assert.Equal("check:checkup-25", Assert.Single(scrolledTo).Key);
        Assert.False(Row(vm, TestData.Checkup25).IsExpanded);
    }

    [Fact]
    public async Task With_a_card_open_just_above_today_you_are_here_itself_starts_the_screen()
    {
        var vm = await OpenJourneyAsync();
        Row(vm, TestData.Glucose).ToggleCommand.Execute(null);
        PlanRow? scrolledTo = null;
        vm.ScrollRequested += (_, row) => scrolledTo = row;

        vm.ShowToday();

        Assert.IsType<HereRow>(scrolledTo);
    }

    [Fact]
    public async Task You_are_here_keeps_its_place_even_with_no_check_left_in_its_trimester()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.DeleteEntryAsync(TestData.Anomaly);
        await _app.Plans.DeleteEntryAsync(TestData.Glucose);
        await _app.Plans.DeleteEntryAsync(TestData.Checkup25);

        var vm = await OpenJourneyAsync();

        Assert.Equal(
            new[] { "section:trimester1", "check:booking", "section:trimester2", "here", "section:trimester3", "check:due-date" },
            vm.Rows.Where(r => r is not WeekRow).Select(r => r.Key));
    }

    [Fact]
    public async Task Her_tasks_stand_on_their_day_or_at_their_week_among_her_checks()
    {
        await _app.CreatePlanAsync();
        var paid = await _app.Plans.AddTaskAsync("Pay the lab", new DateOnly(2026, 9, 20));
        var call = await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 9, 30), new TimeOnly(9, 0));
        var vitamins = await _app.Plans.AddTaskAsync("Buy vitamins", new DateOnly(2026, 10, 5));
        var remember = await _app.Plans.AddTaskAsync("Something to remember", week: 30);
        var bag = await _app.Plans.AddTaskAsync("Pack the hospital bag", new DateOnly(2027, 2, 1));

        var vm = await OpenJourneyAsync();

        // Tasks of a day come after its checks; today, one whose time has come (the call at 09:00, at noon) stands before
        // "You are here", like a booking that has begun. One with no date stands at the start of its week.
        Assert.Equal(
            new[]
            {
                "section:trimester1", "check:booking",
                "section:trimester2", "check:anomaly", "task:" + paid, "check:ogtt", "task:" + call, "here", "check:checkup-25", "task:" + vitamins,
                "section:trimester3", "task:" + remember, "check:due-date",
                "section:after_birth", "task:" + bag,
            },
            vm.Rows.Where(r => r is not WeekRow).Select(r => r.Key));
        var row = TaskOn(vm, call);
        Assert.Equal("Call the clinic", row.Title);
        Assert.Equal("Wed 30 Sep · 09:00", row.When);
        Assert.Equal("Call the clinic, Wed 30 Sep · 09:00", row.SpokenName);
        Assert.Equal("Mon 5 Oct", TaskOn(vm, vitamins).When);
        Assert.Equal("Week 30", TaskOn(vm, remember).When);
        Assert.True(TaskOn(vm, paid).LineAbove);
        Assert.True(TaskOn(vm, paid).LineBelow);
        // After birth, the bag is alone under its week's heading: the line comes down from the heading and stops at it.
        Assert.True(TaskOn(vm, bag).LineAbove);
        Assert.False(TaskOn(vm, bag).LineBelow);
    }

    [Fact]
    public async Task Booked_checks_stand_at_their_booking_after_you_are_here_in_time_order()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(8, 30)));

        var vm = await OpenJourneyAsync();

        Assert.Equal(
            new[]
            {
                "section:trimester1", "check:booking",
                "section:trimester2", "check:anomaly", "here", "check:checkup-25", "check:ogtt",
                "section:trimester3", "check:due-date",
            },
            vm.Rows.Where(r => r is not WeekRow).Select(r => r.Key));
    }

    [Fact]
    public async Task A_booking_later_today_stands_after_you_are_here_so_journey_opens_with_it_in_view()
    {
        // It is noon. The blood test this morning has begun, as has the pharmacy at noon, and the time of her call at 11
        // has come: they stand before "You are here" by their time. The yoga class this afternoon is still to come.
        await _app.CreatePlanAsync();
        var bloods = await _app.Plans.AddEntryAsync("Blood test", EntryKind.Exam, EntryWhen.Booked(new Appointment(TestData.Today, new TimeOnly(8, 30))));
        var yoga = await _app.Plans.AddEntryAsync("Prenatal yoga class", EntryKind.Other, EntryWhen.Booked(new Appointment(TestData.Today, new TimeOnly(15, 0))));
        var pharmacy = await _app.Plans.AddEntryAsync("Pharmacy", EntryKind.Other, EntryWhen.Booked(new Appointment(TestData.Today, new TimeOnly(12, 0))));
        var call = await _app.Plans.AddTaskAsync("Call the clinic", TestData.Today, new TimeOnly(11, 0));
        var vm = NewJourney();
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);

        vm.ShowToday();
        await vm.LoadAsync();

        string[] today = ["check:" + bloods, "task:" + call, "check:" + pharmacy, "here", "check:" + yoga];
        Assert.Equal(today, vm.Rows.Select(r => r.Key).Where(today.Contains));
        Assert.Equal(vm.Rows.IndexOf(vm.Rows.Single(r => r.Key == "here")) + 1, vm.Rows.IndexOf(Row(vm, yoga)));

        // Journey opens on the row just above "You are here", so the yoga class just below it is in view.
        Assert.Equal("check:" + pharmacy, Assert.Single(scrolledTo).Key);
    }

    [Fact]
    public async Task Once_its_time_has_come_todays_booking_stands_before_you_are_here()
    {
        await _app.CreatePlanAsync();
        var yoga = await _app.Plans.AddEntryAsync("Prenatal yoga class", EntryKind.Other, EntryWhen.Booked(new Appointment(TestData.Today, new TimeOnly(15, 0))));
        var vm = await OpenJourneyAsync();
        Assert.True(Index(vm, "here") < Index(vm, "check:" + yoga));

        _app.Clock.SetTime(TestData.Today, new TimeOnly(15, 0));
        await vm.LoadAsync();

        Assert.True(Index(vm, "check:" + yoga) < Index(vm, "here"));
    }

    [Fact]
    public async Task Todays_tasks_whose_time_has_passed_stand_before_you_are_here_like_bookings()
    {
        // It is 15:00. The iron tablet at 08:00 and the call at 14:30 are past; the pharmacy at 17:00 and a task with no
        // time are still to do today, so they follow "You are here".
        await _app.CreatePlanAsync();
        var bloods = await _app.Plans.AddEntryAsync("Blood test", EntryKind.Exam, EntryWhen.Booked(new Appointment(TestData.Today, new TimeOnly(9, 0))));
        var tablet = await _app.Plans.AddTaskAsync("Take the iron tablet", TestData.Today, new TimeOnly(8, 0));
        var call = await _app.Plans.AddTaskAsync("Call the clinic", TestData.Today, new TimeOnly(14, 30));
        var pharmacy = await _app.Plans.AddTaskAsync("Pharmacy", TestData.Today, new TimeOnly(17, 0));
        var someTime = await _app.Plans.AddTaskAsync("Buy vitamins", TestData.Today);
        _app.Clock.SetTime(TestData.Today, new TimeOnly(15, 0));

        var vm = await OpenJourneyAsync();

        string[] today = ["task:" + tablet, "check:" + bloods, "task:" + call, "here", "task:" + someTime, "task:" + pharmacy];
        Assert.Equal(today, vm.Rows.Select(r => r.Key).Where(today.Contains));

        // As the day goes on, the pharmacy's time comes too.
        _app.Clock.SetTime(TestData.Today, new TimeOnly(17, 0));
        await vm.LoadAsync();
        Assert.True(Index(vm, "task:" + pharmacy) < Index(vm, "here"));
        Assert.True(Index(vm, "here") < Index(vm, "task:" + someTime));
    }

    [Fact]
    public async Task The_tasks_switch_is_on_until_she_turns_it_off_and_is_remembered()
    {
        await _app.CreatePlanAsync();
        var call = await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var vm = await OpenJourneyAsync();
        Assert.True(vm.ShowTasks);
        Assert.Contains(vm.Rows, r => r.Key == "task:" + call);
        var loads = _app.Repository.LoadCount;

        // The switch is bound both ways: she turns it off, and the timeline changes at once, without a reload.
        vm.ShowTasks = false;

        Assert.False(_settings.ShowTasks);
        Assert.DoesNotContain(vm.Rows, r => r is TaskRow);
        Assert.Equal(loads, _app.Repository.LoadCount);

        var next = await OpenJourneyAsync();
        Assert.False(next.ShowTasks);
        Assert.DoesNotContain(next.Rows, r => r is TaskRow);

        next.ShowTasks = true;
        Assert.True(_settings.ShowTasks);
        Assert.Contains(next.Rows, r => r.Key == "task:" + call);
    }

    [Fact]
    public async Task Turning_the_tasks_switch_before_journey_has_loaded_just_remembers_it()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var vm = NewJourney();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ShowTasks = false;

        Assert.Equal(new[] { nameof(PlanViewModel.ShowTasks) }, changed);
        Assert.False(_settings.ShowTasks);
        Assert.Empty(vm.Rows);

        await vm.LoadAsync();
        Assert.DoesNotContain(vm.Rows, r => r is TaskRow);
    }

    [Fact]
    public void Journey_turns_her_tasks_on_and_off_with_a_switch_named_tasks_under_the_intro()
    {
        var page = XDocument.Load(Repo.App("Views", "PlanPage.xaml"));
        var header = page.Descendants().Single(e => e.Name.LocalName == "CollectionView.Header").Elements().Single();
        var intro = header.Elements().Single(e => AccessibilityTests.Attr(e, "Text") == "{loc:Translate Plan_Intro}");
        var row = intro.ElementsAfterSelf().Single();
        var toggle = row.Elements().Single(e => e.Name.LocalName == "PillSwitch");

        // A compact row at the start, right under the intro: the word, then the switch (its own 60 × 44 dp target), which
        // a screen reader hears by the same word.
        Assert.Equal("Start", AccessibilityTests.Attr(row, "HorizontalOptions"));
        Assert.Equal("{Binding ShowTasks}", AccessibilityTests.Attr(toggle, "IsToggled"));
        Assert.Equal("{loc:Translate Plan_ShowTasks}", AccessibilityTests.Attr(toggle, "Label"));
        Assert.Equal("{loc:Translate Plan_ShowTasks}", AccessibilityTests.Attr(row.Elements().First(), "Text"));
        Assert.Equal(("Tasks", "Εργασίες"), (Text.In("Plan_ShowTasks", Text.English), Text.In("Plan_ShowTasks", Text.Greek)));

        // No chip and no command any more: the switch sets the view model's ShowTasks itself.
        Assert.DoesNotContain(page.Descendants(), e => e.Name.LocalName == "ChoicePill");
        Assert.True(typeof(PlanViewModel).GetProperty(nameof(PlanViewModel.ShowTasks))!.SetMethod!.IsPublic);
        Assert.Null(typeof(PlanViewModel).GetProperty("ToggleTasksCommand"));
    }

    [Fact]
    public async Task Journey_has_no_week_eyebrow_and_you_are_here_says_just_that()
    {
        // The week and the due date show only on Your week.
        var page = XDocument.Load(Repo.App("Views", "PlanPage.xaml"));
        var header = page.Descendants().Single(e => e.Name.LocalName == "CollectionView.Header").Elements().Single();
        Assert.Equal("{loc:Translate Plan_Title}", AccessibilityTests.Attr(header.Elements().First(), "Text"));
        Assert.DoesNotContain(page.Descendants(), e => AccessibilityTests.Attr(e, "Text") == "{Binding Eyebrow}");
        Assert.Null(typeof(PlanViewModel).GetProperty("Eyebrow"));
        Assert.DoesNotContain("Plan_Eyebrow", Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture));

        var vm = await OpenJourneyAsync();
        Assert.Equal("You are here", vm.Rows.OfType<HereRow>().Single().Text);
        Assert.Equal("Είσαι εδώ", Text.In("Plan_Here", Text.Greek));
    }

    [Fact]
    public async Task On_a_phone_that_keeps_no_choice_her_tasks_show()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1));
        var vm = new PlanViewModel(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);

        await vm.LoadAsync();

        Assert.True(vm.ShowTasks);
        Assert.Single(vm.Rows.OfType<TaskRow>());
    }

    [Fact]
    public async Task Ticking_a_task_on_journey_ticks_it_off_as_your_week_does()
    {
        await _app.CreatePlanAsync();
        var call = await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var vm = await OpenJourneyAsync();
        var row = TaskOn(vm, call);

        await row.ToggleCommand.ExecuteAsync(null);

        Assert.True(row.IsDone);
        Assert.True(_app.Repository.Stored!.GetTask(call).IsDone);
        var week = _app.CreateWeek();
        await week.LoadAsync();
        Assert.DoesNotContain(week.Tasks, t => t.Id == call);

        // A ticked task stays on its day, ticked, and a second tap takes it back.
        await vm.LoadAsync();
        Assert.True(TaskOn(vm, call).IsDone);
        await TaskOn(vm, call).ToggleCommand.ExecuteAsync(null);
        Assert.False(_app.Repository.Stored!.GetTask(call).IsDone);
    }

    [Fact]
    public async Task A_task_ticked_on_journey_stays_ticked_when_she_hides_her_tasks_and_shows_them_again()
    {
        await _app.CreatePlanAsync();
        var call = await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1), new TimeOnly(9, 0));
        var vm = await OpenJourneyAsync();
        var scrolledTo = new List<PlanRow>();
        vm.ScrollRequested += (_, row) => scrolledTo.Add(row);
        var row = TaskOn(vm, call);

        await row.ToggleCommand.ExecuteAsync(null);

        // She keeps her place: the same row, ticked, and no scroll.
        Assert.Same(row, TaskOn(vm, call));
        Assert.True(row.IsDone);
        Assert.Empty(scrolledTo);

        vm.ShowTasks = false;
        vm.ShowTasks = true;

        Assert.True(TaskOn(vm, call).IsDone);

        // One tap takes it back: no second tap is needed.
        await TaskOn(vm, call).ToggleCommand.ExecuteAsync(null);
        Assert.False(_app.Repository.Stored!.GetTask(call).IsDone);
        Assert.False(TaskOn(vm, call).IsDone);
    }

    [Fact]
    public async Task A_task_that_cannot_be_ticked_stays_as_it_was_and_says_so_calmly()
    {
        await _app.CreatePlanAsync();
        var call = await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1));
        var vm = await OpenJourneyAsync();
        _app.Repository.FailNextSave = new IOException("Disk full.");

        await TaskOn(vm, call).ToggleCommand.ExecuteAsync(null);

        Assert.False(TaskOn(vm, call).IsDone);
        Assert.Single(_app.Notifier.Shown);
        Assert.All(_app.Errors.Reports, r => Assert.DoesNotContain("Call the clinic", r.Exception.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Entries_she_adds_say_what_they_are_and_stand_at_their_day()
    {
        await _app.CreatePlanAsync();
        var visit = await _app.Plans.AddEntryAsync("Visit with Dr Papadaki", EntryKind.DoctorVisit, EntryWhen.Booked(new Appointment(new DateOnly(2026, 10, 2), new TimeOnly(17, 0))));
        var tour = await _app.Plans.AddEntryAsync("Hospital tour", EntryKind.HospitalVisit, EntryWhen.NotBookedInWeek(24));
        var thyroid = await _app.Plans.AddEntryAsync("Thyroid test", EntryKind.Exam, EntryWhen.Booked(new Appointment(new DateOnly(2026, 9, 29), new TimeOnly(10, 0))));
        var classes = await _app.Plans.AddEntryAsync("Antenatal class", EntryKind.Other, EntryWhen.NotBookedInWeek(24));

        var vm = await OpenJourneyAsync();

        Assert.Equal("Doctor visit", Row(vm, visit).KindText);
        Assert.Equal("Hospital visit", Row(vm, tour).KindText);
        Assert.Equal("Exam or test", Row(vm, thyroid).KindText);
        Assert.Equal("Something else", Row(vm, classes).KindText);
        Assert.False(Row(vm, TestData.Glucose).HasKind);
        Assert.False(Row(vm, visit).HasNote);

        // Not booked yet: at the week it waits in, with no "needs a date". Done: on its day. Booked or done: at its day,
        // either side of today.
        Assert.Equal("2nd trimester · Week 24", Row(vm, tour).Caption);
        Assert.Equal("2nd trimester · Week 24 · Tue 29 Sep, 10:00", Row(vm, thyroid).Caption);
        Assert.Equal("Done", Row(vm, thyroid).StatusText);
        var keys = vm.Rows.Select(r => r.Key).ToList();
        Assert.True(keys.IndexOf("check:" + thyroid) < keys.IndexOf("here"));
        Assert.True(keys.IndexOf("here") < keys.IndexOf("check:" + visit));
        Assert.True(keys.IndexOf("check:" + visit) < keys.IndexOf("check:checkup-25"));
    }

    [Fact]
    public async Task Entry_kinds_and_the_new_words_read_in_greek()
    {
        await _app.CreatePlanAsync();
        var visit = await _app.Plans.AddEntryAsync("Επίσκεψη", EntryKind.DoctorVisit, EntryWhen.NotBookedInWeek(24));
        using var greek = new CultureScope("el-GR");

        var vm = await OpenJourneyAsync();

        Assert.Equal("Επίσκεψη σε γιατρό", Row(vm, visit).KindText);
        Assert.Equal("2ο τρίμηνο · Εβδομάδα 24", Row(vm, visit).Caption);
        Assert.Equal("Κλείσε", Text.Get("Check_Book"));
        Assert.Equal("Εργασίες", Text.Get("Plan_ShowTasks"));
    }

    [Fact]
    public void Every_kind_of_entry_has_its_label()
    {
        var keys = Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture);

        Assert.All(Enum.GetValues<EntryKind>(), kind => Assert.Contains($"Kind_{kind}", keys));
    }

    [Fact]
    public void Add_names_each_kind_in_the_words_journey_labels_it_with()
    {
        var pills = XDocument.Load(Repo.App("Views", "AddEntryPage.xaml")).Descendants()
            .Where(e => AccessibilityTests.Attr(e, "Command") == "{Binding ChooseKindCommand}")
            .Where(e => AccessibilityTests.Attr(e, "CommandParameter") is not "{x:Static vm:AddEntryViewModel.TaskChoice}" and not "{x:Static vm:AddEntryViewModel.BloodPressureChoice}")
            .ToDictionary(e => AccessibilityTests.Attr(e, "CommandParameter")!, e => AccessibilityTests.Attr(e, "Text"));

        Assert.Equal(Enum.GetNames<EntryKind>().Order(), pills.Keys.Order());
        Assert.All(Enum.GetValues<EntryKind>(), kind => Assert.Equal($"{{loc:Translate Kind_{kind}}}", pills[kind.ToString()]));
        Assert.Equal(
            new[] { "Doctor visit", "Exam or test", "Hospital visit", "Something else" },
            Enum.GetValues<EntryKind>().Select(kind => Text.In($"Kind_{kind}", Text.English)));
        Assert.Equal(
            new[] { "Επίσκεψη σε γιατρό", "Εξέταση", "Επίσκεψη σε νοσοκομείο", "Κάτι άλλο" },
            Enum.GetValues<EntryKind>().Select(kind => Text.In($"Kind_{kind}", Text.Greek)));
    }

    [Fact]
    public void Add_shows_the_kinds_without_a_title_but_a_screen_reader_still_hears_what_is_it()
    {
        var page = XDocument.Load(Repo.App("Views", "AddEntryPage.xaml"));
        var kinds = page.Descendants().Where(e => AccessibilityTests.Attr(e, "Command") == "{Binding ChooseKindCommand}").Select(e => e.Parent!).Distinct().Single();

        Assert.DoesNotContain(page.Descendants(), e => e.Name.LocalName == "Label" && AccessibilityTests.Attr(e, "Text") == "{loc:Translate Add_What}");
        Assert.Equal("{loc:Translate Add_What}", AccessibilityTests.Attr(kinds, "SemanticProperties.Description"));
        Assert.Equal("What is it?", Text.In("Add_What", Text.English));
        Assert.Equal("Τι είναι;", Text.In("Add_What", Text.Greek));
    }

    [Fact]
    public void A_card_holds_no_results_they_are_kept_on_results_alone()
    {
        var page = File.ReadAllText(Repo.App("Views", "PlanPage.xaml"));

        foreach (var gone in new[] { "UploadCommand", "TakePhotoCommand", "OpenResultCommand", "AskForResultsCommand", "ShowResultsPanel", "HasResults", "LinkForm" })
        {
            Assert.DoesNotContain(gone, page, StringComparison.Ordinal);
        }

        Assert.Null(typeof(CheckRow).GetProperty("Results"));
        Assert.Null(typeof(CheckRow).GetProperty("ResultCount"));
    }

    private PlanViewModel NewJourney() =>
        new(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus, _settings);

    private async Task<PlanViewModel> OpenJourneyAsync()
    {
        if (_app.Repository.Stored is null)
        {
            await _app.CreatePlanAsync();
        }

        var vm = NewJourney();
        await vm.LoadAsync();
        return vm;
    }

    private static CheckRow Row(PlanViewModel vm, CheckId id) => vm.Rows.OfType<CheckRow>().Single(r => r.Id == id);

    private static TaskRow TaskOn(PlanViewModel vm, TaskId id) => vm.Rows.OfType<TaskRow>().Single(r => r.Id == id);

    private static int Index(PlanViewModel vm, string key) => vm.Rows.Select(r => r.Key).ToList().IndexOf(key);

    /// <summary>The "Tasks" switch as the phone keeps it, on until she turns it off.</summary>
    private sealed class KeptJourneySettings : IJourneySettings
    {
        public bool ShowTasks { get; set; } = true;
    }
}
