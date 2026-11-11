using Maia.App.Controls;
using Maia.Presentation.About;

namespace Maia.App.Views;

public partial class AboutPage : MaiaPage
{
    public AboutPage(AboutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
