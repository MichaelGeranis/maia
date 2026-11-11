namespace Maia.Domain.Plans;

/// <summary>What she has said about an entry on her journey. Persisted.</summary>
public enum CheckState
{
    /// <summary>Not booked: it stands at a trimester, a week, or (from setup) its usual weeks.</summary>
    NotBooked = 0,

    /// <summary>Booked for a day and time. Once that time has passed it shows as done, without her.</summary>
    Booked = 1,

    /// <summary>Done, with no booking: she gave only the trimester or the week it was done in.</summary>
    Done = 2,
}

/// <summary>
/// What the UI shows for an entry at a given moment. Derived from <see cref="CheckState"/>, the pregnancy's age and
/// the time it is now; never persisted, so it is always consistent with now.
/// </summary>
public enum CheckStatus
{
    NotBooked = 0,

    /// <summary>Booked for a time still to come.</summary>
    Booked = 1,

    /// <summary>Done: a booking whose time has passed, or one she added as done.</summary>
    Done = 2,

    /// <summary>
    /// A check she kept at setup, never booked, whose usual weeks have passed. It reads as done; checks she added herself
    /// never get this status.
    /// </summary>
    ProbablyDone = 3,

    /// <summary>A timeline marker such as the due date.</summary>
    Milestone = 4,
}
