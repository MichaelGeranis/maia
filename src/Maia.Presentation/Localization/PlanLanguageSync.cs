using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Presentation.Common;

namespace Maia.Presentation.Localization;

/// <summary>
/// Keeps the plan's language in step with the language Maia shows. Check titles and her reminders follow the
/// plan's language, while notes, toasts and every other text follow the app's. When the phone's language changed and
/// she never picked one in Maia (or a backup came from a phone in another language), the two would drift apart, so
/// at start-up the plan follows the app.
/// </summary>
public sealed class PlanLanguageSync
{
    private readonly PlanService _plans;
    private readonly IErrorReporter _errors;

    public PlanLanguageSync(PlanService plans, IErrorReporter errors)
    {
        _plans = plans;
        _errors = errors;
    }

    /// <summary>
    /// Renames her checks (and her reminders) into the app's language if the plan is in another one. Nothing is
    /// saved when they already match. A failure is reported by code and never stops the app from opening.
    /// </summary>
    /// <returns>Whether her checks were renamed, so a screen already showing them should load again.</returns>
    public async Task<bool> ApplyAsync()
    {
        string? planLanguage = null;
        _ = await PlanChange.RunAsync(
            async () =>
            {
                try
                {
                    planLanguage = (await _plans.GetPlanAsync()).Language;
                }
                catch (PlanNotFoundException)
                {
                    // No plan yet: setup creates it in the app's language.
                }
            },
            "startup.language",
            _errors);
        return await ApplyAsync(planLanguage);
    }

    /// <summary>As <see cref="ApplyAsync()"/>, for a caller that has just read the plan's language: nothing is loaded again when it matches.</summary>
    /// <param name="planLanguage">The language her checks are named in; null without a plan.</param>
    public async Task<bool> ApplyAsync(string? planLanguage)
    {
        var language = Text.Language;
        if (planLanguage is null || planLanguage == language)
        {
            return false;
        }

        var failed = await PlanChange.RunAsync(() => _plans.ChangeLanguageAsync(language), "startup.language", _errors);
        return failed is null;
    }
}
