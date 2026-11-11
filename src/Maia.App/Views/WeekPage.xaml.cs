using System.ComponentModel;
using Maia.App.Controls;
using Maia.App.Services;
using Maia.Presentation.Week;

namespace Maia.App.Views;

public partial class WeekPage : MaiaPage
{
    private readonly WeekViewModel _viewModel;

    public WeekPage(WeekViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += OnChanged;

        // A task's editor closed: its field is gone, so the keyboard goes too.
        _viewModel.EditorClosed += (_, _) => Dispatcher.Dispatch(SoftKeyboard.Hide);

        // Loads as it appears, when Maia comes back to the foreground, and when a new day starts while it shows.
        ScreenWork.KeepCurrent(this, _viewModel.LoadAsync, "week.load");
    }

    /// <summary>Leaving Your week (another tab, or Your tasks, Questions or Breathe over it) folds the task card back.</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Leave();
    }

    /// <summary>
    /// Puts the keyboard away once "Add a task" closes (added, cancelled, or giving way to a row opened for changes):
    /// the field it belonged to is gone. The form is hidden before this runs, so the field has lost its focus already
    /// and the keyboard goes through the window.
    /// </summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeekViewModel.IsTaskFormOpen) && !_viewModel.IsTaskFormOpen && (TaskEntry.IsFocused || SoftKeyboard.IsVisible))
        {
            Dispatcher.Dispatch(SoftKeyboard.Hide);
        }
    }
}
