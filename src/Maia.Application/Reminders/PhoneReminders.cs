using Maia.Application.Abstractions;
using Maia.Application.Plans;

namespace Maia.Application.Reminders;

/// <summary>
/// Keeps the reminders on this phone in step with the plan, when she has turned them on (Settings, or setup step 3).
/// They are Maia's own notifications: Maia writes nothing to the phone's calendar. Every piece of work sets all of them
/// again from the latest saved plan (see <see cref="ReminderPlanner"/>): after each saved change, when her reminder
/// choices change, and when the app starts.
/// Turned off, or with no plan, every reminder is cancelled.
/// </summary>
/// <remarks>
/// Work runs off the UI thread, one piece at a time; changes that arrive while one runs are folded into a single next
/// run. Nothing here fails her action: a failure is reported by code, never with anything from the plan.
/// </remarks>
public sealed class PhoneReminders
{
    private readonly IReminderScheduler _scheduler;
    private readonly IReminderSettingsStore _settings;
    private readonly IPlanRepository _plans;
    private readonly IClock _clock;
    private readonly IErrorReporter _errors;
    private readonly Lock _sync = new();
    private Task? _running;
    private bool _dirty;

    public PhoneReminders(
        IReminderScheduler scheduler,
        IReminderSettingsStore settings,
        IPlanRepository plans,
        IClock clock,
        IErrorReporter errors)
    {
        _scheduler = scheduler;
        _settings = settings;
        _plans = plans;
        _clock = clock;
        _errors = errors;
    }

    public Task<bool> IsOnAsync(CancellationToken cancellationToken = default) => _settings.IsOnAsync(cancellationToken);

    /// <summary>Her reminder choices: when, and discreet titles.</summary>
    public Task<ReminderSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => _settings.GetAsync(cancellationToken);

    /// <summary>Saves her reminder choices and sets every reminder again with them.</summary>
    public async Task SaveSettingsAsync(ReminderSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        await RearmAsync().ConfigureAwait(false);
    }

    /// <summary>Turns reminders on this phone on (and sets them) or off (and cancels every one).</summary>
    public async Task SetOnAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await _settings.SetOnAsync(enabled, cancellationToken).ConfigureAwait(false);
        await RearmAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets every reminder again from the latest saved plan, in the background. The returned task completes once this
    /// change has reached the phone; it never throws.
    /// </summary>
    public Task RearmAsync()
    {
        lock (_sync)
        {
            _dirty = true;
            return _running ??= Task.Run(RunAsync);
        }
    }

    /// <summary>Completes once every change asked for so far has reached the phone.</summary>
    public Task WhenIdleAsync()
    {
        lock (_sync)
        {
            return _running ?? Task.CompletedTask;
        }
    }

    private async Task RunAsync()
    {
        while (true)
        {
            lock (_sync)
            {
                if (!_dirty)
                {
                    _running = null;
                    return;
                }

                _dirty = false;
            }

            await ArmAsync().ConfigureAwait(false);
        }
    }

    private async Task ArmAsync()
    {
#pragma warning disable CA1031 // Reminders never fail her action; a failure is reported by code and the next change or start tries again.
        try
        {
            var plan = await _settings.IsOnAsync(CancellationToken.None).ConfigureAwait(false)
                ? await _plans.LoadAsync(CancellationToken.None).ConfigureAwait(false)
                : null;
            if (plan is null)
            {
                await _scheduler.ReplaceAllAsync([], CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var settings = await _settings.GetAsync(CancellationToken.None).ConfigureAwait(false);
            var reminders = ReminderPlanner.For(plan, settings, _clock.UtcNow, HomeTimeZone.Of(plan, _clock));
            await _scheduler.ReplaceAllAsync(reminders, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "reminders.set");
        }
#pragma warning restore CA1031
    }
}
