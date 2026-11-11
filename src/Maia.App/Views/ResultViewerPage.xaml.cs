using System.ComponentModel;
using Maia.App.Controls;
using Maia.Presentation.Results;

namespace Maia.App.Views;

public partial class ResultViewerPage : MaiaPage
{
    /// <summary>The page cards' side margins, in dp: PDF pages are drawn as wide as they show.</summary>
    private const double SideMargins = 40;

    private readonly ResultViewerViewModel _viewModel;
    private (int First, int Last) _inView;

    public ResultViewerPage(ResultViewerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Listened to only while the viewer shows, so nothing outside keeps this page once she leaves.
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.PropertyChanged += OnViewModelChanged;
        var display = DeviceDisplay.Current.MainDisplayInfo;
        var width = Width > 0 ? Width : display.Width / display.Density;
        ScreenWork.Load(() => _viewModel.LoadAsync((int)((width - SideMargins) * display.Density)), "viewer.load");
    }

    /// <summary>Leaving the viewer lets go of the decrypted file.</summary>
    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Close();
        base.OnDisappearing();
    }

    /// <summary>Back from a zoomed page goes to all pages first.</summary>
    protected override bool OnBackButtonPressed() => _viewModel.HandleBack() || base.OnBackButtonPressed();

    /// <summary>As she scrolls, the pages coming into view are drawn and those far away let go (only when that changes).</summary>
    private void OnPagesScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        var inView = (e.FirstVisibleItemIndex, e.LastVisibleItemIndex);
        if (inView == _inView || inView.FirstVisibleItemIndex < 0)
        {
            return;
        }

        _inView = inView;
        ScreenWork.Run(() => _viewModel.ShowPagesAsync(inView.FirstVisibleItemIndex, inView.LastVisibleItemIndex), "viewer.pages");
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResultViewerViewModel.ZoomedPage))
        {
            PageZoom.Reset();
        }
    }
}
