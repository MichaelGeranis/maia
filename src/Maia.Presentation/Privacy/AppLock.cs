using CommunityToolkit.Mvvm.ComponentModel;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Privacy;

/// <summary>The phone's own screen lock: its PIN, pattern or password, or a fingerprint or face backed by one. Implemented by the app.</summary>
public interface IScreenLock
{
    /// <summary>Whether the phone has a screen lock. Without one Maia can't be locked, since nothing could open it again.</summary>
    bool IsSet { get; }

    /// <summary>Asks for the phone's screen lock, in the phone's own prompt.</summary>
    /// <returns>True once she confirmed it; false when she cancelled or it didn't work.</returns>
    Task<bool> ConfirmAsync(string title, string subtitle);

    /// <summary>Opens the phone's security settings, where she can set a screen lock.</summary>
    void OpenSettings();
}

/// <summary>Where "Lock Maia" is remembered. Off unless she turns it on. A setting, not health data.</summary>
public interface IAppLockPreference
{
    bool IsOn { get; set; }
}

/// <summary>
/// "Lock Maia with your screen lock" (off by default). With it on, Maia is locked when it starts and when it comes back
/// after more than <see cref="GracePeriod"/> away, and only the phone's own screen lock opens it again. Maia never sees
/// or keeps the PIN: the phone asks for it. Time away is measured on a clock the phone's own clock setting can't move.
/// If the phone no longer has a screen lock, Maia doesn't lock (nothing could open it); Settings says why.
/// </summary>
public sealed partial class AppLock : ObservableObject
{
    /// <summary>How long Maia may be in the background and still open without asking.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(1);

    private readonly IAppLockPreference _preference;
    private readonly IScreenLock _screenLock;
    private readonly TimeProvider _time;
    private long? _leftAt;
    private bool _confirming;
    private TaskCompletionSource? _unlocked;

    public AppLock(IAppLockPreference preference, IScreenLock screenLock, TimeProvider time)
    {
        _preference = preference;
        _screenLock = screenLock;
        _time = time;
    }

    /// <summary>Whether she turned the lock on.</summary>
    public bool IsOn => _preference.IsOn;

    /// <summary>Whether the phone has a screen lock Maia can ask for.</summary>
    public bool CanLock => _screenLock.IsSet;

    /// <summary>Whether Maia is locked right now: the app covers everything with the lock screen.</summary>
    [ObservableProperty]
    public partial bool IsLocked { get; private set; }

    private bool ShouldLock => IsOn && CanLock;

    /// <summary>
    /// Whether Maia will be locked when it starts: known before its first screen is chosen, so that screen never loads
    /// or draws her plan before she has unlocked.
    /// </summary>
    public bool LocksOnStart => ShouldLock;

    /// <summary>Maia starts: locked from the first screen when the lock is on.</summary>
    public void Start()
    {
        _leftAt = null;
        IsLocked = ShouldLock;
    }

    /// <summary>Maia went to the background. Her own trip to the phone's lock prompt doesn't count.</summary>
    /// <returns>
    /// Whether the app should hide its pages now: the lock is on and this time away counts, so coming back may need the
    /// lock screen, and her plan must not show for a moment before it.
    /// </returns>
    public bool Leave()
    {
        if (_confirming || IsLocked)
        {
            return false;
        }

        _leftAt = _time.GetTimestamp();
        return ShouldLock;
    }

    /// <summary>Completes once Maia is open: at once when it isn't locked. Start-up waits on it before opening her plan.</summary>
    public Task WhenUnlockedAsync() =>
        IsLocked ? (_unlocked ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task : Task.CompletedTask;

    partial void OnIsLockedChanged(bool value)
    {
        if (!value)
        {
            _unlocked?.TrySetResult();
            _unlocked = null;
        }
    }

    /// <summary>Maia is back in front: locked when it was away for longer than <see cref="GracePeriod"/>.</summary>
    public void Return()
    {
        if (_confirming || _leftAt is not { } leftAt)
        {
            return;
        }

        _leftAt = null;
        if (ShouldLock && _time.GetElapsedTime(leftAt) > GracePeriod)
        {
            IsLocked = true;
        }
    }

    /// <summary>Asks for the phone's screen lock and opens Maia once she confirms it.</summary>
    /// <returns>Whether Maia is open.</returns>
    public async Task<bool> UnlockAsync()
    {
        if (!IsLocked)
        {
            return true;
        }

        if (!CanLock)
        {
            // Her screen lock was removed while Maia was locked: there is nothing left to ask for.
            IsLocked = false;
            return true;
        }

        if (await ConfirmAsync(Text.Get("Lock_PromptTitle")))
        {
            IsLocked = false;
        }

        return !IsLocked;
    }

    /// <summary>Turns the lock on once she has confirmed her screen lock, so she knows it opens Maia.</summary>
    /// <returns>Whether the lock is now on.</returns>
    public async Task<bool> TurnOnAsync()
    {
        if (IsOn)
        {
            return true;
        }

        if (!CanLock || !await ConfirmAsync(Text.Get("Lock_TurnOnTitle")))
        {
            return false;
        }

        _preference.IsOn = true;
        OnPropertyChanged(nameof(IsOn));
        return true;
    }

    public void TurnOff()
    {
        if (!IsOn)
        {
            return;
        }

        _preference.IsOn = false;
        IsLocked = false;
        OnPropertyChanged(nameof(IsOn));
    }

    public void OpenScreenLockSettings() => _screenLock.OpenSettings();

    private async Task<bool> ConfirmAsync(string title)
    {
        _confirming = true;
        try
        {
            return await _screenLock.ConfirmAsync(title, Text.Get("Lock_PromptSubtitle"));
        }
        finally
        {
            _confirming = false;
            _leftAt = null;
        }
    }
}
