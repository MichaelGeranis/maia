using Maia.App.Controls;
using Maia.Presentation.Setup;

namespace Maia.App.Views;

public partial class RestorePage : MaiaPage
{
    public RestorePage(RestoreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Puts the keyboard away when she leaves (after restoring, the password field is gone).</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (PasswordEntry.IsFocused)
        {
            ScreenWork.Run(
                async () =>
                {
                    await PasswordEntry.HideSoftInputAsync(CancellationToken.None);
                    PasswordEntry.Unfocus();
                },
                "restore.keyboard");
        }
    }
}
