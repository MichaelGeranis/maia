using Maia.App.Services;
using Maia.App.Views;

namespace Maia.App;

public partial class AppShell : Shell
{
    /// <param name="openOnWeek">
    /// Start on Your week instead of the start-up screen: her plan is on this phone and nothing else comes first (see
    /// StartupFlow). The start-up screen is then never built unless her plan can't be opened.
    /// </param>
    public AppShell(bool openOnWeek = false)
    {
        InitializeComponent();
        if (openOnWeek)
        {
            CurrentItem = Main;
        }

        Routing.RegisterRoute(Routes.SetupPlan, typeof(SetupPlanPage));
        Routing.RegisterRoute(Routes.SetupCare, typeof(SetupCarePage));
        Routing.RegisterRoute(Routes.Questions, typeof(QuestionsPage));
        Routing.RegisterRoute(Routes.Tasks, typeof(TasksPage));
        Routing.RegisterRoute(Routes.Breathe, typeof(BreathePage));
        Routing.RegisterRoute(Routes.Calendar, typeof(CalendarPage));
        Routing.RegisterRoute(Routes.Restore, typeof(RestorePage));
        Routing.RegisterRoute(Routes.ResultViewer, typeof(ResultViewerPage));
        Routing.RegisterRoute(Routes.About, typeof(AboutPage));
        Routing.RegisterRoute(Routes.Privacy, typeof(PrivacyPage));
        Routing.RegisterRoute(Routes.Terms, typeof(TermsPage));
        Routing.RegisterRoute(Routes.Licences, typeof(LicencesPage));
    }
}
