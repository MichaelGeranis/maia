using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Plans;
using Maia.Presentation.Care;
using Maia.Presentation.Plan;
using Maia.Presentation.Setup;
using Maia.Presentation.Week;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class TeamViewModelTests : IDisposable
{
    private static readonly PickedContact Doctor = new("Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1");
    private static readonly PickedContact Clinic = new("Papadaki Clinic", "+30 210 000 2210", "Reception", "c2");
    private static readonly PickedContact Dentist = new("Dr. Maria Kosta", "+30 690 000 1234", "Mobile", "c4");

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public void Care_team_is_a_card_in_settings_right_after_her_pregnancy()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "SettingsPage.xaml"));
        var card = page.Descendants().Single(e => AccessibilityTests.Attr(e, "BindingContext") == "{Binding Team, x:DataType=vm:SettingsViewModel}");
        var heading = card.Descendants().First(e => AccessibilityTests.Attr(e, "Text") == "{loc:Translate Team_Title}");

        Assert.Equal("care:TeamViewModel", AccessibilityTests.Attr(card, "x:DataType") ?? AccessibilityTests.Attr(card, "DataType"));
        Assert.Equal("{Binding DueDate.HasPlan}", AccessibilityTests.Attr(card.ElementsBeforeSelf().Last(), "IsVisible"));
        Assert.Equal("Level2", AccessibilityTests.Attr(heading, "SemanticProperties.HeadingLevel"));
        Assert.Contains(card.Descendants(), e => AccessibilityTests.Attr(e, "Command") == "{Binding LinkCommand}");
        Assert.Contains(card.Descendants(), e => AccessibilityTests.Attr(e, "Command") == "{Binding LinkDoctorCommand}");
        Assert.DoesNotContain(card.Descendants(), e => AccessibilityTests.Attr(e, "Style") == "{StaticResource Eyebrow}");
        Assert.Contains("_viewModel.Team.LoadAsync", Repo.CodeOf(Repo.App("Views", "SettingsPage.xaml.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Team_Eyebrow", Localization.Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Care_team_is_folded_until_she_taps_its_heading_and_folds_again_on_a_second_tap()
    {
        var vm = await OpenAsync();
        Assert.False(vm.IsOpen);

        vm.ToggleCommand.Execute(null);
        Assert.True(vm.IsOpen);

        vm.ToggleCommand.Execute(null);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public async Task Folding_drops_a_question_or_note_it_was_showing()
    {
        var vm = await OpenAsync();
        vm.ToggleCommand.Execute(null);
        _app.Contacts.Picks.Enqueue(Doctor);
        _app.Contacts.Picks.Enqueue(new PickedContact("Nikos", " ", null, "c3"));
        await vm.LinkCommand.ExecuteAsync(null);
        vm.AskUnlinkCommand.Execute(vm.ObGyns[0]);

        vm.ToggleCommand.Execute(null);
        Assert.False(vm.IsConfirmingUnlink);

        vm.ToggleCommand.Execute(null);
        await vm.LinkCommand.ExecuteAsync(null);
        Assert.NotNull(vm.Notice);
        vm.Fold();
        Assert.Null(vm.Notice);
        Assert.Single(vm.ObGyns);
    }

    [Fact]
    public async Task Leaving_settings_folds_care_team_again()
    {
        await _app.CreatePlanAsync();
        var settings = _app.CreateSettings();
        settings.Team.ToggleCommand.Execute(null);

        settings.Leave();

        Assert.False(settings.Team.IsOpen);
    }

    [Fact]
    public void Its_heading_is_one_tap_read_out_as_an_expander_and_link_from_contacts_shows_only_without_an_ob_gyn()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "SettingsPage.xaml"));
        var card = page.Descendants().Single(e => AccessibilityTests.Attr(e, "BindingContext") == "{Binding Team, x:DataType=vm:SettingsViewModel}");
        var heading = card.Descendants().Single(e => AccessibilityTests.Attr(e, "Accessible.Role") == "Expander");
        var links = card.Descendants().Where(e => AccessibilityTests.Attr(e, "Text") == "{loc:Translate Team_LinkFromContacts}").ToList();

        Assert.Equal("{Binding IsOpen}", AccessibilityTests.Attr(heading, "Accessible.IsOn"));
        Assert.Contains(heading.Descendants(), e => AccessibilityTests.Attr(e, "Command") == "{Binding ToggleCommand}");
        Assert.Contains(heading.Descendants(), e => AccessibilityTests.Attr(e, "Text") == "{loc:Translate Team_Title}");
        Assert.Equal("{Binding IsOpen}", AccessibilityTests.Attr(heading.ElementsAfterSelf().Single(), "IsVisible"));
        var link = Assert.Single(links);
        Assert.Equal("{Binding HasObGyns, Converter={StaticResource InvertedBool}}", AccessibilityTests.Attr(link, "IsVisible"));
    }

    [Fact]
    public async Task Link_another_doctor_offers_her_ob_gyn_first_for_a_second_contact()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        _app.Contacts.Picks.Enqueue(Clinic);
        await vm.LinkCommand.ExecuteAsync(null);
        _app.Chooser.Taps("OB-GYN");

        await vm.LinkDoctorCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Dr. Eleni Papadaki", "Papadaki Clinic" }, vm.ObGyns.Select(c => c.Name));
        Assert.Equal("OB-GYN", _app.Chooser.Asked.Single().Choices[0]);
        Assert.All(vm.DoctorRows, r => Assert.False(r.IsShown));
    }

    [Fact]
    public async Task Linking_from_the_phone_picker_adds_her_ob_gyn()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);

        await vm.LinkCommand.ExecuteAsync(null);

        var contact = Assert.Single(vm.ObGyns);
        Assert.Equal(("Dr. Eleni Papadaki", "EP", "Mobile · ••• 4567"), (contact.Name, contact.Initials, contact.Meta));
        Assert.Equal("1 contact linked to your OB-GYN.", _app.Notifier.Shown.Single());
        Assert.Equal(CareRole.ObGyn, Assert.Single(_app.Repository.Stored!.CareTeam.Contacts).Role);
    }

    [Fact]
    public async Task Cancelling_the_picker_or_picking_a_contact_without_a_number_links_nothing()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(null);
        _app.Contacts.Picks.Enqueue(new PickedContact("Nikos", " ", null, "c3"));

        await vm.LinkCommand.ExecuteAsync(null);
        await vm.LinkCommand.ExecuteAsync(null);

        Assert.Empty(vm.ObGyns);
        Assert.Equal("That contact has no phone number. Pick one that does.", vm.Notice);
    }

    [Fact]
    public async Task Call_and_unlink_work_from_each_row()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        _app.Contacts.Picks.Enqueue(Clinic);
        await vm.LinkCommand.ExecuteAsync(null);
        await vm.LinkCommand.ExecuteAsync(null);

        vm.CallCommand.Execute(vm.ObGyns[1]);
        vm.AskUnlinkCommand.Execute(vm.ObGyns[0]);
        Assert.Equal("Unlink Dr. Eleni Papadaki? Your bookings stay in your journey.", vm.UnlinkQuestion);
        await vm.UnlinkCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "+30 210 000 2210" }, _app.Dialer.Dialled);
        Assert.Equal("Papadaki Clinic", Assert.Single(vm.ObGyns).Name);
        Assert.False(vm.IsConfirmingUnlink);
        Assert.Equal(new[] { "Calling Papadaki Clinic…", "Dr. Eleni Papadaki unlinked." }, _app.Notifier.Shown.TakeLast(2));
    }

    [Fact]
    public async Task Unlinking_asks_once_more_and_keep_changes_nothing()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await vm.LinkCommand.ExecuteAsync(null);
        var doctor = vm.ObGyns.Single();
        Assert.Equal(("Call Dr. Eleni Papadaki", "Unlink Dr. Eleni Papadaki"), (doctor.CallLabel, doctor.UnlinkLabel));

        vm.AskUnlinkCommand.Execute(doctor);
        Assert.True(vm.IsConfirmingUnlink);
        vm.KeepContactCommand.Execute(null);
        await vm.UnlinkCommand.ExecuteAsync(null);

        Assert.False(vm.IsConfirmingUnlink);
        Assert.Single(vm.ObGyns);
        Assert.Single(_app.Repository.Stored!.CareTeam.Contacts);
    }

    [Fact]
    public async Task Her_other_doctors_have_a_row_each_in_their_order_and_none_shows_until_she_links_someone()
    {
        var vm = await OpenAsync();

        Assert.Equal(
            new[] { "Dentist", "Internist", "Endocrinologist", "Haematologist", "Cardiologist", "Dermatologist", "Gastroenterologist" },
            vm.DoctorTiles.Select(t => t.Name));
        Assert.All(vm.DoctorRows, r => Assert.Single(r.Tiles));
        Assert.Equal(vm.DoctorTiles, vm.DoctorRows.SelectMany(r => r.Tiles));
        Assert.All(vm.DoctorTiles, t => Assert.False(t.HasContacts));
        Assert.All(vm.DoctorRows, r => Assert.False(r.IsShown));
    }

    [Fact]
    public async Task Care_team_shows_only_the_doctors_she_has_linked_someone_to()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync(
        [
            new ContactToLink(CareRole.Dentist, Dentist.DisplayName, Dentist.PhoneNumber, Dentist.Label, Dentist.DeviceContactId),
            new ContactToLink(CareRole.Cardiologist, Clinic.DisplayName, Clinic.PhoneNumber, Clinic.Label, Clinic.DeviceContactId),
        ]);
        var vm = NewViewModel();

        await vm.LoadAsync();

        Assert.Equal(
            new[] { "Dentist", "Cardiologist" },
            vm.DoctorRows.Where(r => r.IsShown).SelectMany(r => r.Tiles).Select(t => t.Name));
    }

    [Fact]
    public async Task Link_another_doctor_asks_which_doctor_then_links_the_contact_to_that_one()
    {
        var vm = await OpenAsync();
        _app.Chooser.Taps("Cardiologist");
        _app.Contacts.Picks.Enqueue(Dentist);

        await vm.LinkDoctorCommand.ExecuteAsync(null);

        var (title, choices) = _app.Chooser.Asked.Single();
        Assert.Equal("Which doctor?", title);
        Assert.Equal(vm.DoctorTiles.Select(t => t.Name).Prepend("OB-GYN"), choices);
        var cardiologist = Tile(vm, CareRole.Cardiologist);
        Assert.Equal("Dr. Maria Kosta", Assert.Single(cardiologist.Contacts).Name);
        Assert.True(vm.DoctorRows.Single(r => r.Tiles.Contains(cardiologist)).IsShown);
        Assert.Single(vm.DoctorRows, r => r.IsShown);
        Assert.Equal(CareRole.Cardiologist, Assert.Single(_app.Repository.Stored!.CareTeam.Contacts).Role);
        Assert.Equal("Dr. Maria Kosta linked.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Closing_the_list_of_doctors_links_nobody_and_asks_for_no_contact()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Dentist);

        await vm.LinkDoctorCommand.ExecuteAsync(null);

        Assert.Single(_app.Chooser.Asked);
        Assert.Single(_app.Contacts.Picks);
        Assert.Empty(_app.Repository.Stored!.CareTeam.Contacts);
        Assert.All(vm.DoctorRows, r => Assert.False(r.IsShown));
    }

    [Fact]
    public void In_greek_her_doctors_have_their_greek_names()
    {
        using var _ = new CultureScope("el-GR");

        var vm = NewViewModel();

        Assert.Equal(
            new[] { "Οδοντίατρος", "Παθολόγος", "Ενδοκρινολόγος", "Αιματολόγος", "Καρδιολόγος", "Δερματολόγος", "Γαστρεντερολόγος" },
            vm.DoctorTiles.Select(t => t.Name));
    }

    [Fact]
    public async Task Linking_a_dentist_puts_the_contact_on_her_tile_and_not_with_her_ob_gyn()
    {
        var vm = await OpenAsync();
        var dentist = Tile(vm, CareRole.Dentist);
        _app.Contacts.Picks.Enqueue(Dentist);

        await LinkDoctorAsync(vm, dentist.Role);

        var contact = Assert.Single(dentist.Contacts);
        Assert.Equal(("Dr. Maria Kosta", "Mobile · ••• 1234", "Call Dr. Maria Kosta"), (contact.Name, contact.Meta, contact.CallLabel));
        Assert.True(dentist.HasContacts);
        Assert.Empty(vm.ObGyns);
        Assert.All(vm.DoctorTiles.Where(t => t != dentist), t => Assert.Empty(t.Contacts));
        Assert.Equal("Dr. Maria Kosta linked.", _app.Notifier.Shown.Single());
        Assert.Equal(CareRole.Dentist, Assert.Single(_app.Repository.Stored!.CareTeam.Contacts).Role);
    }

    [Fact]
    public async Task A_doctor_can_have_more_than_one_contact_and_the_same_number_only_once()
    {
        var vm = await OpenAsync();
        var cardiologist = Tile(vm, CareRole.Cardiologist);
        _app.Contacts.Picks.Enqueue(Dentist);
        _app.Contacts.Picks.Enqueue(Clinic);
        _app.Contacts.Picks.Enqueue(Dentist with { DisplayName = "Maria" });

        await LinkDoctorAsync(vm, cardiologist.Role);
        await LinkDoctorAsync(vm, cardiologist.Role);
        await LinkDoctorAsync(vm, cardiologist.Role);

        Assert.Equal(new[] { "Dr. Maria Kosta", "Papadaki Clinic" }, cardiologist.Contacts.Select(c => c.Name));
        Assert.Equal(new[] { "Dr. Maria Kosta linked.", "Papadaki Clinic linked." }, _app.Notifier.Shown);
    }

    [Fact]
    public async Task The_same_contact_can_be_her_ob_gyn_and_another_doctor()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        _app.Contacts.Picks.Enqueue(Doctor);

        await vm.LinkCommand.ExecuteAsync(null);
        await LinkDoctorAsync(vm, CareRole.Internist);

        Assert.Equal("Dr. Eleni Papadaki", Assert.Single(vm.ObGyns).Name);
        Assert.Equal("Dr. Eleni Papadaki", Assert.Single(Tile(vm, CareRole.Internist).Contacts).Name);
    }

    [Fact]
    public async Task Calling_a_doctor_rings_that_contact()
    {
        var vm = await OpenAsync();
        var dermatologist = Tile(vm, CareRole.Dermatologist);
        _app.Contacts.Picks.Enqueue(Dentist);
        await LinkDoctorAsync(vm, dermatologist.Role);

        vm.CallCommand.Execute(dermatologist.Contacts[0]);

        Assert.Equal(new[] { "+30 690 000 1234" }, _app.Dialer.Dialled);
        Assert.Equal("Calling Dr. Maria Kosta…", _app.Notifier.Shown.Last());
    }

    [Fact]
    public async Task Unlinking_a_doctors_contact_asks_under_its_row_and_nowhere_else()
    {
        var vm = await OpenAsync();
        var haematologist = Tile(vm, CareRole.Haematologist);
        _app.Contacts.Picks.Enqueue(Dentist);
        await LinkDoctorAsync(vm, haematologist.Role);
        var contact = haematologist.Contacts.Single();
        Assert.Equal("Unlink Dr. Maria Kosta", contact.UnlinkLabel);

        vm.AskUnlinkCommand.Execute(contact);

        var row = vm.DoctorRows.Single(r => r.Tiles.Contains(haematologist));
        Assert.True(row.IsConfirmingUnlink);
        Assert.Equal("Unlink Dr. Maria Kosta? Your bookings stay in your journey.", row.UnlinkQuestion);
        Assert.False(vm.IsConfirmingUnlink);
        Assert.All(vm.DoctorRows.Where(r => r != row), r => Assert.False(r.IsConfirmingUnlink));

        await vm.UnlinkCommand.ExecuteAsync(null);

        Assert.False(row.IsConfirmingUnlink);
        Assert.Empty(haematologist.Contacts);
        Assert.False(haematologist.HasContacts);
        Assert.False(row.IsShown);
        Assert.Empty(_app.Repository.Stored!.CareTeam.Contacts);
        Assert.Equal("Dr. Maria Kosta unlinked.", _app.Notifier.Shown.Last());
    }

    [Fact]
    public async Task Keep_leaves_a_doctors_contact_linked()
    {
        var vm = await OpenAsync();
        var gastroenterologist = Tile(vm, CareRole.Gastroenterologist);
        _app.Contacts.Picks.Enqueue(Dentist);
        await LinkDoctorAsync(vm, gastroenterologist.Role);

        vm.AskUnlinkCommand.Execute(gastroenterologist.Contacts[0]);
        vm.KeepContactCommand.Execute(null);
        await vm.UnlinkCommand.ExecuteAsync(null);

        Assert.All(vm.DoctorRows, r => Assert.False(r.IsConfirmingUnlink));
        Assert.Single(gastroenterologist.Contacts);
        Assert.Single(_app.Repository.Stored!.CareTeam.Contacts);
    }

    [Fact]
    public async Task Asking_about_another_contact_moves_the_question_to_it()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        _app.Contacts.Picks.Enqueue(Dentist);
        await vm.LinkCommand.ExecuteAsync(null);
        await LinkDoctorAsync(vm, CareRole.Endocrinologist);

        vm.AskUnlinkCommand.Execute(vm.ObGyns[0]);
        Assert.True(vm.IsConfirmingUnlink);
        vm.AskUnlinkCommand.Execute(Tile(vm, CareRole.Endocrinologist).Contacts[0]);
        await vm.UnlinkCommand.ExecuteAsync(null);

        // Only the one asked about last goes: her OB-GYN stays.
        Assert.False(vm.IsConfirmingUnlink);
        Assert.Equal("Dr. Eleni Papadaki", Assert.Single(vm.ObGyns).Name);
        Assert.Empty(Tile(vm, CareRole.Endocrinologist).Contacts);
    }

    [Fact]
    public async Task A_doctors_contact_without_a_number_is_explained_under_its_row()
    {
        var vm = await OpenAsync();
        var dentist = Tile(vm, CareRole.Dentist);
        _app.Contacts.Picks.Enqueue(Dentist);
        await LinkDoctorAsync(vm, dentist.Role);
        _app.Contacts.Picks.Enqueue(new PickedContact("Nikos", " ", null, "c3"));

        await LinkDoctorAsync(vm, dentist.Role);

        var row = vm.DoctorRows.Single(r => r.Tiles.Contains(dentist));
        Assert.Equal("That contact has no phone number. Pick one that does.", row.Notice);
        Assert.Null(vm.Notice);
        Assert.Null(vm.DoctorsNotice);
        Assert.All(vm.DoctorRows.Where(r => r != row), r => Assert.Null(r.Notice));
        Assert.Single(dentist.Contacts);

        // The next thing she does clears it.
        _app.Contacts.Picks.Enqueue(null);
        await LinkDoctorAsync(vm, dentist.Role);
        Assert.Null(row.Notice);
    }

    [Fact]
    public async Task A_contact_without_a_number_for_a_doctor_not_shown_yet_is_explained_under_link_another_doctor()
    {
        var vm = await OpenAsync();
        _app.Chooser.Taps("Dentist");
        _app.Contacts.Picks.Enqueue(new PickedContact("Nikos", " ", null, "c3"));

        await vm.LinkDoctorCommand.ExecuteAsync(null);

        Assert.Equal("That contact has no phone number. Pick one that does.", vm.DoctorsNotice);
        Assert.Null(vm.Notice);
        Assert.All(vm.DoctorRows, r => Assert.Null(r.Notice));
        Assert.All(vm.DoctorRows, r => Assert.False(r.IsShown));

        // The next thing she does clears it.
        _app.Chooser.Pick = _ => null;
        await vm.LinkDoctorCommand.ExecuteAsync(null);
        Assert.Null(vm.DoctorsNotice);
    }

    [Fact]
    public async Task A_full_doctor_says_how_many_it_holds_and_her_ob_gyn_can_still_be_linked()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync(Enumerable.Range(0, CareTeam.MaxContactsPerRole)
            .Select(i => new ContactToLink(CareRole.Dentist, $"Contact {i}", $"+30 690 000 00{i:00}", null, $"c{i}")));
        var vm = NewViewModel();
        await vm.LoadAsync();
        var dentist = Tile(vm, CareRole.Dentist);
        _app.Contacts.Picks.Enqueue(Clinic);
        _app.Contacts.Picks.Enqueue(Clinic);

        await LinkDoctorAsync(vm, dentist.Role);
        Assert.Equal("You can link up to 10 contacts here. Unlink one to add another.", vm.DoctorRows[0].Notice);
        await vm.LinkCommand.ExecuteAsync(null);

        Assert.Equal(CareTeam.MaxContactsPerRole, dentist.Contacts.Count);
        Assert.Equal("Papadaki Clinic", Assert.Single(vm.ObGyns).Name);
        Assert.Null(vm.DoctorRows[0].Notice);
    }

    [Theory]
    [InlineData("Dr. Eleni Papadaki", "EP")]
    [InlineData("Papadaki Clinic", "PC")]
    [InlineData("Mom", "M")]
    [InlineData("Δρ. Ελένη Παπαδάκη", "ΕΠ")]
    public void Initials_skip_titles(string name, string initials) =>
        Assert.Equal(initials, ContactItem.InitialsOf(name));

    [Fact]
    public async Task Rows_that_stay_are_kept_in_place_and_only_a_new_contact_is_added()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await vm.LinkCommand.ExecuteAsync(null);
        var doctorRow = Assert.Single(vm.ObGyns);
        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        vm.ObGyns.CollectionChanged += (_, e) => changes.Add(e.Action);

        await vm.LoadAsync();
        _app.Contacts.Picks.Enqueue(Clinic);
        await vm.LinkCommand.ExecuteAsync(null);

        Assert.Same(doctorRow, vm.ObGyns[0]);
        Assert.Equal("Papadaki Clinic", vm.ObGyns[1].Name);
        Assert.Equal(new[] { System.Collections.Specialized.NotifyCollectionChangedAction.Add }, changes);
    }

    [Fact]
    public async Task Unlinking_a_contact_leaves_the_rows_below_it_as_they_were()
    {
        var vm = await OpenAsync();
        await _app.Plans.LinkContactsAsync(
        [
            new ContactToLink(CareRole.ObGyn, Doctor.DisplayName, Doctor.PhoneNumber, Doctor.Label, Doctor.DeviceContactId),
            new ContactToLink(CareRole.ObGyn, Clinic.DisplayName, Clinic.PhoneNumber, Clinic.Label, Clinic.DeviceContactId),
        ]);
        await vm.LoadAsync();
        var clinicRow = vm.ObGyns[1];

        vm.AskUnlinkCommand.Execute(vm.ObGyns[0]);
        await vm.UnlinkCommand.ExecuteAsync(null);

        Assert.Same(clinicRow, Assert.Single(vm.ObGyns));
    }

    private async Task<TeamViewModel> OpenAsync()
    {
        await _app.CreatePlanAsync();
        var vm = NewViewModel();
        await vm.LoadAsync();
        return vm;
    }

    private TeamViewModel NewViewModel() => new(_app.Queries, _app.Plans, _app.Contacts, _app.Dialer, _app.Chooser, _app.Notifier, _app.Errors);

    private static DoctorTile Tile(TeamViewModel vm, CareRole role) => vm.DoctorTiles.Single(t => t.Role == role);

    /// <summary>"Link another doctor", tapping <paramref name="role"/> in the list of doctors.</summary>
    private async Task LinkDoctorAsync(TeamViewModel vm, CareRole role)
    {
        _app.Chooser.Taps(Tile(vm, role).Name);
        await vm.LinkDoctorCommand.ExecuteAsync(null);
    }
}

public sealed class SetupCareViewModelTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Start_creates_the_plan_with_her_doctor_and_reminders()
    {
        var vm = await OpenAsync();
        Assert.Equal("Choose from contacts", vm.PickText);
        _app.Contacts.Picks.Enqueue(new PickedContact("Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1"));

        await vm.PickCommand.ExecuteAsync(null);
        await vm.ToggleRemindersCommand.ExecuteAsync(null);
        Assert.Equal("Link another contact", vm.PickText);
        Assert.True(vm.RemindMe);

        await vm.StartCommand.ExecuteAsync(null);

        var plan = _app.Repository.Stored;
        Assert.NotNull(plan);
        Assert.DoesNotContain(plan.Checks, c => c.Id == TestData.Anomaly);
        Assert.Equal("Dr. Eleni Papadaki", Assert.Single(plan.CareTeam.For(CareRole.ObGyn)).DisplayName);
        Assert.True(_app.ReminderSettings.IsOn);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Your journey is ready.", _app.Notifier.Shown.Single());
        Assert.Null(_app.Setup.DueDate);
    }

    [Fact]
    public async Task Skip_creates_the_plan_without_doctor_or_reminders()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(new PickedContact("Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1"));
        await vm.PickCommand.ExecuteAsync(null);
        await vm.ToggleRemindersCommand.ExecuteAsync(null);

        await vm.SkipCommand.ExecuteAsync(null);

        Assert.NotNull(_app.Repository.Stored);
        Assert.Empty(_app.Repository.Stored.CareTeam.Contacts);
        Assert.False(_app.ReminderSettings.IsOn);
    }

    [Fact]
    public async Task Refused_notifications_are_explained_and_remind_me_stays_off()
    {
        _app.NotificationAccess.WillGrant = false;
        var vm = await OpenAsync();

        await vm.ToggleRemindersCommand.ExecuteAsync(null);

        Assert.False(vm.RemindMe);
        Assert.StartsWith("Maia can't show notifications yet.", vm.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_due_date_it_goes_back_to_step_one()
    {
        var vm = NewViewModel();

        await vm.LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
    }

    private SetupCareViewModel NewViewModel() => _app.CreateSetupCare();

    private async Task<SetupCareViewModel> OpenAsync()
    {
        _app.Setup.DueDate = TestData.DueDate;
        _app.Setup.IncludeOverrides[TestData.Anomaly] = false;
        var vm = NewViewModel();
        await vm.LoadAsync();
        return vm;
    }
}

public sealed class CallToBookTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Your_week_names_the_doctor_and_its_next_up_card_has_no_call_button()
    {
        await LinkDoctorAndClinicAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = _app.CreateWeek();

        await vm.LoadAsync();

        Assert.Equal("with Dr. Eleni Papadaki", vm.NextUpWith);
        Assert.Null(typeof(WeekViewModel).GetProperty("CanCall"));
        Assert.Null(typeof(WeekViewModel).GetProperty("CallCommand"));
        Assert.DoesNotContain("IconPhone", File.ReadAllText(Repo.App("Views", "WeekPage.xaml")), StringComparison.Ordinal);
    }

    [Fact]
    public void Journey_items_offer_no_call_to_her_doctor()
    {
        Assert.Null(typeof(CheckRow).GetProperty("CanCall"));
        Assert.Null(typeof(CheckRow).GetProperty("CallCommand"));
        Assert.DoesNotContain("IconPhone", File.ReadAllText(Repo.App("Views", "PlanPage.xaml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_timed_task_is_never_next_up()
    {
        await LinkDoctorAndClinicAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", new DateOnly(2026, 10, 3), new TimeOnly(9, 0));
        var vm = _app.CreateWeek();

        await vm.LoadAsync();

        Assert.False(vm.HasNextUp);
        Assert.NotEqual("Pack the hospital bag", vm.NextUpTitle);
    }

    [Fact]
    public async Task A_doctor_visit_she_renamed_does_not_say_who_it_is_with()
    {
        await LinkDoctorAndClinicAsync();
        var add = _app.CreateAdd();
        await add.LoadAsync();
        add.ChooseKindCommand.Execute("DoctorVisit");
        add.Name = "Dermatologist";
        add.ChooseWhenCommand.Execute("Booked");
        add.Timing.Date = new DateTime(2026, 10, 1);
        await add.SaveCommand.ExecuteAsync(null);
        var vm = _app.CreateWeek();

        await vm.LoadAsync();

        Assert.Equal("Dermatologist", vm.NextUpTitle);
        Assert.False(vm.HasNextUpWith);
    }

    private PlanViewModel NewPlan() =>
        new(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);

    private async Task LinkDoctorAndClinicAsync()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync(
        [
            new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1"),
            new ContactToLink(CareRole.ObGyn, "Papadaki Clinic", "+30 210 000 2210", "Reception", "c2"),
        ]);
    }
}
