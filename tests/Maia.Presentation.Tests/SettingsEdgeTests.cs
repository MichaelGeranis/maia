using Maia.Application.Plans;
using Maia.Presentation.Settings;
using Maia.Presentation.Setup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Settings when the phone or the plan lets her down, and the parts of its cards that only show sometimes.</summary>
public sealed class SettingsEdgeTests : IDisposable
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
    public async Task Reminders_that_cannot_ask_for_notifications_switch_back_off_and_say_so()
    {
        await _app.CreatePlanAsync();
        var setting = new PhoneRemindersSetting(_app.Reminders, new BrokenNotificationAccess(), _app.Errors);
        await setting.LoadAsync();

        setting.IsOn = true;
        await setting.WhenSavedAsync();

        Assert.False(setting.IsOn);
        Assert.Equal(WentWrong, setting.Notice);
        Assert.Equal("settings.reminders", Assert.Single(_app.Errors.Reports).Operation);
        Assert.False(_app.ReminderSettings.IsOn);
    }

    [Fact]
    public async Task A_problem_log_the_phone_hands_back_empty_handed_is_said_calmly()
    {
        var vm = new DiagnosticsViewModel(new NoReportDiagnostics(), _app.Notifier, _app.Errors);

        await vm.ShareCommand.ExecuteAsync(null);

        Assert.Equal(WentWrong, _app.Notifier.Notices.Single());
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Without_a_plan_the_due_date_editor_does_not_open()
    {
        var card = _app.CreateSettings().DueDate;
        await card.LoadAsync();

        Assert.False(card.EditCommand.CanExecute(null));
        card.EditCommand.Execute(null);

        Assert.False(card.IsEditing);
    }

    [Fact]
    public async Task While_a_new_due_date_saves_the_editor_cannot_open_again()
    {
        await _app.CreatePlanAsync();
        var card = _app.CreateSettings().DueDate;
        await card.LoadAsync();
        card.EditCommand.Execute(null);
        card.Entry.DueDate = TestData.DueDate.AddDays(9).ToDateTime(TimeOnly.MinValue);
        var hold = new TaskCompletionSource();
        _app.Repository.BeforeLoad = () => hold.Task;

        var saving = card.SaveCommand.ExecuteAsync(null);
        Assert.False(card.EditCommand.CanExecute(null));
        _app.Repository.BeforeLoad = null;
        hold.SetResult();
        await saving;

        Assert.True(card.EditCommand.CanExecute(null));
        Assert.Equal(TestData.DueDate.AddDays(9), _app.Repository.Stored!.Dating.DueDate);
    }

    [Fact]
    public async Task A_due_date_saved_after_her_journey_was_deleted_elsewhere_says_there_is_none()
    {
        await _app.CreatePlanAsync();
        var card = _app.CreateSettings().DueDate;
        await card.LoadAsync();
        card.EditCommand.Execute(null);
        await _app.Repository.DeleteAsync(CancellationToken.None);
        await card.LoadAsync();

        Assert.True(card.SaveCommand.CanExecute(null));
        await card.SaveCommand.ExecuteAsync(null);

        Assert.Equal("There's no journey on this phone yet. Set one up first.", _app.Notifier.Notices.Single());
    }

    [Fact]
    public void The_date_field_says_which_date_it_wants_and_how_to_give_the_other()
    {
        var entry = new DueDateEntry(_app.Clock, TestData.DueDate);
        Assert.Equal((true, "When is your baby due?", "Not sure? Use your last period instead"), (entry.UseDueDate, entry.DateLabel, entry.DateModeLink));

        entry.ToggleDateModeCommand.Execute(null);

        Assert.Equal((false, "First day of your last period", "I know my due date"), (entry.UseDueDate, entry.DateLabel, entry.DateModeLink));
    }

    [Fact]
    public void A_due_date_outside_what_the_picker_offers_starts_at_its_nearest_end()
    {
        var entry = new DueDateEntry(_app.Clock, TestData.Today.AddYears(3));
        Assert.Equal(entry.DueMaximumDate, entry.DueDate);

        entry.Reset(TestData.Today.AddYears(-3));
        Assert.Equal(entry.DueMinimumDate, entry.DueDate);
    }

    [Fact]
    public async Task Create_a_backup_shows_until_she_opens_the_form_or_is_asked_if_she_kept_it()
    {
        await _app.CreatePlanAsync();
        var vm = _app.CreateSettings();
        Assert.True(vm.CanStartBackup);
        Assert.True(vm.DeleteCommand.CanExecute(null));

        vm.OpenBackupFormCommand.Execute(null);
        Assert.False(vm.CanStartBackup);
        vm.BackupPassword = vm.BackupPasswordAgain = "olive tree by the sea";
        await vm.CreateBackupCommand.ExecuteAsync(null);

        Assert.Equal((false, true, false), (vm.IsBackupFormOpen, vm.IsAskingIfBackupKept, vm.CanStartBackup));
    }

    [Fact]
    public async Task A_backup_that_leaves_out_several_results_names_them_all()
    {
        await _app.CreatePlanAsync();
        var first = await _app.Plans.AttachFileAsync(new FileToAttach("Blood test.pdf", "application/pdf", new byte[1024]));
        var second = await _app.Plans.AttachFileAsync(new FileToAttach("Scan.pdf", "application/pdf", new byte[1024]));
        foreach (var result in _app.Repository.Stored!.Results)
        {
            _app.Attachments.Damaged.Add(result.BlobId!);
        }

        var vm = _app.CreateSettings();
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "olive tree by the sea";
        await vm.CreateBackupCommand.ExecuteAsync(null);

        var names = _app.Repository.Stored!.Results.Where(r => r.Id == first || r.Id == second).Select(r => r.DisplayName);
        Assert.Equal(
            $"Your backup is ready, but 2 results couldn't go in because their files no longer open on this phone: {string.Join(", ", names)}.",
            vm.BackupNotice);
    }

    [Fact]
    public async Task Deleting_everything_that_fails_says_so_and_stays_in_settings()
    {
        await _app.CreatePlanAsync();
        _app.DataKey.WhenForgotten = () => throw new IOException("keystore busy");
        var vm = _app.CreateSettings();
        vm.AskDeleteCommand.Execute(null);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(WentWrong, _app.Notifier.Notices.Single());
        Assert.True(vm.IsConfirmingDelete);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Equal("settings.delete", _app.Errors.Reports.Last().Operation);
    }

    /// <summary>A phone whose notification permission can't be asked for just now.</summary>
    private sealed class BrokenNotificationAccess : INotificationAccess
    {
        public bool IsSupported => true;

        public Task<bool> IsGrantedAsync() => Task.FromResult(false);

        public Task<bool> RequestAsync() => throw new InvalidOperationException("no activity to ask from");

        public void OpenAppSettings()
        {
        }
    }

    /// <summary>A problem log the phone hands back as nothing at all, against its word.</summary>
    private sealed class NoReportDiagnostics : IDiagnostics
    {
        public Task<DiagnosticsReport> ReadAsync() => Task.FromResult<DiagnosticsReport>(null!);

        public Task ShareAsync(DiagnosticsReport report) => Task.CompletedTask;
    }
}
