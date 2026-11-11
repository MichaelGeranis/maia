using Maia.App.Controls;
using Maia.Presentation.Setup;

namespace Maia.App.Views;

public partial class SetupCarePage : MaiaPage
{
    private readonly SetupCareViewModel _viewModel;

    public SetupCarePage(SetupCareViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScreenWork.Load(_viewModel.LoadAsync, "setup_care.load");
    }
}
