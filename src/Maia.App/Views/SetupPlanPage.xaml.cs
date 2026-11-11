using Maia.App.Controls;
using Maia.App.Services;
using Maia.Presentation.Navigation;
using Maia.Presentation.Setup;

namespace Maia.App.Views;

public partial class SetupPlanPage : MaiaPage
{
    private readonly SetupPlanViewModel _viewModel;

    public SetupPlanPage(SetupPlanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        // Through the navigation gate: a double tap goes back once.
        Back.Command = new Command(() => ShellNavigator.Current?.TapAsync(NavigationGate.Back));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScreenWork.Load(_viewModel.LoadAsync, "setup_plan.load");
    }
}
