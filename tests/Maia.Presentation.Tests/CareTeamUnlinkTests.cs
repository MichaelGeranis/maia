using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Presentation.Care;

namespace Maia.Presentation.Tests;

/// <summary>Care team: the question before unlinking, when what it asks about is no longer there.</summary>
public sealed class CareTeamUnlinkTests : IDisposable
{
    private static readonly PickedContact Doctor = new("Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1");
    private static readonly PickedContact Dentist = new("Dr. Maria Kosta", "+30 690 000 1234", "Mobile", "c4");

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task The_unlink_question_names_the_contact_on_the_card_or_row_that_asks_it()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        _app.Contacts.Picks.Enqueue(Dentist);
        await vm.LinkCommand.ExecuteAsync(null);
        var dentist = vm.DoctorTiles.Single(t => t.Role == CareRole.Dentist);
        _app.Chooser.Taps("Dentist");
        await vm.LinkDoctorCommand.ExecuteAsync(null);
        var row = vm.DoctorRows.Single(r => r.Tiles.Contains(dentist));

        Assert.Equal((string.Empty, string.Empty), (vm.UnlinkQuestion, row.UnlinkQuestion));

        vm.AskUnlinkCommand.Execute(dentist.Contacts[0]);
        Assert.Equal((string.Empty, "Unlink Dr. Maria Kosta? Your bookings stay in your journey."), (vm.UnlinkQuestion, row.UnlinkQuestion));

        vm.AskUnlinkCommand.Execute(vm.ObGyns[0]);
        Assert.Equal(("Unlink Dr. Eleni Papadaki? Your bookings stay in your journey.", string.Empty), (vm.UnlinkQuestion, row.UnlinkQuestion));
    }

    [Fact]
    public async Task A_question_about_a_contact_unlinked_elsewhere_closes_when_care_team_reloads()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await vm.LinkCommand.ExecuteAsync(null);
        vm.AskUnlinkCommand.Execute(vm.ObGyns[0]);

        await _app.Plans.UnlinkContactAsync(_app.Repository.Stored!.CareTeam.Contacts.Single().Id);
        await vm.LoadAsync();

        Assert.False(vm.IsConfirmingUnlink);
        Assert.Empty(vm.ObGyns);
    }

    [Fact]
    public async Task A_question_about_a_contact_still_linked_stays_open_when_care_team_reloads()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await vm.LinkCommand.ExecuteAsync(null);
        vm.AskUnlinkCommand.Execute(vm.ObGyns[0]);

        await vm.LoadAsync();

        Assert.True(vm.IsConfirmingUnlink);
        Assert.Equal("Dr. Eleni Papadaki", vm.PendingUnlink!.Name);
    }

    [Fact]
    public async Task Unlink_with_nothing_asked_about_unlinks_nothing()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await vm.LinkCommand.ExecuteAsync(null);
        var shown = _app.Notifier.Shown.Count;

        await vm.UnlinkCommand.ExecuteAsync(null);

        Assert.Single(vm.ObGyns);
        Assert.Single(_app.Repository.Stored!.CareTeam.Contacts);
        Assert.Equal(shown, _app.Notifier.Shown.Count);
    }

    [Fact]
    public async Task Unlink_of_a_contact_that_is_not_on_care_team_unlinks_nothing()
    {
        var vm = await OpenAsync();
        _app.Contacts.Picks.Enqueue(Doctor);
        await vm.LinkCommand.ExecuteAsync(null);
        var shown = _app.Notifier.Shown.Count;

        // A contact just picked has no id yet; one with an id Care team does not show is no longer hers.
        vm.AskUnlinkCommand.Execute(ContactItem.From(Dentist, 0));
        await vm.UnlinkCommand.ExecuteAsync(null);
        vm.AskUnlinkCommand.Execute(vm.ObGyns[0] with { Id = new CareContactId(Guid.NewGuid()) });
        await vm.UnlinkCommand.ExecuteAsync(null);

        Assert.False(vm.IsConfirmingUnlink);
        Assert.All(vm.DoctorRows, r => Assert.False(r.IsConfirmingUnlink));
        Assert.Single(_app.Repository.Stored!.CareTeam.Contacts);
        Assert.Equal(shown, _app.Notifier.Shown.Count);
    }

    [Theory]
    [InlineData("123", "·")]
    [InlineData("Dr.", "·")]
    public void A_name_with_no_letters_to_start_a_word_gets_a_dot(string name, string initials) =>
        Assert.Equal(initials, ContactItem.InitialsOf(name));

    [Theory]
    [InlineData(null, "112", "••• 112")]
    [InlineData("Home", "4567", "Home · ••• 4567")]
    [InlineData(" ", "+30 690 000 4567", "••• 4567")]
    public void A_short_number_shows_whole_behind_the_dots(string? label, string number, string meta) =>
        Assert.Equal(meta, ContactItem.MetaOf(label, number));

    private async Task<TeamViewModel> OpenAsync()
    {
        await _app.CreatePlanAsync();
        var vm = new TeamViewModel(_app.Queries, _app.Plans, _app.Contacts, _app.Dialer, _app.Chooser, _app.Notifier, _app.Errors);
        await vm.LoadAsync();
        return vm;
    }
}
