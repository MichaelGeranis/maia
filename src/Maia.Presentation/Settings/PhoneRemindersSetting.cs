using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Reminders;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Settings;

/// <summary>The phone's permission to show Maia's notifications. Implemented by the app.</summary>
public interface INotificationAccess
{
    /// <summary>Whether this phone can show Maia's reminders (Android for now); elsewhere the switch is not offered.</summary>
    bool IsSupported { get; }

    /// <summary>Whether Maia may show notifications right now, without asking.</summary>
    Task<bool> IsGrantedAsync();

    /// <summary>Asks for it if needed (Android 13 and later ask; before, it is granted). Returns whether it was granted.</summary>
    Task<bool> RequestAsync();

    /// <summary>Opens the phone's settings for Maia, where refused notifications can be turned back on.</summary>
    void OpenAppSettings();
}

/// <summary>
/// The Reminders card in Settings: Maia's own notifications on this phone for bookings and timed tasks, 24 hours
/// before and/or 2 hours before as she chooses ("Remind me"), and when it's time to book. Turning them on asks for
/// notifications only then; if she says no, they stay off and a calm note says where to allow them. Turning them off
/// cancels every reminder. "Discreet lock screen" keeps names out of the notifications. Every change sets the
/// reminders again, in order.
/// </summary>
public sealed partial class PhoneRemindersSetting : ObservableObject
{
    private readonly PhoneReminders _reminders;
    private readonly INotificationAccess _access;
    private readonly IErrorReporter _errors;
    private Task _saving = Task.CompletedTask;
    private ReminderSettings _settings = ReminderSettings.Default;
    private bool _showing;

    public PhoneRemindersSetting(PhoneReminders reminders, INotificationAccess access, IErrorReporter errors)
    {
        _reminders = reminders;
        _access = access;
        _errors = errors;
    }

    public bool IsSupported => _access.IsSupported;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    /// <summary>When she is reminded of a booking or a timed task.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemindDayBefore), nameof(RemindTwoHours), nameof(RemindBoth))]
    public partial ReminderPreference When { get; private set; }

    public bool RemindDayBefore => When == ReminderPreference.DayBefore;

    public bool RemindTwoHours => When == ReminderPreference.TwoHoursBefore;

    public bool RemindBoth => When == ReminderPreference.Both;

    /// <summary>"Discreet lock screen": notifications name no check or task.</summary>
    [ObservableProperty]
    public partial bool DiscreetTitles { get; set; }

    /// <summary>A calm Sand note when notifications are not allowed.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    public async Task LoadAsync()
    {
        if (!IsSupported)
        {
            return;
        }

        await _saving;
        var on = await _reminders.IsOnAsync();
        var settings = await _reminders.GetSettingsAsync();
        _settings = settings;
        Show(on);
        When = settings.When;
        _showing = true;
        DiscreetTitles = settings.DiscreetTitles;
        _showing = false;
        Notice = on && !await _access.IsGrantedAsync() ? Text.Get("Reminders_Blocked") : null;
    }

    /// <summary>Completes when the last change she made has been saved and the reminders set or cancelled.</summary>
    public Task WhenSavedAsync() => _saving;

    [RelayCommand]
    private void OpenPhoneSettings() => _access.OpenAppSettings();

    /// <summary>"Remind me": 24 hours before, 2 hours before, or both.</summary>
    [RelayCommand]
    private void SetReminders(string choice)
    {
        if (Enum.TryParse<ReminderPreference>(choice, out var when) && Enum.IsDefined(when) && when != When)
        {
            When = when;
            Change(_settings with { When = when });
        }
    }

    partial void OnDiscreetTitlesChanged(bool value)
    {
        if (!_showing)
        {
            Change(_settings with { DiscreetTitles = value });
        }
    }

    /// <summary>Saves her choice behind any save still running, and sets the reminders again with it.</summary>
    private void Change(ReminderSettings settings)
    {
        _settings = settings;
        _saving = SaveAsync(_saving, settings);
    }

    private async Task SaveAsync(Task previous, ReminderSettings settings)
    {
        await previous;
        try
        {
            await _reminders.SaveSettingsAsync(settings);
        }
#pragma warning disable CA1031 // Her choice is reported by code and she can choose again; Settings stays open.
        catch (Exception ex)
        {
            _errors.Report(ex, "settings.reminder_choices");
            Notice = Text.Get("Common_Error");
        }
#pragma warning restore CA1031
    }

    partial void OnIsOnChanged(bool value)
    {
        if (!_showing)
        {
            _saving = ChangeAsync(_saving, value);
        }
    }

    private async Task ChangeAsync(Task previous, bool on)
    {
        await previous;
        try
        {
            if (on && !await _access.RequestAsync())
            {
                Show(false);
                Notice = Text.Get("Reminders_Denied");
                return;
            }

            Notice = null;
            await _reminders.SetOnAsync(on);
        }
#pragma warning disable CA1031 // The switch goes back and the failure is reported by code; she can try again.
        catch (Exception ex)
        {
            _errors.Report(ex, "settings.reminders");
            Show(!on);
            Notice = Text.Get("Common_Error");
        }
#pragma warning restore CA1031
    }

    /// <summary>Shows a state without treating it as her change.</summary>
    private void Show(bool on)
    {
        _showing = true;
        IsOn = on;
        _showing = false;
    }
}
