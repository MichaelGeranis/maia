namespace Maia.Presentation.Navigation;

/// <summary>Screen navigation, implemented by the app with MAUI Shell. View models never touch Shell directly.</summary>
public interface INavigator
{
    /// <summary>
    /// The start-up screen, when her plan is stored but can't be opened: it says why and offers the ways on (restore a
    /// backup, start again, update Maia), which no other screen can.
    /// </summary>
    Task GoToStartupAsync();

    Task GoToSetupAsync();

    Task GoToSetupPlanAsync();

    /// <summary>Setup step 3: her OB-GYN and reminders.</summary>
    Task GoToSetupCareAsync();

    /// <summary>The main screens, starting on Your week.</summary>
    Task GoToMainAsync();

    /// <summary>Journey (once called Your plan). To open it on one check, call <see cref="PlanFocus.Request"/> first.</summary>
    Task GoToPlanAsync();

    /// <summary>Results, for example to file what another app shared.</summary>
    Task GoToResultsAsync();

    /// <summary>The in-app viewer. Call <see cref="Results.ResultViewing.ShowAsync"/> rather than this.</summary>
    Task GoToResultViewerAsync();

    /// <summary>The Settings tab, for example to make a backup.</summary>
    Task GoToSettingsAsync();

    Task GoToRestoreAsync();

    /// <summary>"Add to your journey", as the bottom bar's Add and Journey's Add open it.</summary>
    Task GoToAddAsync();

    Task GoToQuestionsAsync();

    /// <summary>
    /// Your tasks, every task in its group, pushed from Your week (the Week slot stays lit). To open it at a group, call
    /// <see cref="TasksFocus.Request"/> first.
    /// </summary>
    Task GoToTasksAsync();

    Task GoToBreatheAsync();

    /// <summary>Calendar: her journey as a month, to look at only, pushed from the week pill on Today.</summary>
    Task GoToCalendarAsync();

    /// <summary>About Maia (pushed from Settings): version, the privacy notice, the terms of use and licences.</summary>
    Task GoToAboutAsync();

    /// <summary>The privacy notice, from About Maia or setup step 1.</summary>
    Task GoToPrivacyAsync();

    /// <summary>The terms of use, from About Maia or setup step 1.</summary>
    Task GoToTermsAsync();

    /// <summary>The fonts and libraries in Maia, with their licences.</summary>
    Task GoToLicencesAsync();

    Task GoBackAsync();
}

/// <summary>
/// Short toasts after an action, implemented by the app. <see cref="Show"/> confirms something done ("Glucose test booked
/// for Thu 1 Oct at 09:00.") with the success tick; <see cref="ShowNotice"/> says calmly that something did not happen
/// (her list is full, a change could not be saved, the screen could not be shown), on Sand and without the tick.
/// </summary>
public interface INotifier
{
    /// <summary>A confirmation: what she did has happened.</summary>
    void Show(string message);

    /// <summary>A calm note that something did not happen, or could not be done: never shown as a success.</summary>
    void ShowNotice(string message);
}
