using Maia.App.Controls;
using Maia.Application.Abstractions;
using Maia.Presentation.Navigation;

namespace Maia.App.Services;

/// <summary>Shell routes. Main tabs live under <c>//main</c>; the rest are pushed onto the current tab.</summary>
public static class Routes
{
    public const string SetupPlan = "setupplan";
    public const string SetupCare = "setupcare";
    public const string Questions = "questions";
    public const string Tasks = "tasks";
    public const string Breathe = "breathe";
    public const string Calendar = "calendar";
    public const string Restore = "restore";
    public const string ResultViewer = "resultviewer";
    public const string About = "about";
    public const string Privacy = "privacy";
    public const string Terms = "terms";
    public const string Licences = "licences";
}

/// <summary>
/// Navigation with MAUI Shell. Routes are declared in AppShell. Every navigation passes one gate
/// (<see cref="NavigationGate"/>): a tap on the bottom bar or a back arrow while another navigation is on its way is
/// dropped, going where she already is does nothing, and a navigation that fails is reported by code instead of
/// ending Maia.
/// </summary>
public sealed class ShellNavigator : INavigator, IDisposable
{
    /// <summary>The start-up screen: it says why her plan can't be opened and offers the ways on.</summary>
    public const string Startup = "//startup";

    private readonly NavigationGate _gate;

    public ShellNavigator(IErrorReporter errors)
    {
        _gate = new NavigationGate(errors);
    }

    /// <summary>The navigator the app's controls use; null before the app is built.</summary>
    public static ShellNavigator? Current => IPlatformApplication.Current?.Services.GetService<ShellNavigator>();

    /// <summary>
    /// For a tap on the bottom bar or a back arrow: goes unless another navigation is on its way or she is already there
    /// (Add on the Add screen).
    /// </summary>
    public Task<bool> TapAsync(string route, bool animate = true) =>
        MainThread.InvokeOnMainThreadAsync(() => _gate.TapAsync(route, Location, () => Shell.Current.GoToAsync(route, animate)));

    /// <summary>The start-up screen, for a plan that can't be opened (see <see cref="Startup"/>).</summary>
    public Task GoToStartupAsync() => Go(Startup, animate: false);

    /// <summary>Any route, for a screen going back where she was (after a language switch, Settings).</summary>
    public Task GoToAsync(string route, bool animate = true) => Go(route, animate);

    public Task GoToSetupAsync() => Go("//setup");

    public Task GoToSetupPlanAsync() => Go(Routes.SetupPlan);

    public Task GoToSetupCareAsync() => Go(Routes.SetupCare);

    public Task GoToMainAsync() => Go($"//main/{Tabs.Week}");

    public Task GoToPlanAsync() => Go($"//main/{Tabs.Plan}", animate: false);

    public Task GoToResultsAsync() => Go($"//main/{Tabs.Results}", animate: false);

    public Task GoToResultViewerAsync() => Go(Routes.ResultViewer);

    public Task GoToSettingsAsync() => Go($"//main/{Tabs.Settings}", animate: false);

    public Task GoToRestoreAsync() => Go(Routes.Restore);

    public Task GoToAddAsync() => Go($"//main/{Tabs.Add}", animate: false);

    public Task GoToQuestionsAsync() => Go(Routes.Questions);

    public Task GoToTasksAsync() => Go(Routes.Tasks);

    public Task GoToBreatheAsync() => Go(Routes.Breathe);

    public Task GoToCalendarAsync() => Go(Routes.Calendar);

    public Task GoToAboutAsync() => Go(Routes.About);

    public Task GoToPrivacyAsync() => Go(Routes.Privacy);

    public Task GoToTermsAsync() => Go(Routes.Terms);

    public Task GoToLicencesAsync() => Go(Routes.Licences);

    public Task GoBackAsync() => Go(NavigationGate.Back);

    public void Dispose() => _gate.Dispose();

    private static string? Location() => Shell.Current?.CurrentState?.Location?.OriginalString;

    private Task<bool> Go(string route, bool animate = true) =>
        MainThread.InvokeOnMainThreadAsync(() => _gate.GoAsync(route, Location, () => Shell.Current.GoToAsync(route, animate)));
}
