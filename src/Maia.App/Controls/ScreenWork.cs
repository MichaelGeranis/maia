using Maia.Application.Abstractions;
using Maia.Presentation.Common;

namespace Maia.App.Controls;

/// <summary>
/// The one way pages run async work from their events (appearing, a property change, a tap), in place of
/// <c>async void</c>: a failure never ends Maia. What happens then is decided in Presentation (see
/// <see cref="ScreenLoader"/> and <see cref="LiveScreen"/>); this only passes on the page's and the window's events.
/// </summary>
public static class ScreenWork
{
    private static IServiceProvider? Services => IPlatformApplication.Current?.Services;

    private static ScreenLoader? Loader => Services?.GetService<ScreenLoader>();

    /// <summary>Loads a screen from a page event.</summary>
    /// <param name="load">The view model's load.</param>
    /// <param name="operation">What a failure is reported as, for example "team.load".</param>
    public static void Load(Func<Task> load, string operation)
    {
        if (Loader is { } loader)
        {
            _ = loader.LoadAsync(load, operation);
        }
        else
        {
            _ = UnreportedAsync(load);
        }
    }

    /// <summary>Runs other work from a page event (putting the keyboard away, an animation): a failure is only reported.</summary>
    public static void Run(Func<Task> work, string operation)
    {
        if (Loader is { } loader)
        {
            _ = loader.RunAsync(work, operation);
        }
        else
        {
            _ = UnreportedAsync(work);
        }
    }

    /// <summary>
    /// Keeps a main screen current (see <see cref="LiveScreen"/>): it loads as it appears, again when Maia comes back
    /// to the foreground, and at the start of a new day while it shows. Call once, from the page's constructor.
    /// </summary>
    public static void KeepCurrent(Page page, Func<Task> load, string operation)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(load);
        _ = new LivePage(page, load, operation);
    }

    /// <summary>Before Maia's services are built there is nowhere to report to; a page event still never ends Maia.</summary>
    private static async Task UnreportedAsync(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
#pragma warning disable CA1031 // Nothing to report to yet, and a page event must never end Maia.
        try
        {
            await work();
        }
        catch (Exception)
        {
            // Nowhere to report before the app is built.
        }
#pragma warning restore CA1031
    }

    /// <summary>Passes a page's and its window's events to its <see cref="LiveScreen"/>; listens to the window only while the page shows.</summary>
    private sealed class LivePage
    {
        private readonly Page _page;
        private readonly Func<Task> _load;
        private readonly string _operation;
        private LiveScreen? _live;
        private Window? _window;

        public LivePage(Page page, Func<Task> load, string operation)
        {
            _page = page;
            _load = load;
            _operation = operation;
            page.Appearing += OnAppearing;
            page.Disappearing += OnDisappearing;
        }

        private void OnAppearing(object? sender, EventArgs e)
        {
            if (Live() is not { } live)
            {
                Load(_load, _operation);
                return;
            }

            if (_page.Window is { } window && _window is null)
            {
                _window = window;
                window.Resumed += OnResumed;
                window.Stopped += OnStopped;
            }

            Run(live.AppearedAsync, _operation);
        }

        private void OnDisappearing(object? sender, EventArgs e)
        {
            if (_window is { } window)
            {
                window.Resumed -= OnResumed;
                window.Stopped -= OnStopped;
                _window = null;
            }

            _live?.Disappeared();
        }

        private void OnResumed(object? sender, EventArgs e)
        {
            if (_live is { } live)
            {
                Run(live.ResumedAsync, _operation);
            }
        }

        private void OnStopped(object? sender, EventArgs e) => _live?.Stopped();

        private void OnWoke()
        {
            if (_live is { } live)
            {
                Run(live.WokeAsync, _operation);
            }
        }

        private LiveScreen? Live()
        {
            if (_live is null && Loader is { } loader && Services?.GetService<IClock>() is { } clock)
            {
                _live = new LiveScreen(loader, clock, new DispatcherWakeTimer(_page.Dispatcher, OnWoke), _load, _operation);
            }

            return _live;
        }
    }

    /// <summary>A one-shot dispatcher timer: it wakes on the page's thread.</summary>
    private sealed class DispatcherWakeTimer : IWakeTimer
    {
        private readonly IDispatcherTimer _timer;

        public DispatcherWakeTimer(IDispatcher dispatcher, Action woke)
        {
            _timer = dispatcher.CreateTimer();
            _timer.IsRepeating = false;
            _timer.Tick += (_, _) => woke();
        }

        public void WakeAfter(TimeSpan wait)
        {
            _timer.Stop();
            _timer.Interval = wait;
            _timer.Start();
        }

        public void Cancel() => _timer.Stop();
    }
}
