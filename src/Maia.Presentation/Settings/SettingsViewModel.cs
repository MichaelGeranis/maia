using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Plans;
using Maia.Presentation.Care;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Results;
using Maia.Presentation.Setup;

namespace Maia.Presentation.Settings;

/// <summary>
/// Settings (the last tab of the bottom bar): her due date, which she can change after setup (a scan may
/// move it); her care team (her OB-GYN and other doctors, linked from her contacts); the language she reads Maia in; her reminders on this phone; an encrypted backup she
/// can keep (ADR 0005); locking Maia with the phone's screen lock; deleting everything Maia keeps on this phone, which
/// asks once more in place; and About Maia, with the privacy notice and licences. Each card with its own logic has its
/// own view model.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly PlanService _plans;
    private readonly ILanguageSwitcher _languages;
    private readonly IResultViewer _viewer;
    private readonly SetupSession _setup;
    private readonly IBackupFiles _backupFiles;
    private readonly IClock _clock;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private readonly IBackupHistory _backupHistory;

    public SettingsViewModel(
        PlanService plans,
        ILanguageSwitcher languages,
        IResultViewer viewer,
        SetupSession setup,
        IBackupFiles backupFiles,
        IClock clock,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors,
        IBackupHistory backupHistory,
        PhoneRemindersSetting reminders,
        DueDateSettings dueDate,
        AppLockSettings appLock,
        DiagnosticsViewModel diagnostics,
        AnimationSetting animations,
        TeamViewModel team)
    {
        _plans = plans;
        _languages = languages;
        _viewer = viewer;
        _setup = setup;
        _backupFiles = backupFiles;
        _clock = clock;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
        _backupHistory = backupHistory;
        Reminders = reminders;
        DueDate = dueDate;
        Lock = appLock;
        Diagnostics = diagnostics;
        Animations = animations;
        Team = team;
        LastBackupText = LastBackup();
    }

    /// <summary>"Animations": on by default; off, Maia's screens keep still.</summary>
    public AnimationSetting Animations { get; }

    /// <summary>"Care team": her OB-GYN and her other doctors, linked from her contacts, to call.</summary>
    public TeamViewModel Team { get; }

    /// <summary>"Your pregnancy": her due date, and changing it.</summary>
    public DueDateSettings DueDate { get; }

    /// <summary>"Reminders": Maia's notifications on this phone, when they come, and a discreet lock screen.</summary>
    public PhoneRemindersSetting Reminders { get; }

    /// <summary>"Lock Maia with your screen lock".</summary>
    public AppLockSettings Lock { get; }

    /// <summary>"Share diagnostics": one tap hands the problem log to the share sheet.</summary>
    public DiagnosticsViewModel Diagnostics { get; }

    /// <summary>
    /// Each time Settings shows: her current due date, her reminder choices and what the phone allows, whether the phone has a screen lock now, and when her last backup was (none, once
    /// everything was deleted and set up again).
    /// </summary>
    public async Task LoadAsync()
    {
        LastBackupText = LastBackup();
        Lock.Refresh();
        await DueDate.LoadAsync();
        await Reminders.LoadAsync();
    }

    public bool IsEnglish { get; } = Text.Language == Text.English;

    public bool IsGreek { get; } = Text.Language == Text.Greek;

    /// <summary>Whether the "delete everything?" card is showing.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(ChooseLanguageCommand), nameof(CreateBackupCommand))]
    public partial bool IsBusy { get; private set; }

    private bool CanChange() => !IsBusy;

    /// <summary>Shows Maia, her checks and her reminders in <paramref name="language"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task ChooseLanguageAsync(string language)
    {
        if (language == Text.Language || language is not (Text.English or Text.Greek))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var error = await PlanChange.RunAsync(() => _plans.ChangeLanguageAsync(language), "settings.language", _errors);
            if (error is not null)
            {
                _notifier.ShowNotice(error);
                return;
            }

            await _languages.SwitchAsync(language);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenAboutAsync() => _navigator.GoToAboutAsync();

    /// <summary>Whether the backup password form is showing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartBackup))]
    public partial bool IsBackupFormOpen { get; private set; }

    /// <summary>
    /// After the share sheet: whether she kept the file. The phone doesn't tell Maia whether she saved it or just closed
    /// the sheet, and Maia's own copy is cleared from its cache, so a backup counts only once she says it is saved.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartBackup))]
    public partial bool IsAskingIfBackupKept { get; private set; }

    /// <summary>Whether "Create a backup" shows: not while the form or the question is open.</summary>
    public bool CanStartBackup => !IsBackupFormOpen && !IsAskingIfBackupKept;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackupHint), nameof(IsBackupHintAttention), nameof(BackupStrength))]
    [NotifyCanExecuteChangedFor(nameof(CreateBackupCommand))]
    public partial string BackupPassword { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackupHint))]
    [NotifyCanExecuteChangedFor(nameof(CreateBackupCommand))]
    public partial string BackupPasswordAgain { get; set; } = string.Empty;

    private BackupPasswordIssue BackupIssue => BackupPasswordRules.Check(BackupPassword);

    /// <summary>
    /// What the password still needs (why an easy one is easy to guess, calmly), or the reminder that it can't be
    /// recovered. Suggests a few words she'll remember rather than rules about capitals and symbols.
    /// </summary>
    public string BackupHint => BackupIssue switch
    {
        BackupPasswordIssue.TooShort => Text.Format("Backup_PasswordShort", BackupPasswordRules.MinLength),
        BackupPasswordIssue.Repeated => Text.Get("Backup_PasswordRepeated"),
        BackupPasswordIssue.Sequence => Text.Get("Backup_PasswordSequence"),
        BackupPasswordIssue.DigitsOnly => Text.Format("Backup_PasswordDigits", BackupPasswordRules.MinDigitsOnly),
        BackupPasswordIssue.Common => Text.Get("Backup_PasswordCommon"),
        _ when BackupPasswordAgain.Length > 0 && BackupPasswordAgain != BackupPassword => Text.Get("Backup_PasswordMismatch"),
        _ => Text.Get("Backup_PasswordKeep"),
    };

    /// <summary>The hint says why this password is easy to guess: shown in Sand, never red.</summary>
    public bool IsBackupHintAttention => BackupIssue is not (BackupPasswordIssue.None or BackupPasswordIssue.TooShort);

    /// <summary>"Good password." or "Strong password." once it is good enough; null before.</summary>
    public string? BackupStrength => BackupIssue != BackupPasswordIssue.None
        ? null
        : Text.Get(BackupPasswordRules.IsStrong(BackupPassword) ? "Backup_PasswordStrong" : "Backup_PasswordGood");

    /// <summary>"Last backup: Thu 1 Oct", or that she has not made one yet.</summary>
    [ObservableProperty]
    public partial string LastBackupText { get; private set; }

    /// <summary>After a backup that had to leave results out: which ones, by name (shown to her, never logged).</summary>
    [ObservableProperty]
    public partial string? BackupNotice { get; private set; }

    [RelayCommand]
    private void OpenBackupForm()
    {
        BackupNotice = null;
        IsAskingIfBackupKept = false;
        BackupPassword = string.Empty;
        BackupPasswordAgain = string.Empty;
        IsBackupFormOpen = true;
    }

    [RelayCommand]
    private void CloseBackupForm()
    {
        BackupPassword = string.Empty;
        BackupPasswordAgain = string.Empty;
        IsBackupFormOpen = false;
    }

    private bool CanCreateBackup() =>
        !IsBusy && BackupIssue == BackupPasswordIssue.None && BackupPasswordAgain == BackupPassword;

    /// <summary>
    /// While the backup is being written: it takes a few seconds before the share sheet opens, so the form says it is
    /// working instead of only greying its button.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCreatingBackup { get; private set; }

    /// <summary>
    /// Writes the encrypted backup and hands it to the phone's share sheet (save to Files, email, Drive…), then asks
    /// whether she kept it: the share sheet can close without the file going anywhere.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateBackup))]
    private async Task CreateBackupAsync()
    {
        IsBusy = true;
        IsCreatingBackup = true;
        try
        {
            var password = BackupPassword;
            var fileName = string.Create(CultureInfo.InvariantCulture, $"Maia-backup-{_clock.Today:yyyy-MM-dd}.maia");
            IReadOnlyList<ResultAttachment> leftOut = [];
            var error = await PlanChange.RunAsync(
                () => _backupFiles.ShareAsync(fileName, async stream => leftOut = await _plans.ExportBackupAsync(stream, password)),
                "settings.backup",
                _errors);
            if (error is not null)
            {
                _notifier.ShowNotice(error);
                return;
            }

            CloseBackupForm();
            BackupNotice = leftOut.Count switch
            {
                0 => null,
                1 => Text.Format("Backup_LeftOutOne", leftOut[0].DisplayName),
                _ => Text.Format("Backup_LeftOut", leftOut.Count, string.Join(", ", leftOut.Select(r => r.DisplayName))),
            };
            IsAskingIfBackupKept = true;
        }
        finally
        {
            IsCreatingBackup = false;
            IsBusy = false;
        }
    }

    /// <summary>She saved the file: it counts as her last backup, and Your week stops suggesting one for a month.</summary>
    [RelayCommand]
    private void BackupKept()
    {
        IsAskingIfBackupKept = false;
        _backupHistory.BackupMade(_clock.UtcNow);
        LastBackupText = LastBackup();
        _notifier.Show(Text.Get("Toast_BackupKept"));
    }

    /// <summary>She didn't save it: nothing is counted, and she can make a backup again whenever it suits her.</summary>
    [RelayCommand]
    private void BackupNotKept() => IsAskingIfBackupKept = false;

    private string LastBackup() => _backupHistory.LastBackup is { } at
        ? Text.Format("Backup_Last", DisplayFormat.Date(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, _clock.TimeZone).DateTime), _clock.Today))
        : Text.Get("Backup_None");

    [RelayCommand]
    private void AskDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void KeepData() => IsConfirmingDelete = false;

    /// <summary>
    /// Deletes her plan and results and cancels every reminder, then starts over at setup.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task DeleteAsync()
    {
        IsBusy = true;
        try
        {
            var error = await PlanChange.RunAsync(() => _plans.DeleteEverythingAsync(), "settings.delete", _errors);
            if (error is not null)
            {
                _notifier.ShowNotice(error);
                return;
            }

            _viewer.ClearCopies();
            _backupHistory.Forget();
            _setup.Reset();
            IsConfirmingDelete = false;
            await _navigator.GoToSetupAsync();

            // After navigating, so setup shows it: raised on the way, Settings (still on screen) took it and closed.
            _notifier.Show(Text.Get("Toast_Deleted"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// She left Settings for another tab or a screen it opened. Settings is a tab, so it stays while she is away, and
    /// what she left open in place closes as if she had cancelled: the backup form forgets the password, a "did you
    /// keep it?" she didn't answer doesn't count as a backup (and its note on left-out results goes with it), "delete
    /// everything?" is no longer asked, a due date she was changing stays as it was, and Care team folds again.
    /// </summary>
    public void Leave()
    {
        CloseBackupForm();
        Team.Fold();
        BackupNotice = null;
        IsAskingIfBackupKept = false;
        IsConfirmingDelete = false;
        DueDate.CancelCommand.Execute(null);
    }
}
