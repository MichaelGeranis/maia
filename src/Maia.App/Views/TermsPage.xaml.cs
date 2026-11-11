using Maia.App.Controls;
using Maia.Presentation.About;

namespace Maia.App.Views;

public partial class TermsPage : MaiaPage
{
    public TermsPage(TermsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
