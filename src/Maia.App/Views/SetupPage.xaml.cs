using Maia.App.Controls;
using Maia.Presentation.Setup;

namespace Maia.App.Views;

public partial class SetupPage : MaiaPage
{
    public SetupPage(SetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
