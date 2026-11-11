using Maia.Application.Abstractions;

namespace Maia.Presentation.Common;

/// <summary>A one-shot timer on the screen's thread, which wakes a <see cref="LiveScreen"/>. The app implements it.</summary>
public interface IWakeTimer
{
    /// <summary>Wakes the screen once after <paramref name="wait"/>, in place of any wait already set.</summary>
    void WakeAfter(TimeSpan wait);

    /// <summary>Drops the wait, if one is set.</summary>
    void Cancel();
}

/// <summary>
/// Keeps a main screen (Your week, Journey) current while it shows. It loads as it appears, again when Maia comes
/// back to the foreground (from the home screen, the phone's settings or a picker), and at the start of a new day, so
/// "Next up · tomorrow" becomes "today" and a check whose window opened overnight asks to be booked. No wait runs while
/// another screen shows or Maia is in the background. The app passes on the page's and the window's events and wakes it
/// with an <see cref="IWakeTimer"/>; none of the methods throw.
/// </summary>
public sealed class LiveScreen
{
    private readonly ScreenLoader _loader;
    private readonly DayWatch _day;
    private readonly IWakeTimer _timer;
    private readonly Func<Task> _load;
    private readonly string _operation;
    private bool _showing;
    private bool _inBackground;
    private bool _loaded;

    /// <param name="loader">Loads the screen: a failure is reported by code and said calmly.</param>
    /// <param name="clock">The phone's clock, for the start of the next day.</param>
    /// <param name="timer">Wakes the screen at the start of the next day: call <see cref="WokeAsync"/>.</param>
    /// <param name="load">The view model's load.</param>
    /// <param name="operation">What a failure is reported as, for example "week.load".</param>
    public LiveScreen(ScreenLoader loader, IClock clock, IWakeTimer timer, Func<Task> load, string operation)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(load);
        _loader = loader;
        _day = new DayWatch(clock);
        _timer = timer;
        _load = load;
        _operation = operation;
    }

    private bool IsAwake => _showing && !_inBackground;

    /// <summary>The screen appeared: it loads and waits for the next day.</summary>
    public Task AppearedAsync()
    {
        _showing = true;
        _inBackground = false;
        return RefreshAsync();
    }

    /// <summary>Another screen shows: no wait runs.</summary>
    public void Disappeared()
    {
        _showing = false;
        _timer.Cancel();
    }

    /// <summary>Maia went to the background: no wait runs, and the screen loads again on the way back.</summary>
    public void Stopped()
    {
        _inBackground = true;
        _timer.Cancel();
    }

    /// <summary>Maia is back in the foreground: a screen showing loads again (what the phone allows or the day may have changed).</summary>
    public Task ResumedAsync()
    {
        _inBackground = false;
        return _showing ? RefreshAsync() : Task.CompletedTask;
    }

    /// <summary>
    /// The wait ended. A new day, or a screen whose last load failed, loads again; woken early (the clock or the time
    /// zone changed on the way), it waits again.
    /// </summary>
    public Task WokeAsync()
    {
        if (!IsAwake)
        {
            return Task.CompletedTask;
        }

        if (!_loaded || _day.IsNewDay)
        {
            return RefreshAsync();
        }

        WaitForNextDay();
        return Task.CompletedTask;
    }

    private async Task RefreshAsync()
    {
        _timer.Cancel();

        // Only a load that worked counts as today's: one that failed is tried again when the wait ends.
        _loaded = await _loader.LoadAsync(_load, _operation);
        if (_loaded)
        {
            _day.Loaded();
        }

        WaitForNextDay();
    }

    private void WaitForNextDay()
    {
        if (IsAwake)
        {
            _timer.WakeAfter(_day.UntilNextDay);
        }
    }
}
