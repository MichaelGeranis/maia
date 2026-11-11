namespace Maia.Presentation.Plan;

/// <summary>
/// Her Journey choices on this phone, kept between visits: whether her tasks with a date show on the timeline. On
/// until she turns it off, so Journey reads as the calendar of what is coming. A setting, not health data. Implemented
/// by the app.
/// </summary>
public interface IJourneySettings
{
    bool ShowTasks { get; set; }
}

/// <summary>Journey's choices for as long as the screen lives, where the phone keeps none (tests, previews).</summary>
internal sealed class SessionJourneySettings : IJourneySettings
{
    public bool ShowTasks { get; set; } = true;
}
