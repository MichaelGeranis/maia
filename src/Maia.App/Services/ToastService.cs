using Maia.Presentation.Navigation;

namespace Maia.App.Services;

/// <summary>One toast: its words, and whether it is a calm notice (Sand, no tick) rather than a confirmation.</summary>
public sealed record Toast(string Message, bool IsNotice);

/// <summary>
/// Carries toasts from view models to the toast on whichever screen is visible. A toast raised just as the screen
/// changes (for example "Thyroid test added to your journey…" on the way to Journey) waits briefly for the new screen
/// to take it.
/// </summary>
public sealed class ToastService : INotifier
{
    private static readonly TimeSpan PendingFor = TimeSpan.FromSeconds(2);
    private Toast? _pending;
    private DateTime _pendingSince;

    public event EventHandler<Toast>? Shown;

    public void Show(string message) => Raise(new Toast(message, IsNotice: false));

    public void ShowNotice(string message) => Raise(new Toast(message, IsNotice: true));

    /// <summary>Called by the toast that displayed the message, so no other screen repeats it.</summary>
    public void MarkShown() => _pending = null;

    /// <summary>A toast nobody has displayed yet, if it is still fresh.</summary>
    public Toast? TakePending()
    {
        var toast = _pending is not null && DateTime.UtcNow - _pendingSince < PendingFor ? _pending : null;
        _pending = null;
        return toast;
    }

    private void Raise(Toast toast) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _pending = toast;
            _pendingSince = DateTime.UtcNow;
            Shown?.Invoke(this, toast);
        });
}
