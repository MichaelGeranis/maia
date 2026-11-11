using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Privacy;

namespace Maia.Presentation.Settings;

/// <summary>
/// "Lock Maia with your screen lock" in Settings (off by default). Turning it on asks for the phone's screen lock once,
/// so she knows it opens Maia. A phone without a screen lock can't lock Maia: the card says so calmly, in Sand, and
/// offers the phone's settings, and the switch stays off.
/// </summary>
public sealed partial class AppLockSettings : ObservableObject
{
    private readonly AppLock _lock;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private bool _changing;

    public AppLockSettings(AppLock appLock, INotifier notifier, IErrorReporter errors)
    {
        _lock = appLock;
        _notifier = notifier;
        _errors = errors;
        Refresh();
    }

    /// <summary>The switch. Turning it on waits for her screen lock; until then it shows what is really set.</summary>
    public bool IsOn
    {
        get => _lock.IsOn;
        set
        {
            if (value != _lock.IsOn && !_changing)
            {
                _ = ChangeAsync(value);
            }
        }
    }

    /// <summary>Why the lock can't work on this phone yet (no screen lock), or null.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>Looks again, for example when she comes back from setting a screen lock in the phone's settings.</summary>
    public void Refresh()
    {
        Notice = _lock.CanLock ? null : Text.Get("Settings_LockNeedsScreenLock");
        OnPropertyChanged(nameof(IsOn));
    }

    [RelayCommand]
    private void OpenPhoneSettings() => _lock.OpenScreenLockSettings();

    /// <summary>The switch's change, finished: on after she confirms her screen lock, off at once.</summary>
    internal async Task ChangeAsync(bool on)
    {
        _changing = true;
        try
        {
            string? done = null;
            var error = await PlanChange.RunAsync(
                async () =>
                {
                    if (!on)
                    {
                        _lock.TurnOff();
                        done = "Toast_LockOff";
                    }
                    else if (await _lock.TurnOnAsync())
                    {
                        done = "Toast_LockOn";
                    }
                },
                "settings.lock",
                _errors);
            if (error is not null)
            {
                _notifier.ShowNotice(error);
            }
            else if (done is not null)
            {
                _notifier.Show(Text.Get(done));
            }
        }
        finally
        {
            _changing = false;
            Refresh();
        }
    }
}
