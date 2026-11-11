using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Settings;

namespace Maia.Presentation.Setup;

/// <summary>
/// Restore a backup (from setup, step 1): she picks the backup file, types its password, and her plan and results
/// come back on this phone. A wrong password or a damaged file is said plainly and can simply be tried again. The file
/// she restored from is a copy of her plan as it now stands, kept somewhere off this phone, so it counts as her last
/// backup: Your week does not ask her to make one on a phone she has just restored.
/// </summary>
public sealed partial class RestoreViewModel : ObservableObject
{
    private readonly PlanService _plans;
    private readonly IBackupFiles _files;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private readonly IBackupHistory _backupHistory;
    private readonly IClock _clock;

    public RestoreViewModel(
        PlanService plans,
        IBackupFiles files,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors,
        IBackupHistory backupHistory,
        IClock clock)
    {
        _plans = plans;
        _files = files;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
        _backupHistory = backupHistory;
        _clock = clock;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName), nameof(HasFile))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    public partial BackupFile? File { get; private set; }

    public bool HasFile => File is not null;

    /// <summary>The picked file's name, or "No file chosen yet".</summary>
    public string FileName => File?.Name ?? Text.Get("Restore_NoFile");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>The phone's file picker. One that fails (a cloud provider offline, say) is said calmly and can be tried again.</summary>
    [RelayCommand]
    private async Task ChooseFileAsync()
    {
        BackupFile? picked = null;
        var failure = await PlanChange.RunAsync(async () => picked = await _files.PickAsync(), "setup.restore_pick", _errors);
        if (failure is not null)
        {
            ErrorMessage = failure;
            return;
        }

        if (picked is not null)
        {
            File = picked;
            ErrorMessage = null;
        }
    }

    private bool CanRestore() => File is not null && Password.Length > 0 && !IsBusy;

    /// <summary>
    /// Restores her plan from the file. Once it is restored she goes on to Your week whatever happens after: the file
    /// failing to close, or the day of the backup not being noted, is reported and never leaves her on this screen as if
    /// it had failed (where "Restore" again would be refused, as the phone now has a plan).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        var file = File!;
        var password = Password;
        string? unreadable = null;
        var restored = false;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var failure = await PlanChange.RunAsync(
                async () =>
                {
                    try
                    {
                        await using var stream = await file.OpenAsync();
                        await _plans.RestoreBackupAsync(stream, password);
                        restored = true;
                    }
                    catch (BackupUnreadableException ex)
                    {
                        // Expected (a typo, the wrong file): said plainly, not reported.
                        unreadable = BackupMessages.For(ex);
                    }
                },
                "setup.restore",
                _errors);

            if (!restored)
            {
                ErrorMessage = unreadable ?? failure;
                return;
            }

            // Her plan now matches the file she restored from.
            await PlanChange.RunAsync(
                () =>
                {
                    _backupHistory.BackupMade(_clock.UtcNow);
                    return Task.CompletedTask;
                },
                "setup.restore_noted",
                _errors);
            Password = string.Empty;
            await _navigator.GoToMainAsync();

            // After navigating, so Your week shows it rather than Restore as it closes.
            _notifier.Show(Text.Get("Toast_Restored"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();
}
