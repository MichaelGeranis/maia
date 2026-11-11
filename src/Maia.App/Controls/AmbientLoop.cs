using Maia.App.Services;

namespace Maia.App.Controls;

/// <summary>
/// A looping ambient animation (background drift, waves, glows, ripples, the breathing ring) that runs only while it
/// can be seen and is worth its battery: its page is on screen, the app is in the foreground, the phone allows motion,
/// and Battery Saver is off. Hidden tabs and a phone in a pocket cost nothing. The tick gets the seconds the loop has
/// been running, so a paused loop resumes where it was.
/// <para>
/// Every loop steps on one shared clock, 20 times a second: all of them change in the same step and cost one redraw
/// between them, and nothing wakes in between. (An animation on MAUI's own ticker would run on every frame of the
/// display, 60 to 120 times a second, only to skip most of them.)
/// </para>
/// </summary>
public sealed class AmbientLoop
{
    /// <summary>Time between steps: 20 a second is plenty for slow drifts and keeps the screen mostly idle.</summary>
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);

    private static readonly List<AmbientLoop> Running = [];
    private static IDispatcherTimer? _clock;

    private readonly VisualElement _owner;
    private readonly Action<double> _tick;
    private Page? _page;
    private Window? _window;
    private bool _loaded;
    private bool _pageVisible = true;
    private bool _foreground = true;
    private bool _running;
    private double _elapsed;
    private DateTime _resumedAt;

    private AmbientLoop(VisualElement owner, Action<double> tick)
    {
        _owner = owner;
        _tick = tick;
        owner.Loaded += OnLoaded;
        owner.Unloaded += OnUnloaded;
    }

    /// <summary>Whether the loop is ticking right now.</summary>
    public bool IsRunning => _running;

    /// <param name="owner">The element that animates; the loop lives as long as it is loaded.</param>
    /// <param name="tick">Called with the running seconds, 20 times a second; set translations, scale, opacity or redraw here.</param>
    public static AmbientLoop Attach(VisualElement owner, Action<double> tick)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(tick);
        return new AmbientLoop(owner, tick);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _loaded = true;
        _pageVisible = true;
        _page = Motions.FindPage(_owner);
        if (_page is not null)
        {
            _page.Appearing += OnAppearing;
            _page.Disappearing += OnDisappearing;
        }

        _window = _owner.Window;
        if (_window is not null)
        {
            _window.Stopped += OnStopped;
            _window.Resumed += OnResumed;
        }

        Motion.Changed += OnMotionChanged;
        Update();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _loaded = false;
        if (_page is not null)
        {
            _page.Appearing -= OnAppearing;
            _page.Disappearing -= OnDisappearing;
            _page = null;
        }

        if (_window is not null)
        {
            _window.Stopped -= OnStopped;
            _window.Resumed -= OnResumed;
            _window = null;
        }

        Motion.Changed -= OnMotionChanged;
        Update();
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        _pageVisible = true;
        Update();
    }

    private void OnDisappearing(object? sender, EventArgs e)
    {
        _pageVisible = false;
        Update();
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        _foreground = false;
        Update();
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        _foreground = true;
        Update();
    }

    private void OnMotionChanged(object? sender, EventArgs e) => _owner.Dispatcher.Dispatch(Update);

    private void Update()
    {
        var shouldRun = _loaded && _pageVisible && _foreground && !Motion.IsReduced && !Motion.IsSaving;
        if (shouldRun && !_running)
        {
            _running = true;
            _resumedAt = DateTime.UtcNow;
            Running.Add(this);
            StartClock(_owner.Dispatcher);
        }
        else if (!shouldRun && _running)
        {
            _running = false;
            _elapsed += (DateTime.UtcNow - _resumedAt).TotalSeconds;
            Running.Remove(this);
            if (Running.Count == 0)
            {
                _clock?.Stop();
            }
        }
    }

    private static void StartClock(IDispatcher dispatcher)
    {
        if (_clock is null)
        {
            _clock = dispatcher.CreateTimer();
            _clock.Interval = Step;
            _clock.IsRepeating = true;
            _clock.Tick += (_, _) => StepAll();
        }

        if (!_clock.IsRunning)
        {
            _clock.Start();
        }
    }

    /// <summary>
    /// One step for every running loop, together, so the screen is redrawn once for all of them. They keep moving while
    /// she scrolls (the owner: motion that stops under her finger looks broken).
    /// </summary>
    private static void StepAll()
    {
        var now = DateTime.UtcNow;
        foreach (var loop in Running.ToArray())
        {
            if (loop._running)
            {
                loop._tick(loop._elapsed + (now - loop._resumedAt).TotalSeconds);
            }
        }
    }
}
