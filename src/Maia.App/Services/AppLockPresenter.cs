using System.ComponentModel;
using Maia.Application.Abstractions;
using Maia.Presentation.Privacy;

namespace Maia.App.Services;

/// <summary>"Lock Maia" in the phone's preferences: off unless she turns it on. A setting, not health data.</summary>
public sealed class PreferencesAppLockPreference : IAppLockPreference
{
    private const string Key = "app_lock";

    public bool IsOn
    {
        get => Preferences.Default.Get(Key, false);
        set => Preferences.Default.Set(Key, value);
    }
}

/// <summary>
/// Time away, on a clock that keeps counting while the phone sleeps and that changing the phone's clock can't move
/// (Android's elapsed real time). The runtime's own monotonic clock stops in deep sleep, so a phone left for an hour
/// could look away for seconds.
/// </summary>
public sealed class BootClock : TimeProvider
{
#if ANDROID
    public override long TimestampFrequency => 1_000_000_000;

    public override long GetTimestamp() => global::Android.OS.SystemClock.ElapsedRealtimeNanos();
#endif
}

/// <summary>
/// Shows the lock screen over everything while <see cref="AppLock"/> is locked, and tells it when Maia goes to the
/// background and comes back. The lock screen has a window of its own over Maia (on Android a full-screen dialog, see
/// LockWindow), never a page on Shell's navigation, so the page she was on never closes or loads again underneath: an
/// open result, a half-filled Add screen and the start-up screen stay exactly as they were. While Maia is away
/// with the lock on, its pages are hidden, so coming back never shows her plan for a moment before the lock screen
/// covers it. Also keeps the recents screen private (see ScreenPrivacy).
/// </summary>
public sealed class AppLockPresenter
{
    private readonly AppLock _lock;
    private readonly IServiceProvider _services;
    private readonly IErrorReporter _errors;
    private Window? _window;
#if ANDROID
    private Platforms.Android.LockWindow? _shown;
#endif

    public AppLockPresenter(AppLock appLock, IServiceProvider services, IErrorReporter errors)
    {
        _lock = appLock;
        _services = services;
        _errors = errors;
        _lock.PropertyChanged += OnLockChanged;
    }

    /// <summary>Whether Maia starts locked, so its first screen is the start-up screen, never her plan (see StartupFlow).</summary>
    public bool StartsLocked => _lock.LocksOnStart;

    /// <summary>
    /// Follows the window: Maia starts locked when the lock is on, and locks again after a minute away. A new window
    /// (Android makes Maia's screen again after a change of the phone's language or text size, say) takes over from the
    /// old one, whose lock screen went with it.
    /// </summary>
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Close(fade: false);
        _window = window;

        window.Stopped += (_, _) =>
        {
            if (window == _window && _lock.Leave())
            {
                Cover(true);
            }
        };
        window.Resumed += (_, _) =>
        {
            if (window != _window)
            {
                return;
            }

            _lock.Return();
            if (!_lock.IsLocked)
            {
                Cover(false);
            }
        };

        // Shown as soon as Maia is in front; start-up waits for the unlock before it opens her plan.
        window.Activated += (_, _) =>
        {
            if (window == _window && _lock.IsLocked)
            {
                Post(Show);
            }
        };
        window.Destroying += (_, _) =>
        {
            if (window == _window)
            {
                Close(fade: false);
                _window = null;
            }
        };

        _lock.Start();
        ApplyScreenPrivacy();
    }

    private void OnLockChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppLock.IsLocked):
                // Always posted, never run in place: locking happens while Android is still bringing Maia back
                // (OnRestart); the post runs once it is back in front.
                Post(() =>
                {
                    if (_lock.IsLocked)
                    {
                        Show();
                    }
                    else
                    {
                        Cover(false);
                        Close(fade: !Motion.IsReduced);
                    }
                });
                break;
            case nameof(AppLock.IsOn):
                Post(ApplyScreenPrivacy);
                break;
            default:
                break;
        }
    }

    private void Post(Action action)
    {
        if (_window?.Dispatcher is { } dispatcher)
        {
            dispatcher.Dispatch(action);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    private void Show()
    {
#if ANDROID
        if (!_lock.IsLocked || _shown is not null
            || _window?.Handler is not { MauiContext: { } context, PlatformView: global::Android.App.Activity activity }
            || activity.IsFinishing || activity.IsDestroyed)
        {
            return;
        }

        Cover(true);
        try
        {
            var view = _services.GetRequiredService<Views.LockView>();
            var shown = Platforms.Android.LockWindow.Open(activity, view, context);
            _shown = shown;

            // A moment after it is up, so the phone's prompt opens over the lock screen.
            view.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () =>
            {
                if (_shown == shown)
                {
                    view.Ask();
                }
            });
        }
#pragma warning disable CA1031 // Reported by code; her pages stay hidden, and coming back to Maia tries again.
        catch (Exception ex)
        {
            _shown = null;
            _errors.Report(ex, "lock.show");
        }
#pragma warning restore CA1031
#endif
    }

    /// <summary>Takes the lock screen away, if it shows.</summary>
#pragma warning disable CA1822 // Uses the lock window on Android; only the other targets compile it without instance data.
    private void Close(bool fade)
#pragma warning restore CA1822
    {
#if ANDROID
        var shown = _shown;
        _shown = null;
        if (shown is null)
        {
            return;
        }

        try
        {
            shown.Close(fade);
        }
#pragma warning disable CA1031 // Reported by code; she is unlocked either way, or its window is already gone.
        catch (Exception ex)
        {
            _errors.Report(ex, "lock.hide");
        }
#pragma warning restore CA1031
#else
        _ = fade;
#endif
    }

    /// <summary>
    /// Hides or shows Maia's own pages (the lock screen is in a window of its own, on top). Shell's Android view doesn't
    /// follow the page's Opacity, so there its view is hidden itself, fully or not at all.
    /// </summary>
    private void Cover(bool cover)
    {
        if (_window?.Page is not { } root)
        {
            return;
        }

        root.Opacity = cover ? 0 : 1;
#if ANDROID
        if (root.Handler?.PlatformView is global::Android.Views.View view)
        {
            view.Alpha = cover ? 0 : 1;
        }
#endif
    }

    private void ApplyScreenPrivacy()
    {
#if ANDROID
        Platforms.Android.ScreenPrivacy.Apply(_window?.Handler?.PlatformView as global::Android.App.Activity ?? Platform.CurrentActivity, _lock.IsOn);
#endif
    }
}


/// <summary>Where Maia can't ask for the phone's screen lock yet (iOS and Windows for now): the lock can't be turned on.</summary>
public sealed class NoScreenLock : IScreenLock
{
    public bool IsSet => false;

    public Task<bool> ConfirmAsync(string title, string subtitle) => Task.FromResult(false);

    public void OpenSettings()
    {
    }
}
