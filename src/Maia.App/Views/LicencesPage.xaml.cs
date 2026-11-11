using Maia.App.Controls;
using Maia.Presentation.About;

namespace Maia.App.Views;

public partial class LicencesPage : MaiaPage
{
    public LicencesPage(LicencesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
