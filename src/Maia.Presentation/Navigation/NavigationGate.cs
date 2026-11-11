using Maia.Application.Abstractions;

namespace Maia.Presentation.Navigation;

/// <summary>
/// One navigation at a time. A tap (the bottom bar, a back arrow) while another navigation is on its way is dropped, so
/// a quick double tap never pushes a screen twice; a screen asking to go somewhere waits its turn instead. Going where
/// she already is does nothing (Add on the Add screen, Settings on Settings). A navigation that fails is reported by code
/// and never ends Maia.
/// </summary>
public sealed class NavigationGate : IDisposable
{
    /// <summary>The route that goes back one screen: never "already there".</summary>
    public const string Back = "..";

    /// <summary>How long a screen's navigation waits for one on its way before it goes anyway.</summary>
    public static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IErrorReporter _errors;

    public NavigationGate(IErrorReporter errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _errors = errors;
    }

    /// <summary>For a tap: goes unless another navigation is on its way or she is already there.</summary>
    /// <param name="route">A Shell route, relative ("add") or absolute ("//main/plan").</param>
    /// <param name="location">Where she is now (the Shell location).</param>
    /// <param name="go">Performs the navigation.</param>
    /// <returns>Whether it went.</returns>
    public async Task<bool> TapAsync(string route, Func<string?> location, Func<Task> go)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (!_gate.Wait(0))
        {
            return false;
        }

        try
        {
            return await GoOnceAsync(route, location(), go);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>For a screen: waits for a navigation on its way, then goes unless she is already there.</summary>
    /// <returns>Whether it went.</returns>
    public async Task<bool> GoAsync(string route, Func<string?> location, Func<Task> go)
    {
        ArgumentNullException.ThrowIfNull(location);
        var entered = await _gate.WaitAsync(WaitLimit);
        try
        {
            return await GoOnceAsync(route, location(), go);
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="location"/> is already the screen <paramref name="route"/> leads to: its last part is the
    /// route's last part ("//main/plan/add" for "add", "//main/week" for "//main/week"). A screen pushed
    /// over a tab ("//main/settings/about") is not the tab, so tapping the tab still takes her back to it.
    /// </summary>
    public static bool IsAt(string? location, string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (string.IsNullOrEmpty(location) || route == Back)
        {
            return false;
        }

        var target = LastPart(route);
        return target.Length > 0 && string.Equals(LastPart(location), target, StringComparison.Ordinal);
    }

    /// <summary>The operation a failed navigation is reported under: "nav." and the route's last part ("nav.back" for back).</summary>
    public static string Operation(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return "nav." + (route == Back ? "back" : LastPart(route));
    }

    public void Dispose() => _gate.Dispose();

    private static string LastPart(string route)
    {
        var trimmed = route.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    private async Task<bool> GoOnceAsync(string route, string? location, Func<Task> go)
    {
        ArgumentNullException.ThrowIfNull(go);
        if (IsAt(location, route))
        {
            return false;
        }

        try
        {
            await go();
            return true;
        }
#pragma warning disable CA1031 // A navigation that fails leaves her where she is; the reporter gets the code and type.
        catch (Exception ex)
        {
            _errors.Report(ex, Operation(route));
            return false;
        }
#pragma warning restore CA1031
    }
}
