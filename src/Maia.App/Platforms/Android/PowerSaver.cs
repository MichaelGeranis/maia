using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using Maia.App.Services;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Battery Saver, as Maia's ambient loops need it: read once, then kept up to date by the phone's broadcast, so a loop
/// can ask twenty times a second without asking the system each time.
/// </summary>
internal static class PowerSaver
{
    private static readonly Lock Gate = new();
    private static bool? _isOn;

    public static bool IsOn
    {
        get
        {
            lock (Gate)
            {
                if (_isOn is { } known)
                {
                    return known;
                }

                var context = global::Android.App.Application.Context;
                ContextCompat.RegisterReceiver(context, new Receiver(), new IntentFilter(PowerManager.ActionPowerSaveModeChanged), ContextCompat.ReceiverNotExported);
                _isOn = Read(context);
                return _isOn.Value;
            }
        }
    }

    private static bool Read(Context context) => (context.GetSystemService(Context.PowerService) as PowerManager)?.IsPowerSaveMode == true;

    private sealed class Receiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context is null)
            {
                return;
            }

            lock (Gate)
            {
                _isOn = Read(context);
            }

            Motion.OnChanged();
        }
    }
}
