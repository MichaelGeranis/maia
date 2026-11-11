using Maia.Application.Common;
using Maia.Presentation.Settings;
using Maia.Presentation.Setup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class RestoreTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private RestoreViewModel Create() => new(_app.Plans, _app.BackupFiles, _app.Navigator, _app.Notifier, _app.Errors, _app.BackupHistory, _app.Clock);

    [Fact]
    public void Setup_offers_a_way_to_restore()
    {
        var setup = new SetupViewModel(_app.Clock, _app.Setup, _app.Navigator, new RecordingLanguageSwitcher());

        setup.RestoreCommand.Execute(null);

        Assert.Equal(new[] { "restore" }, _app.Navigator.Visited);
    }

    [Fact]
    public void Restoring_needs_a_file_and_its_password()
    {
        var vm = Create();

        Assert.False(vm.HasFile);
        Assert.Equal("No file chosen yet", vm.FileName);
        Assert.False(vm.RestoreCommand.CanExecute(null));
    }

    [Fact]
    public async Task Restoring_a_backup_brings_her_plan_back()
    {
        await PickBackupOfAPlanAsync("sea-breeze-17");
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-17";

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Equal("Maia-backup-2026-09-30.maia", vm.FileName);
        Assert.NotNull(_app.Repository.Stored);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Welcome back. Your journey is restored.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task The_welcome_back_shows_on_your_week_not_on_restore_as_it_closes()
    {
        await PickBackupOfAPlanAsync("sea-breeze-17");
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-17";
        var toastsOnTheWay = -1;
        _app.Navigator.WhenVisiting = _ => toastsOnTheWay = _app.Notifier.Shown.Count;

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(0, toastsOnTheWay);
        Assert.Equal("Welcome back. Your journey is restored.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_wrong_password_is_said_plainly_and_can_be_tried_again()
    {
        await PickBackupOfAPlanAsync("sea-breeze-17");
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-18";

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Equal("That password doesn't open this backup. Check it and try again.", vm.ErrorMessage);
        Assert.Null(_app.Repository.Stored);
        Assert.Empty(_app.Errors.Reports);
        Assert.Empty(_app.Navigator.Visited);
        Assert.Null(_app.BackupHistory.LastBackup);

        vm.Password = "sea-breeze-17";
        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(_app.Repository.Stored);
    }

    [Fact]
    public async Task A_restored_plan_counts_as_backed_up_so_your_week_does_not_ask_for_a_backup_a_week_later()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Pack the hospital bag", week: 30);
        await PickBackupOfThePlanAsync("sea-breeze-17");
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-17";

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(_app.Clock.UtcNow, _app.BackupHistory.LastBackup);
        _app.Clock.SetToday(TestData.Today.AddDays(7));
        var week = _app.CreateWeek();
        await week.LoadAsync();
        Assert.False(week.ShowBackupReminder);
    }

    [Fact]
    public async Task Restoring_replaces_a_plan_this_phone_cannot_open()
    {
        await PickBackupOfAPlanAsync("sea-breeze-17");
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Not in the backup", week: 30);
        _app.Repository.Unreadable = PlanUnreadableException.KeyUnavailable(new InvalidOperationException("keystore"));
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-17";

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Empty(_app.Repository.Stored!.Tasks);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Welcome back. Your journey is restored.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Once_her_plan_is_restored_she_goes_on_to_your_week_even_if_the_file_will_not_close()
    {
        await PickBackupOfAPlanAsync("sea-breeze-17");
        var bytes = await ReadPickedAsync();
        _app.BackupFiles.NextPick = new BackupFile("Maia-backup-2026-09-30.maia", () => Task.FromResult<Stream>(new FailsToClose(bytes)));
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-17";

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(_app.Repository.Stored);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Welcome back. Your journey is restored.", _app.Notifier.Shown.Single());
        Assert.Equal("setup.restore", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task Once_her_plan_is_restored_she_goes_on_to_your_week_even_if_the_backup_day_cannot_be_noted()
    {
        await PickBackupOfAPlanAsync("sea-breeze-17");
        _app.BackupHistory.FailBackupMade = new InvalidOperationException("preferences");
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-17";

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Welcome back. Your journey is restored.", _app.Notifier.Shown.Single());
        Assert.Equal("setup.restore_noted", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_backup_with_hundreds_of_tasks_is_restored_and_your_week_shows_a_short_card()
    {
        await _app.CreatePlanAsync();
        var many = Enumerable.Range(0, 1000).Select(i => ((DateOnly?)TestData.Today.AddDays((i % 60) - 20), (TimeOnly?)new TimeOnly(9, 0), i % 3 == 0));
        await _app.Repository.SaveAsync(_app.Repository.Stored!.WithTasks(many), CancellationToken.None);
        await PickBackupOfThePlanAsync("sea-breeze-17");
        var vm = Create();
        await vm.ChooseFileCommand.ExecuteAsync(null);
        vm.Password = "sea-breeze-17";

        await vm.RestoreCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal(1000, _app.Repository.Stored!.Tasks.Count);
        var week = _app.CreateWeek();
        await week.LoadAsync();
        Assert.True(week.IsLoaded);
        Assert.Equal(Week.WeekViewModel.CollapsedRows, week.Tasks.Count);
    }

    /// <summary>The bytes of the backup queued to be picked.</summary>
    private async Task<byte[]> ReadPickedAsync()
    {
        await using var stream = await _app.BackupFiles.NextPick!.OpenAsync();
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    private async Task PickBackupOfAPlanAsync(string password)
    {
        await _app.CreatePlanAsync();
        await PickBackupOfThePlanAsync(password);
    }

    /// <summary>Backs up the plan on this phone, deletes it, and queues the file to be picked.</summary>
    private async Task PickBackupOfThePlanAsync(string password)
    {
        using var file = new MemoryStream();
        await _app.Plans.ExportBackupAsync(file, password);
        await _app.Plans.DeleteEverythingAsync();
        var bytes = file.ToArray();
        _app.BackupFiles.NextPick = new BackupFile("Maia-backup-2026-09-30.maia", () => Task.FromResult<Stream>(new MemoryStream(bytes)));
    }
}

/// <summary>A file from another app that reads to the end but fails as it is closed, as a cloud provider can.</summary>
internal sealed class FailsToClose(byte[] content) : MemoryStream(content)
{
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        throw new IOException("provider gone");
    }
}
