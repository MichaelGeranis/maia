using Maia.Application.Abstractions;
using Maia.Presentation.Care;
using Maia.Presentation.Settings;
using Maia.Presentation.Setup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Setup and restore at their edges: a file that won't open, a step reached without a due date, drafts that fail.</summary>
public sealed class SetupEdgeTests : IDisposable
{
    private const string WentWrong = "Something went wrong. Please try again.";

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Restore_is_ready_once_she_has_a_file_and_a_password_and_not_while_it_restores()
    {
        var vm = CreateRestore();
        var opening = new TaskCompletionSource<Stream>();
        _app.BackupFiles.NextPick = new BackupFile("Maia-backup.maia", () => opening.Task);
        await vm.ChooseFileCommand.ExecuteAsync(null);
        Assert.False(vm.RestoreCommand.CanExecute(null));

        vm.Password = "sea-breeze-17";
        Assert.True(vm.RestoreCommand.CanExecute(null));

        var restoring = vm.RestoreCommand.ExecuteAsync(null);
        Assert.False(vm.RestoreCommand.CanExecute(null));
        opening.SetException(new IOException("provider offline"));
        await restoring;

        Assert.Equal(WentWrong, vm.ErrorMessage);
        Assert.Equal("setup.restore", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Null(_app.Repository.Stored);
    }

    [Fact]
    public async Task Back_from_restore_and_from_her_doctor_goes_back()
    {
        await CreateRestore().BackCommand.ExecuteAsync(null);
        await _app.CreateSetupCare().BackCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "back", "back" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Her_doctor_picked_as_maia_closed_without_a_number_is_explained()
    {
        _app.Setup.DueDate = TestData.DueDate;
        _app.Contacts.Interrupted = new PickedContact("Nikos", " ", null, "c3");
        var vm = _app.CreateSetupCare();

        await vm.LoadAsync();

        Assert.Equal("That contact has no phone number. Pick one that does.", vm.Notice);
        Assert.Empty(_app.Setup.ObGyns);
        Assert.True(vm.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task Start_without_a_due_date_makes_no_plan()
    {
        var vm = _app.CreateSetupCare();

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Null(_app.Repository.Stored);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public void Back_on_step_one_shows_the_due_date_she_chose()
    {
        _app.Setup.DueDate = new DateOnly(2027, 2, 3);

        var setup = new SetupViewModel(_app.Clock, _app.Setup, _app.Navigator, new RecordingLanguageSwitcher());

        Assert.Equal(new DateTime(2027, 2, 3), setup.Entry.DueDate);
    }

    [Fact]
    public async Task A_session_kept_in_memory_only_has_nothing_to_bring_back_or_delete()
    {
        var session = new SetupSession { DueDate = TestData.DueDate };

        await session.SaveAsync(SetupStep.Plan);
        Assert.Null(await session.RestoreAsync());
        await session.ClearAsync();

        Assert.Null(session.DueDate);
    }

    [Fact]
    public async Task A_draft_kept_before_she_chose_a_due_date_brings_her_back_to_the_first_step()
    {
        _app.Setup.RemindMe = true;
        await _app.Setup.SaveAsync(SetupStep.Care);

        var restarted = new SetupSession(_app.Drafts, _app.Errors);

        Assert.Equal(SetupStep.DueDate, await restarted.RestoreAsync());
        Assert.True(restarted.RemindMe);
    }

    [Fact]
    public async Task A_draft_that_cannot_be_read_starts_setup_afresh_and_is_reported()
    {
        _app.Setup.DueDate = TestData.DueDate;
        await _app.Setup.SaveAsync(SetupStep.Plan);
        _app.Drafts.FailReads = new IOException("unreadable");
        var restarted = new SetupSession(_app.Drafts, _app.Errors) { DueDate = new DateOnly(2027, 3, 1) };

        Assert.Null(await restarted.RestoreAsync());

        Assert.Null(restarted.DueDate);
        Assert.Equal("setup.draft_read", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_draft_that_cannot_be_deleted_is_reported_and_her_choices_are_still_forgotten()
    {
        var drafts = new UndeletableDrafts();
        var session = new SetupSession(drafts, _app.Errors) { DueDate = TestData.DueDate };
        await session.SaveAsync(SetupStep.Plan);

        await session.ClearAsync();

        Assert.Null(session.DueDate);
        Assert.Equal("setup.draft_delete", Assert.Single(_app.Errors.Reports).Operation);
    }

    private RestoreViewModel CreateRestore() =>
        new(_app.Plans, _app.BackupFiles, _app.Navigator, _app.Notifier, _app.Errors, _app.BackupHistory, _app.Clock);

    /// <summary>Drafts kept on a phone that refuses to delete them just now.</summary>
    private sealed class UndeletableDrafts : IDraftStore
    {
        private readonly InMemoryDraftStore _inner = new();

        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => _inner.ReadAsync(name, cancellationToken);

        public Task WriteAsync(string name, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
            _inner.WriteAsync(name, content, cancellationToken);

        public Task DeleteAsync(string name, CancellationToken cancellationToken) => throw new IOException("storage busy");
    }
}
