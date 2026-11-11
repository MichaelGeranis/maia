using Maia.Application.Abstractions;

namespace Maia.Testing;

/// <summary>
/// The phone's alarms for Maia's reminders, as the Android adapter keeps them: setting them replaces every one set
/// before, and what survives a restart is only the times and the plain words.
/// </summary>
public sealed class FakeReminderScheduler : IReminderScheduler
{
    /// <summary>The reminders set on the phone now, soonest first.</summary>
    public IReadOnlyList<PhoneReminder> Set { get; private set; } = [];

    /// <summary>What is kept for after a restart: when, and the words without names.</summary>
    public IReadOnlyList<(DateTimeOffset At, string Title, string Text, string LockScreen)> Kept =>
        [.. Set.Select(r => (r.At, r.DiscreetTitle, r.DiscreetText, r.LockScreenTitle))];

    /// <summary>How many times every reminder was set again.</summary>
    public int Replaced { get; private set; }

    /// <summary>Makes the next call throw this, as when the phone refuses.</summary>
    public Exception? FailNext { get; set; }

    public Task ReplaceAllAsync(IReadOnlyList<PhoneReminder> reminders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        if (FailNext is { } failure)
        {
            FailNext = null;
            throw failure;
        }

        Set = [.. reminders];
        Replaced++;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryReminderSettingsStore : IReminderSettingsStore
{
    public bool IsOn { get; set; }

    public ReminderSettings Settings { get; set; } = ReminderSettings.Default;

    /// <summary>Makes the next save of her choices throw this.</summary>
    public Exception? FailNextSave { get; set; }

    public Task<bool> IsOnAsync(CancellationToken cancellationToken) => Task.FromResult(IsOn);

    public Task SetOnAsync(bool enabled, CancellationToken cancellationToken)
    {
        IsOn = enabled;
        return Task.CompletedTask;
    }

    public Task<ReminderSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task SaveAsync(ReminderSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (FailNextSave is { } failure)
        {
            FailNextSave = null;
            throw failure;
        }

        Settings = settings;
        return Task.CompletedTask;
    }
}
