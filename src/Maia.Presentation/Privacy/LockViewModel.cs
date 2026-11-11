using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Privacy;

/// <summary>
/// The lock screen that covers Maia while it is locked (<see cref="AppLock"/>). It asks for the phone's screen lock as
/// it shows; if she cancels, it stays calmly locked until she taps Unlock.
/// </summary>
public sealed partial class LockViewModel : ObservableObject
{
    private readonly AppLock _lock;

    public LockViewModel(AppLock appLock)
    {
        _lock = appLock;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>After a cancelled or failed attempt: that Maia stays locked, and how to try again.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    private bool CanUnlock() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        IsBusy = true;
        try
        {
            Notice = null;
            if (!await _lock.UnlockAsync())
            {
                Notice = Text.Get("Lock_NotUnlocked");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
