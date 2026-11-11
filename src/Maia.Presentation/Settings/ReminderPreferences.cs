using Maia.Application.Abstractions;

namespace Maia.Presentation.Settings;

/// <summary>The phone's preferences, as far as reminder choices need them. Implemented by the app.</summary>
public interface IPreferenceValues
{
    bool Has(string key);

    int ReadInt(string key, int fallback);

    bool ReadBool(string key, bool fallback);

    void WriteInt(string key, int value);

    void WriteBool(string key, bool value);
}

/// <summary>
/// Whether she turned reminders on this phone on, and her choices for them, in device preferences: one key each, so a key
/// an earlier build never wrote reads as its default. "Remind me" and discreet titles were once kept with calendar sync,
/// under <c>calendar.reminders</c> and <c>calendar.discreet_titles</c>: until she chooses again, those are read instead,
/// so a phone keeps the choices she made then.
/// </summary>
public sealed class ReminderPreferences : IReminderSettingsStore
{
    public const string OnKey = "reminders.on";
    public const string WhenKey = "reminders.when";
    public const string DiscreetKey = "reminders.discreet";
    public const string OldWhenKey = "calendar.reminders";
    public const string OldDiscreetKey = "calendar.discreet_titles";

    private readonly IPreferenceValues _values;

    public ReminderPreferences(IPreferenceValues values)
    {
        _values = values;
    }

    public Task<bool> IsOnAsync(CancellationToken cancellationToken) => Task.FromResult(_values.ReadBool(OnKey, false));

    public Task SetOnAsync(bool enabled, CancellationToken cancellationToken)
    {
        _values.WriteBool(OnKey, enabled);
        return Task.CompletedTask;
    }

    public Task<ReminderSettings> GetAsync(CancellationToken cancellationToken)
    {
        var defaults = ReminderSettings.Default;
        var when = (ReminderPreference)_values.ReadInt(_values.Has(WhenKey) ? WhenKey : OldWhenKey, (int)defaults.When);
        return Task.FromResult(new ReminderSettings
        {
            When = Enum.IsDefined(when) ? when : defaults.When,
            DiscreetTitles = _values.ReadBool(_values.Has(DiscreetKey) ? DiscreetKey : OldDiscreetKey, defaults.DiscreetTitles),
        });
    }

    public Task SaveAsync(ReminderSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _values.WriteInt(WhenKey, (int)settings.When);
        _values.WriteBool(DiscreetKey, settings.DiscreetTitles);
        return Task.CompletedTask;
    }
}
