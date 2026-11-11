using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Setup;

namespace Maia.Presentation.Startup;

/// <summary>
/// What start-up does once her plan has opened and she is on her way, whichever way Maia started (straight on Your
/// week, or through the start-up screen): a plan saved before Maia kept a home time zone takes the phone's, her plan
/// takes the wording of a newer template this build ships (ADR 0003) and she is told calmly, and the choices setup kept
/// go. It runs after her first screen, so her plan is loaded only once before that screen shows. Each step is best
/// effort: a failure is reported by code and never stops her plan opening.
/// </summary>
public sealed class PlanUpkeep
{
    private readonly PlanService _plans;
    private readonly SetupSession _setup;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;

    public PlanUpkeep(PlanService plans, SetupSession setup, INotifier notifier, IErrorReporter errors)
    {
        _plans = plans;
        _setup = setup;
        _notifier = notifier;
        _errors = errors;
    }

    /// <summary>Brings her opened plan up to date. Never throws.</summary>
    /// <returns>Whether her checks now read differently, so a screen already showing them should load again.</returns>
    public async Task<bool> RunAsync()
    {
        await KeepHomeTimeZoneAsync();
        var reworded = await UpdateFromTemplateAsync();

        // Setup is over once her plan exists: nothing it kept is needed any more.
        await _setup.ClearAsync();

        // After her first screen, so the toast shows on the screen she lands on.
        if (reworded)
        {
            _notifier.Show(Text.Get("Toast_PlanWordingUpdated"));
        }

        return reworded;
    }

    /// <summary>A plan saved before Maia kept a home time zone takes the phone's, once. Tried again next start.</summary>
    private async Task KeepHomeTimeZoneAsync()
    {
        try
        {
            await _plans.KeepHomeTimeZoneAsync();
        }
#pragma warning disable CA1031 // Her plan opens either way; the failure is reported by code and it is tried again next start.
        catch (Exception ex)
        {
            _errors.Report(ex, "startup.time_zone");
        }
#pragma warning restore CA1031
    }

    /// <returns>Whether any check reads differently.</returns>
    private async Task<bool> UpdateFromTemplateAsync()
    {
        var updated = false;
        _ = await PlanChange.RunAsync(async () => updated = await _plans.UpdateFromTemplateAsync(), "startup.template", _errors);
        return updated;
    }
}
