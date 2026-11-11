using Maia.Presentation.Calm;

namespace Maia.App.Services;

/// <summary>Keeps the screen on through the phone's display settings (a window flag on Android).</summary>
public sealed class DeviceScreenAwake : IScreenAwake
{
    public void KeepOn(bool keepOn) => MainThread.BeginInvokeOnMainThread(() => DeviceDisplay.Current.KeepScreenOn = keepOn);
}

/// <summary>Speaks through TalkBack (or VoiceOver) when it is on; does nothing otherwise.</summary>
public sealed class ScreenReaderAnnouncer : IAnnouncer
{
    public void Announce(string text) => MainThread.BeginInvokeOnMainThread(() => Controls.Accessible.Say(text));
}
