using Maia.Application.Abstractions;

namespace Maia.Presentation.Common;

/// <summary>
/// The day a screen last loaded on, so a screen left open past midnight (or found again the next morning) can tell a
/// new day has begun: "Next up · tomorrow" becomes "today", and a check's window may have opened overnight.
/// </summary>
public sealed class DayWatch
{
    /// <summary>A little past midnight, so the new day has surely begun when the screen looks again.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromSeconds(1);

    private readonly IClock _clock;
    private DateOnly? _loadedOn;

    public DayWatch(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>Whether the screen loaded on an earlier day than today.</summary>
    public bool IsNewDay => _loadedOn is { } day && day != _clock.Today;

    /// <summary>
    /// How long until the next day begins on the phone's clock. A clock change or a time-zone change on the way only
    /// makes the wait end early or late: look at <see cref="IsNewDay"/> when it ends, and wait again.
    /// </summary>
    public TimeSpan UntilNextDay
    {
        get
        {
            // Always more than nothing: the next midnight is at least a tick away.
            var local = TimeZoneInfo.ConvertTime(_clock.UtcNow, _clock.TimeZone).DateTime;
            return local.Date.AddDays(1) - local + Margin;
        }
    }

    /// <summary>Call after each load.</summary>
    public void Loaded() => _loadedOn = _clock.Today;
}
