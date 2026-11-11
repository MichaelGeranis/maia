using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Results;
using Maia.Presentation.Setup;

namespace Maia.Presentation.Startup;

/// <summary>What the start-up screen shows.</summary>
public enum StartupState
{
    /// <summary>Opening her plan: a quiet spinner, usually for a moment.</summary>
    Opening,

    /// <summary>A plan is saved on this phone but can't be opened: she can restore a backup or start again.</summary>
    CannotOpen,

    /// <summary>A newer Maia saved her plan: updating Maia opens it. Nothing on this screen changes it.</summary>
    NeedsUpdate,

    /// <summary>
    /// Something else stopped her plan opening (storage not ready, a fault in Maia). Her plan may be fine, so she can
    /// only try again: nothing on this screen changes or removes it.
    /// </summary>
    OpenFailed,
}

/// <summary>
/// The first screen: opens her plan if there is one, otherwise setup. A plan in another language than the app is
/// renamed first, so its checks read in the same language as everything else. When a plan is saved on this phone but
/// can't be opened (its key is gone after moving phones, or it is damaged), it says so calmly and offers a way on:
/// restore a backup in its place, or start again, which asks once more in place. A plan saved by a newer Maia is never
/// touched: she is asked to update. Anything else that stops it opening may pass and says nothing about the plan, so
/// she can only try again: Restore and Start again are offered only for a plan known to be unreadable. Only error codes
/// are reported, never anything from the plan.
/// </summary>
public sealed partial class StartupViewModel : ObservableObject
{
    private readonly PlanQueries _queries;
    private readonly PlanService _plans;
    private readonly SetupSession _setup;
    private readonly INavigator _navigator;
    private readonly PlanUpkeep _upkeep;
    private readonly IClock _clock;
    private readonly IErrorReporter _errors;
    private readonly PlanLanguageSync _language;
    private readonly ShareIntake _shares;
    private readonly IResultViewer _viewer;
    private Task? _opening;

    public StartupViewModel(
        PlanQueries queries,
        PlanService plans,
        SetupSession setup,
        INavigator navigator,
        INotifier notifier,
        IClock clock,
        IErrorReporter errors,
        PlanLanguageSync language,
        ShareIntake shares,
        IResultViewer viewer)
    {
        _viewer = viewer;
        _shares = shares;
        _queries = queries;
        _plans = plans;
        _setup = setup;
        _navigator = navigator;
        _upkeep = new PlanUpkeep(plans, setup, notifier, errors);
        _clock = clock;
        _errors = errors;
        _language = language;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpening), nameof(CannotOpen), nameof(NeedsUpdate), nameof(OpenFailed))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand), nameof(AskStartAgainCommand), nameof(StartAgainCommand))]
    public partial StartupState State { get; private set; }

    public bool IsOpening => State == StartupState.Opening;

    public bool CannotOpen => State == StartupState.CannotOpen;

    public bool NeedsUpdate => State == StartupState.NeedsUpdate;

    public bool OpenFailed => State == StartupState.OpenFailed;

    /// <summary>Whether the "start again?" card is showing.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingStartAgain { get; private set; }

    /// <summary>The background tidy of result files started by the last clean <see cref="OpenAsync"/>.</summary>
    internal Task Tidying { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand), nameof(AskStartAgainCommand), nameof(StartAgainCommand), nameof(TryAgainCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// Opens her plan, or setup when there is none. When a plan is saved but can't be opened, stays here and shows why
    /// and what she can do. Once at a time: asked again while her plan is still opening (the start-up screen showing
    /// again), it joins the opening under way rather than loading, tidying and navigating a second time.
    /// </summary>
    /// <param name="openPlan">Where to go once her plan opens, instead of Your week (after a language switch, Settings).</param>
    public Task OpenAsync(Func<Task>? openPlan = null)
    {
        if (_opening is { IsCompleted: false } opening)
        {
            return opening;
        }

        return _opening = OpenOnceAsync(openPlan);
    }

    private async Task OpenOnceAsync(Func<Task>? openPlan)
    {
        State = StartupState.Opening;
        IsConfirmingStartAgain = false;
        ErrorMessage = null;

        // Her plan is loaded once here: all start-up needs from it is whether it exists and its language.
        string? planLanguage;
        try
        {
            planLanguage = await _queries.GetLanguageAsync();
        }
        catch (PlanUnreadableException ex)
        {
            _errors.Report(ex, "startup." + ex.Code);
            State = ex.IsFromNewerApp ? StartupState.NeedsUpdate : StartupState.CannotOpen;
            return;
        }
#pragma warning disable CA1031 // Anything else may pass and says nothing about her plan: she can only try again. The reporter gets the type.
        catch (Exception ex)
        {
            _errors.Report(ex, "startup.load");
            State = StartupState.OpenFailed;
            return;
        }
#pragma warning restore CA1031

        var hasPlan = planLanguage is not null;
        if (hasPlan)
        {
            await _language.ApplyAsync(planLanguage);
        }

        try
        {
            await GoOnAsync(hasPlan, openPlan);

            // Once she is on her way, so her plan is loaded only once before her first screen: it takes the phone's time
            // zone if it has none, newer template wording (with a calm toast), and setup's kept choices go.
            if (hasPlan)
            {
                await _upkeep.RunAsync();
            }
        }
        finally
        {
            // Only after a clean load, once she is on her way: result files the plan does not point at are tidied away.
            // With no plan at all, old files left by a stopped restore go too. When the plan could not be read, nothing
            // is touched.
            Tidying = Task.Run(() => StartupFlow.TidyAsync(_plans, _errors));
        }
    }

    /// <summary>
    /// Where she goes: Results for a share to file (or setup, with a word, when there is no plan yet), setup without a
    /// plan, otherwise her plan.
    /// </summary>
    private async Task GoOnAsync(bool hasPlan, Func<Task>? openPlan)
    {
        if (await _shares.OpenAsync())
        {
            return;
        }

        if (!hasPlan)
        {
            await ResumeSetupAsync();
            return;
        }

        if (openPlan is not null)
        {
            await openPlan();
        }
        else
        {
            await _navigator.GoToMainAsync();
        }
    }

    /// <summary>
    /// Setup, at the step she had reached if Android closed Maia part-way through, with every choice she made. A due
    /// date that no longer fits a current pregnancy takes her back to step 1 to check it.
    /// </summary>
    private async Task ResumeSetupAsync()
    {
        var step = await _setup.RestoreAsync();
        if (_setup.DueDate is { } due && !DueDateEntry.Accepts(due, _clock.Today))
        {
            step = SetupStep.DueDate;
        }

        await _navigator.GoToSetupAsync();
        if (step >= SetupStep.Plan)
        {
            await _navigator.GoToSetupPlanAsync();
        }

        if (step >= SetupStep.Care)
        {
            await _navigator.GoToSetupCareAsync();
        }
    }

    /// <summary>
    /// Restore and Start again are only for a plan known to be unreadable. The commands check it themselves too, since
    /// running a command directly skips its CanExecute.
    /// </summary>
    private bool CanRecover() => CannotOpen && !IsBusy;

    /// <summary>Restore a backup in place of the plan this phone can't open.</summary>
    [RelayCommand(CanExecute = nameof(CanRecover))]
    private Task RestoreAsync() => CanRecover() ? _navigator.GoToRestoreAsync() : Task.CompletedTask;

    /// <summary>Tries to open her plan again (the keystore may just not have been ready).</summary>
    [RelayCommand(CanExecute = nameof(CanTryAgain))]
    private Task TryAgainAsync() => OpenAsync();

    private bool CanTryAgain() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRecover))]
    private void AskStartAgain() => IsConfirmingStartAgain = CanRecover();

    [RelayCommand]
    private void KeepPlan() => IsConfirmingStartAgain = false;

    /// <summary>
    /// Removes the plan this phone can't open with its result files and the data key, then goes to setup. Offered only
    /// for a plan known to be unreadable: never for one from a newer Maia, nor when something else stopped it opening.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRecover))]
    private async Task StartAgainAsync()
    {
        if (!CanRecover())
        {
            return;
        }

        IsBusy = true;
        try
        {
            ErrorMessage = await PlanChange.RunAsync(() => _plans.DeleteEverythingAsync(), "startup.start_again", _errors);
            if (ErrorMessage is not null)
            {
                return;
            }

            // As after "Delete everything": plaintext copies in the cache go too (MAUI's copies of picked and shared files).
            _viewer.ClearCopies();
            await _setup.ClearAsync();
            IsConfirmingStartAgain = false;
            await _navigator.GoToSetupAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
