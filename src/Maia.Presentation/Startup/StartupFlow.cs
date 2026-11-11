using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Presentation.Localization;
using Maia.Presentation.Results;

namespace Maia.Presentation.Startup;

/// <summary>
/// How Maia starts. With her plan stored on this phone and nothing else to do first, Maia opens straight on Your
/// week: no start-up screen and no extra navigation, and Your week's own load is the only plan load before it shows.
/// Once it has loaded, her plan follows the app's language, the rest of start-up brings it up to date (see
/// <see cref="PlanUpkeep"/>; Your week loads again only if her checks now read differently) and result files her plan no
/// longer points at are tidied away. Everything else goes through the start-up screen (<see cref="StartupViewModel"/>):
/// no plan yet, a share to file, coming back after a language switch, Maia starting locked (the start-up screen opens
/// her plan only once she has unlocked), and a plan that can't be opened, whose recovery states only it offers. If Your
/// week can't load, the start-up screen takes over.
/// </summary>
public sealed class StartupFlow
{
    private readonly Func<bool> _planStored;
    private readonly ShareIntake _shares;
    private readonly PlanLanguageSync _language;
    private readonly PlanUpkeep _upkeep;
    private readonly PlanService _plans;
    private readonly IErrorReporter _errors;
    private bool _pending;

    /// <param name="planStored">Whether a plan file is on this phone, readable or not: a quick look, no decrypting.</param>
    /// <param name="shares">Files another app shared as Maia started: the start-up screen takes her to file them.</param>
    /// <param name="language">Brings her plan into the app's language once Your week has loaded.</param>
    /// <param name="upkeep">Brings her plan up to date once Your week has loaded.</param>
    /// <param name="plans">Tidies result files her plan no longer points at.</param>
    /// <param name="errors">Receives codes (never contents) when a tidy fails.</param>
    public StartupFlow(Func<bool> planStored, ShareIntake shares, PlanLanguageSync language, PlanUpkeep upkeep, PlanService plans, IErrorReporter errors)
    {
        _planStored = planStored;
        _shares = shares;
        _language = language;
        _upkeep = upkeep;
        _plans = plans;
        _errors = errors;
    }

    /// <summary>The background tidy of result files started once Your week first loaded.</summary>
    public Task Tidying { get; private set; } = Task.CompletedTask;

    /// <summary>Whether Your week has yet to report its first load after Maia opened straight on it.</summary>
    public bool IsPending => _pending;

    /// <summary>
    /// Chooses the first screen, as the app's window is made: true to open straight on Your week, false for the
    /// start-up screen.
    /// </summary>
    /// <param name="returning">Maia is being redrawn in another language and goes back where she was.</param>
    /// <param name="locked">
    /// Maia starts locked ("Lock Maia"): the start-up screen waits behind the lock screen and opens her plan only once she
    /// has unlocked, so nothing of it is loaded or drawn before.
    /// </param>
    public bool OpensOnWeek(bool returning, bool locked = false)
    {
        _pending = !returning && !locked && _shares.Pending.Count == 0 && _planStored();
        return _pending;
    }

    /// <summary>
    /// Call after a screen loaded. The first time, after opening straight on Your week, her plan follows the app's
    /// language, is brought up to date (<see cref="PlanUpkeep"/>) and the tidy starts; any other time it does nothing.
    /// </summary>
    /// <returns>Whether her checks were renamed or reworded, so the screen should load again.</returns>
    public async Task<bool> LoadedAsync()
    {
        if (!_pending)
        {
            return false;
        }

        _pending = false;
        var renamed = await _language.ApplyAsync();
        var reworded = await _upkeep.RunAsync();
        Tidying = Task.Run(() => TidyAsync(_plans, _errors));
        return renamed || reworded;
    }

    /// <summary>
    /// Call when a screen could not load. After opening straight on Your week, the start-up screen takes over (it says
    /// what happened and offers the way on, and does the rest of start-up itself).
    /// </summary>
    /// <returns>Whether the start-up screen should now show.</returns>
    public bool Failed()
    {
        var pending = _pending;
        _pending = false;
        return pending;
    }

    /// <summary>
    /// Deletes result files her plan does not point at, after a clean load only. Best effort: a failure is reported by
    /// code and tried again next start.
    /// </summary>
    internal static async Task TidyAsync(PlanService plans, IErrorReporter errors)
    {
        try
        {
            await plans.RemoveOrphanedFilesAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Tidying is best effort; a failure is reported by code and tried again next start.
        catch (Exception ex)
        {
            errors.Report(ex, "results.tidy");
        }
#pragma warning restore CA1031
    }
}
