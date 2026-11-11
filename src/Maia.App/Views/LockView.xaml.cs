using Maia.Presentation.Privacy;

namespace Maia.App.Views;

/// <summary>Covers Maia while it is locked. The presenter asks for the phone's screen lock once as it shows; Unlock asks again.</summary>
public partial class LockView : ContentView
{
    private readonly LockViewModel _viewModel;

    public LockView(LockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>Asks for the phone's screen lock, as Unlock does.</summary>
    public void Ask() => _viewModel.UnlockCommand.Execute(null);
}
