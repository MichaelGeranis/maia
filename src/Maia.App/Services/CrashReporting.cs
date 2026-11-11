using Maia.Application.Abstractions;

namespace Maia.App.Services;

/// <summary>
/// Reports failures nothing else caught, with a fixed operation and the exception types only (a message could name a
/// check, and health data never reaches logs or reports). They go to the problem log on this phone, which is kept in
/// Release builds too. An unhandled exception still ends the app, once reported; a task failure nobody awaited is
/// reported and then marked as seen.
/// </summary>
public static class CrashReporting
{
    /// <summary>Starts the operation of a failure that ends Maia: its line goes to the problem log at once.</summary>
    public const string OperationPrefix = "app.unhandled";

    public static void Register(IErrorReporter errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                errors.Report(exception, OperationPrefix);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            errors.Report(e.Exception, "app.unobserved_task");
            e.SetObserved();
        };

#if ANDROID
        // A managed exception that reaches Android's main loop (from an event handler, say) comes here first.
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => errors.Report(e.Exception, OperationPrefix + "_android");
#endif
    }
}
