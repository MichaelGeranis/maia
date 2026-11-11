using Maia.Presentation.Plan;

namespace Maia.App.Services;

/// <summary>
/// Her Journey choices in the phone's preferences: the "Tasks" switch is on until she turns it off. A setting, nothing
/// about her plan.
/// </summary>
public sealed class PreferencesJourneySettings : IJourneySettings
{
    private const string ShowTasksKey = "journey.show_tasks";

    public bool ShowTasks
    {
        get => Preferences.Default.Get(ShowTasksKey, true);
        set => Preferences.Default.Set(ShowTasksKey, value);
    }
}
