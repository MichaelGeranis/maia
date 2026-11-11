using Maia.Application.Abstractions;
using Maia.Presentation.Navigation;
using Maia.Presentation.Startup;

namespace Maia.Presentation.Common;

/// <summary>
/// Runs a screen's load, and any other work, from a page event (appearing, a property change, a tap) in place of
/// <c>async void</c>: a failure never ends Maia. It is reported by operation code and exception type only. A load that
/// fails shows a calm line, and a plan that can't be opened hands over to the start-up screen, which alone offers the
/// ways on. After Maia opened straight on Your week, it also finishes start-up (see <see cref="StartupFlow"/>): once
/// Your week has loaded, her plan follows the app's language; if it can't load, the start-up screen takes over.
/// </summary>
public sealed class ScreenLoader
{
    private readonly StartupFlow _startup;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;

    public ScreenLoader(StartupFlow startup, INavigator navigator, INotifier notifier, IErrorReporter errors)
    {
        _startup = startup;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
    }

    /// <summary>Loads a screen from a page event. Never throws.</summary>
    /// <param name="load">The view model's load.</param>
    /// <param name="operation">What a failure is reported as, for example "team.load".</param>
    /// <returns>Whether the screen loaded.</returns>
    public async Task<bool> LoadAsync(Func<Task> load, string operation)
    {
        var result = await ScreenLoad.RunAsync(load, operation, _errors);
        try
        {
            if (result.IsLoaded)
            {
                // Opened straight on Your week: her plan now follows the app's language. Renamed, the screen shows it so.
                if (await _startup.LoadedAsync() && await ScreenLoad.ReloadAsync(load, operation, _errors) is { } message)
                {
                    _notifier.ShowNotice(message);
                }
            }
            else if (_startup.Failed() || result.NeedsStartup)
            {
                // Her first screen, or a plan that can't be opened: the start-up screen says why and offers the ways on.
                await _navigator.GoToStartupAsync();
            }
            else
            {
                _notifier.ShowNotice(result.Message!);
            }
        }
#pragma warning disable CA1031 // The last line of defence for a page event: report by code, never end Maia.
        catch (Exception ex)
        {
            _errors.Report(ex, operation);
        }
#pragma warning restore CA1031

        return result.IsLoaded;
    }

    /// <summary>
    /// Runs other work from a page event (putting the keyboard away, an animation). Never throws: a failure is only
    /// reported, by code.
    /// </summary>
    public async Task RunAsync(Func<Task> work, string operation)
    {
        ArgumentNullException.ThrowIfNull(work);
        try
        {
            await work();
        }
#pragma warning disable CA1031 // Polish that fails (an animation, the keyboard) is reported by code and never ends Maia.
        catch (Exception ex)
        {
            _errors.Report(ex, operation);
        }
#pragma warning restore CA1031
    }
}
