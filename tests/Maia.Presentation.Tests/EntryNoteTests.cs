using System.Xml.Linq;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Add;
using Maia.Presentation.Plan;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// Her notes on what she adds: written on New, shown when the card is opened on Journey as a check's note from a
/// typical journey is, and changed in Edit. With none, nothing shows.
/// </summary>
public sealed class EntryNoteTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task A_note_written_on_new_is_saved_with_the_entry_and_new_starts_afresh()
    {
        var vm = await OpenNewAsync();
        vm.ChooseKindCommand.Execute("Exam");
        vm.Name = "Iron recheck";
        vm.Note = "  Fasting from midnight  ";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Week");

        await vm.SaveCommand.ExecuteAsync(null);

        var entry = _app.Repository.Stored!.Checks.Single(c => c.Origin == CheckOrigin.AddedByParent);
        Assert.Equal(LocalizedText.Plain("Fasting from midnight"), entry.Description);
        Assert.Equal(string.Empty, vm.Note);
    }

    [Fact]
    public async Task An_entry_added_without_a_note_has_none()
    {
        var vm = await OpenNewAsync();
        vm.ChooseKindCommand.Execute("HospitalVisit");
        vm.Name = "Hospital tour";
        vm.ChooseWhenCommand.Execute("NotBookedYet");
        vm.Timing.ChooseModeCommand.Execute("Trimester");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(_app.Repository.Stored!.Checks.Single(c => c.Origin == CheckOrigin.AddedByParent).Description);
    }

    [Fact]
    public async Task The_note_says_how_much_room_is_left_near_its_limit()
    {
        var vm = await OpenNewAsync();

        Assert.Null(vm.NoteLengthHint);
        vm.Note = new string('n', AddEntryViewModel.MaxNoteLength - 5);
        Assert.Equal("5 characters left", vm.NoteLengthHint);
        Assert.Equal(PlannedCheck.MaxNoteLength, AddEntryViewModel.MaxNoteLength);
    }

    [Fact]
    public async Task Her_note_shows_when_the_card_opens_and_an_entry_without_one_shows_none()
    {
        await _app.CreatePlanAsync();
        var scan = await _app.Plans.AddEntryAsync("Extra scan", EntryKind.Exam, EntryWhen.NotBookedInWeek(30), "Bring the referral");
        var tour = await _app.Plans.AddEntryAsync("Hospital tour", EntryKind.HospitalVisit, EntryWhen.NotBookedInWeek(30));

        var vm = await OpenJourneyAsync();

        Assert.Equal(("Bring the referral", true), (Row(vm, scan).Note, Row(vm, scan).HasNote));
        Assert.Equal((string.Empty, false), (Row(vm, tour).Note, Row(vm, tour).HasNote));
    }

    [Fact]
    public async Task Edit_starts_with_its_note_and_saves_the_one_she_writes()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = await OpenJourneyAsync();
        var checkup = Row(vm, TestData.Checkup25);
        checkup.ToggleCommand.Execute(null);

        checkup.StartEditCommand.Execute(null);
        Assert.Equal("Check-up details", checkup.EditNote);
        Assert.Null(checkup.EditNoteLengthHint);

        checkup.EditNote = " Ask about the scan results ";
        await checkup.SaveCommand.ExecuteAsync(null);

        Assert.Equal(LocalizedText.Plain("Ask about the scan results"), _app.Repository.Stored!.GetCheck(TestData.Checkup25).Description);
        Assert.Equal("Ask about the scan results", Row(vm, TestData.Checkup25).Note);
        Assert.Equal("Check-up updated.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Edit_with_its_note_emptied_leaves_none()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Checkup25, new Appointment(new DateOnly(2026, 10, 6), new TimeOnly(10, 30)));
        var vm = await OpenJourneyAsync();
        var checkup = Row(vm, TestData.Checkup25);
        checkup.ToggleCommand.Execute(null);
        checkup.StartEditCommand.Execute(null);

        checkup.EditNote = new string('n', PlannedCheck.MaxNoteLength);
        Assert.Equal("That's all that fits: 500 characters.", checkup.EditNoteLengthHint);
        checkup.EditNote = string.Empty;
        await checkup.SaveCommand.ExecuteAsync(null);

        Assert.Null(_app.Repository.Stored!.GetCheck(TestData.Checkup25).Description);
        Assert.False(Row(vm, TestData.Checkup25).HasNote);
    }

    [Fact]
    public void New_and_edit_both_offer_an_optional_notes_field_she_can_write_lines_in()
    {
        foreach (var page in new[] { "AddEntryPage.xaml", "PlanPage.xaml" })
        {
            var editor = XDocument.Load(Repo.App("Views", page)).Descendants().Single(e => e.Name.LocalName == "Editor");

            Assert.Equal("{loc:Translate Add_Note}", AccessibilityTests.Attr(editor, "SemanticProperties.Description"));
            Assert.Equal("{loc:Translate Add_NotePlaceholder}", AccessibilityTests.Attr(editor, "Placeholder"));
            Assert.Equal("TextChanges", AccessibilityTests.Attr(editor, "AutoSize"));
            Assert.EndsWith("MaxNoteLength}", AccessibilityTests.Attr(editor, "MaxLength"), StringComparison.Ordinal);
        }

        Assert.Contains("EditorHandler.Mapper.AppendToMapping(\"NoUnderline\"", Repo.CodeOf(Repo.App("MauiProgram.cs")), StringComparison.Ordinal);
    }

    private async Task<AddEntryViewModel> OpenNewAsync()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateAdd();
        await vm.LoadAsync();
        return vm;
    }

    private async Task<PlanViewModel> OpenJourneyAsync()
    {
        var vm = new PlanViewModel(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);
        await vm.LoadAsync();
        return vm;
    }

    private static CheckRow Row(PlanViewModel vm, CheckId id) => vm.Rows.OfType<CheckRow>().Single(r => r.Id == id);
}
