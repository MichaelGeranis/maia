using Maia.App.Controls;
using Maia.Presentation.About;

namespace Maia.App.Views;

public partial class PrivacyPage : MaiaPage
{
    public PrivacyPage(PrivacyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
