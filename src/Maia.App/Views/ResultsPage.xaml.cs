using Maia.App.Controls;
using Maia.Presentation.Results;

namespace Maia.App.Views;

public partial class ResultsPage : MaiaPage
{
    private readonly ResultsViewModel _viewModel;
    private readonly ShareIntake _shares;

    public ResultsPage(ResultsViewModel viewModel, ShareIntake shares)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _shares = shares;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _shares.Changed += OnSharesChanged;
        ScreenWork.Load(_viewModel.LoadAsync, "results.load");
    }

    protected override void OnDisappearing()
    {
        _shares.Changed -= OnSharesChanged;
        _viewModel.Leave();
        base.OnDisappearing();
    }

    /// <summary>While she chooses results to share, the phone's Back ends the choosing rather than leaving Results.</summary>
    protected override bool OnBackButtonPressed() => _viewModel.HandleBack() || base.OnBackButtonPressed();

    /// <summary>
    /// She held a tile. Android's long press reaches MAUI only as the start of a drag, so the drag is cancelled (nothing
    /// is dragged) and choosing results to share starts, with this one ticked.
    /// </summary>
    private void OnTileHeld(object? sender, DragStartingEventArgs e)
    {
        e.Cancel = true;
        if (sender is GestureRecognizer { BindingContext: ResultCard card })
        {
            _viewModel.SelectFromCommand.Execute(card);
        }
    }

    /// <summary>A file shared while Results is already showing: kept at once, then she is asked whether to tag it.</summary>
    private void OnSharesChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => ScreenWork.Run(_viewModel.KeepSharedAsync, "results.share"));
}
