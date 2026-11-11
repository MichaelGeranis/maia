using Maia.App.Controls;
using Maia.App.Services;
using Maia.Presentation.Privacy;
using Maia.Presentation.Startup;

namespace Maia.App.Views;

/// <summary>
/// Start-up screen: opens the plan if one exists, otherwise setup; says calmly when a saved plan can't be opened. When
/// her plan is stored and nothing else comes first, Maia opens straight on Your week instead (see StartupFlow), and
/// comes here only if it can't load.
/// </summary>
public partial class StartupPage : MaiaPage
{
    private readonly StartupViewModel _viewModel;
    private readonly AppLock _lock;

    public StartupPage(StartupViewModel viewModel, AppLock appLock)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _lock = appLock;
    }

    /// <summary>Where to go instead of Your week, once: set when the app is redrawn in another language.</summary>
    public static string? ThenOpen { get; set; }

    /// <summary>
    /// Runs each time the page shows, so coming back from Restore looks again. With "Lock Maia" on, nothing of her plan
    /// opens until she has unlocked.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScreenWork.Load(
            async () =>
            {
                // Windows raises OnAppearing while Shell is still building its views, and navigating then throws
                // "Pending Navigations still processing". With no plan stored, nothing below waits, so let that finish first.
                await Task.Yield();
                await _lock.WhenUnlockedAsync();
                var route = ThenOpen;
                await _viewModel.OpenAsync(route is null ? null : () =>
                {
                    ThenOpen = null;
                    return ShellNavigator.Current?.GoToAsync(route, animate: false) ?? Task.CompletedTask;
                });
            },
            "startup.open");
    }
}
