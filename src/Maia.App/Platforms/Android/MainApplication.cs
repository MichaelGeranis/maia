using Android.App;
using Android.Runtime;

namespace Maia.App;

// Fully qualified: inside Maia.App, "Application" would resolve to the Maia.Application namespace.
[global::Android.App.Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
