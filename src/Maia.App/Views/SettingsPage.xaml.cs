using Maia.App.Controls;
using Maia.Presentation.Settings;

namespace Maia.App.Views;

public partial class SettingsPage : MaiaPage
{
    private readonly SettingsViewModel _viewModel;
    private Window? _window;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // The diagnostics card has screen logic of its own: one tap shares the problem log.
        DiagnosticsCard.BindingContext = viewModel.Diagnostics;
    }

    /// <summary>
    /// Each time Settings shows: her due date, her care team, her reminders, and whether
    /// the phone has a screen lock. The care team loads on its own, so a problem there leaves the other cards as they are.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _window = Window;
        if (_window is not null)
        {
            _window.Resumed += OnResumed;
        }

        ScreenWork.Load(_viewModel.LoadAsync, "settings.load");
        ScreenWork.Load(_viewModel.Team.LoadAsync, "team.load");
    }

    /// <summary>
    /// Settings is a tab, so the page stays while she is elsewhere: what she left open in place closes (see
    /// <see cref="SettingsViewModel.Leave"/>).
    /// </summary>
    protected override void OnDisappearing()
    {
        if (_window is not null)
        {
            _window.Resumed -= OnResumed;
            _window = null;
        }

        _viewModel.Leave();
        base.OnDisappearing();
    }

    /// <summary>
    /// Back from the phone's settings, where she may just have set a screen lock, or allowed notifications: the
    /// lock card and the reminders card show what the phone allows now.
    /// </summary>
    private void OnResumed(object? sender, EventArgs e)
    {
        _viewModel.Lock.Refresh();
        ScreenWork.Load(_viewModel.Reminders.LoadAsync, "settings.reminders.load");
    }
}
