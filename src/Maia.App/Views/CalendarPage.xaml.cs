using Maia.App.Controls;
using Maia.App.Services;
using Maia.Presentation.Calendar;

namespace Maia.App.Views;

public partial class CalendarPage : MaiaPage
{
    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Loads as it appears, when Maia comes back to the foreground, and when a new day starts while it shows.
        ScreenWork.KeepCurrent(this, viewModel.LoadAsync, "calendar.load");
    }
}
