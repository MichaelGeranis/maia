using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Common;

/// <summary>How a screen's load went: loaded, or the calm line to show and whether the start-up screen should take over.</summary>
/// <param name="Message">Null once loaded; otherwise what to tell her.</param>
/// <param name="NeedsStartup">
/// Her plan is stored but can't be opened (its key is gone, it is damaged, or a newer Maia saved it): the start-up
/// screen says so and offers the ways on, which no other screen can.
/// </param>
public sealed record LoadResult(string? Message, bool NeedsStartup)
{
    public static readonly LoadResult Loaded = new(null, NeedsStartup: false);

    public bool IsLoaded => Message is null;
}

/// <summary>
/// Loads (or reloads) a screen without ever ending Maia: a failure is reported by operation code and exception type
/// only (never anything from her plan) and becomes a calm line for her. Page events, and a screen's own reload after an
/// action, go through here.
/// </summary>
public static class ScreenLoad
{
    public static async Task<LoadResult> RunAsync(Func<Task> load, string operation, IErrorReporter errors)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(errors);
        try
        {
            await load();
            return LoadResult.Loaded;
        }
        catch (PlanUnreadableException ex)
        {
            errors.Report(ex, operation);
            return new LoadResult(ErrorMessages.For(ex), NeedsStartup: true);
        }
#pragma warning disable CA1031 // A screen that can't load must not end Maia: the reporter gets the code and type.
        catch (Exception ex)
        {
            errors.Report(ex, operation);
            return new LoadResult(Text.Get("Common_LoadError"), NeedsStartup: false);
        }
#pragma warning restore CA1031
    }

    /// <summary>A screen's reload after an action: null once loaded, otherwise the line to show.</summary>
    public static async Task<string?> ReloadAsync(Func<Task> load, string operation, IErrorReporter errors) =>
        (await RunAsync(load, operation, errors)).Message;
}
