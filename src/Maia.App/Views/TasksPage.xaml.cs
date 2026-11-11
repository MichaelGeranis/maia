using Maia.App.Controls;
using Maia.App.Services;
using Maia.Presentation.Week;

namespace Maia.App.Views;

public partial class TasksPage : MaiaPage
{
    private readonly TasksViewModel _viewModel;

    /// <summary>A group to bring to the top as soon as the list is on screen (the first time, it is not yet).</summary>
    private object? _pendingScroll;

    public TasksPage(TasksViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ScrollRequested += OnScrollRequested;
        List.Loaded += OnListLoaded;

        // A task's editor closed: its field is gone, so the keyboard goes too.
        _viewModel.EditorClosed += (_, _) => Dispatcher.Dispatch(SoftKeyboard.Hide);

        // Loads as it appears, when Maia comes back to the foreground, and when a new day starts while it shows.
        ScreenWork.KeepCurrent(this, _viewModel.LoadAsync, "tasks.load");
    }

    /// <summary>
    /// Brings the group she tapped on Your week to the top at once, with no animation, in the same turn as the rows
    /// arrive. The first time, the list is not on screen yet: it scrolls as it arrives.
    /// </summary>
    private void OnScrollRequested(object? sender, object row)
    {
        if (List.IsLoaded)
        {
            List.ScrollTo(row, position: ScrollToPosition.Start, animate: false);
        }
        else
        {
            _pendingScroll = row;
        }
    }

    private void OnListLoaded(object? sender, EventArgs e)
    {
        if (_pendingScroll is { } row)
        {
            _pendingScroll = null;
            List.ScrollTo(row, position: ScrollToPosition.Start, animate: false);
        }
    }
}
