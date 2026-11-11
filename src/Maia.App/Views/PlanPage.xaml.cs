using Maia.App.Controls;
using Maia.Presentation.Plan;

namespace Maia.App.Views;

public partial class PlanPage : MaiaPage
{
    private readonly PlanViewModel _viewModel;

    /// <summary>A row to bring to the top as soon as the list is on screen (the first time, it is not yet).</summary>
    private PlanRow? _pendingScroll;

    public PlanPage(PlanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ScrollRequested += OnScrollRequested;
        Timeline.Loaded += OnTimelineLoaded;

        // Loads as it appears, when Maia comes back to the foreground, and when a new day starts while it shows.
        ScreenWork.KeepCurrent(this, _viewModel.LoadAsync, "plan.load");
    }

    /// <summary>
    /// Opening Journey (from the bar, Your week or anywhere else) shows today. Coming back to it straight from the
    /// screen she opened over it (Add, a result) keeps her place; so does Maia coming back to the foreground, which is
    /// no navigation. Shell reports the page shown before Journey as <see cref="NavigatedToEventArgs.PreviousPage"/>,
    /// so leaving that screen for another tab first (Add, then Week) opens Journey at today again (see
    /// <see cref="PlanViewModel.Arrive"/>).
    /// </summary>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        base.OnNavigatedTo(args);
        _viewModel.Arrive(args.PreviousPage);
    }

    /// <summary>A screen pushed over Journey is remembered; going to another tab is not.</summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        base.OnNavigatedFrom(args);
        _viewModel.Leave(args.NavigationType == NavigationType.Push ? args.DestinationPage : null);
    }

    /// <summary>
    /// Brings a row to the top at once, with no animation: the check opened from another screen, or today as Journey
    /// opens. It is asked for in the same turn as the rows change, before the list is drawn, so the list is laid out
    /// there rather than shown at the top first and then jumping. The first time Journey opens, the list is not on
    /// screen yet: it scrolls as it arrives.
    /// </summary>
    private void OnScrollRequested(object? sender, PlanRow row)
    {
        if (Timeline.IsLoaded)
        {
            Timeline.ScrollTo(row, position: ScrollToPosition.Start, animate: false);
        }
        else
        {
            _pendingScroll = row;
        }
    }

    private void OnTimelineLoaded(object? sender, EventArgs e)
    {
        if (_pendingScroll is { } row)
        {
            _pendingScroll = null;
            Timeline.ScrollTo(row, position: ScrollToPosition.Start, animate: false);
        }
    }
}
