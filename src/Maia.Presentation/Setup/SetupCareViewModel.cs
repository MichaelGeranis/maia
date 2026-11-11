using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Domain.Care;
using Maia.Presentation.Care;
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Settings;

namespace Maia.Presentation.Setup;

/// <summary>
/// Setup step 3 (design screen 03): her OB-GYN from the phone's contacts, and "Remind me", which turns on Maia's own
/// reminders on this phone. Both are optional. "Start my plan" creates the plan with them; "Skip for now" creates it
/// without.
/// </summary>
public sealed partial class SetupCareViewModel : ObservableObject
{
    private readonly SetupSession _session;
    private readonly PlanService _plans;
    private readonly IContactPicker _picker;
    private readonly PhoneReminders _reminders;
    private readonly INotificationAccess _access;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;

    public SetupCareViewModel(
        SetupSession session,
        PlanService plans,
        IContactPicker picker,
        PhoneReminders reminders,
        INotificationAccess access,
        INavigator navigator,
        INotifier notifier,
        IErrorReporter errors)
    {
        _session = session;
        _plans = plans;
        _picker = picker;
        _reminders = reminders;
        _access = access;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
    }

    public ObservableCollection<ContactItem> ObGyns { get; } = [];

    /// <summary>Whether this phone can show Maia's reminders (Android for now); elsewhere the card is not offered.</summary>
    public bool CanRemind => _access.IsSupported;

    /// <summary>"Choose from contacts", or "Link another contact" once one is linked.</summary>
    [ObservableProperty]
    public partial string PickText { get; private set; } = string.Empty;

    /// <summary>"Remind me" is on: her reminders start with her plan.</summary>
    [ObservableProperty]
    public partial bool RemindMe { get; private set; }

    [ObservableProperty]
    public partial string? Notice { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(SkipCommand))]
    public partial bool IsBusy { get; private set; }

    public async Task LoadAsync()
    {
        if (_session.DueDate is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        // A contact she picked just as Android closed Maia in the background comes back with the rest of her choices.
        if (await _picker.TakeInterruptedPickAsync() is { } picked)
        {
            await AddAsync(picked);
        }

        Show();
    }

    [RelayCommand]
    private async Task PickAsync()
    {
        Notice = null;
        var picked = await _picker.PickAsync();
        if (picked is not null)
        {
            await AddAsync(picked);
        }

        Show();
    }

    /// <summary>
    /// "Remind me": turning it on asks for notifications right away, so the answer is known before she starts. If she
    /// says no, it stays off and a calm note says where to allow them.
    /// </summary>
    [RelayCommand]
    private async Task ToggleRemindersAsync()
    {
        Notice = null;
        if (_session.RemindMe)
        {
            _session.RemindMe = false;
        }
        else if (await _access.RequestAsync())
        {
            _session.RemindMe = true;
        }
        else
        {
            Notice = Text.Get("Reminders_Denied");
        }

        await _session.SaveAsync(SetupStep.Care);
        Show();
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private Task StartAsync() => FinishAsync(withChoices: true);

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private Task SkipAsync() => FinishAsync(withChoices: false);

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();

    private bool CanFinish() => !IsBusy;

    private async Task AddAsync(PickedContact picked)
    {
        if (string.IsNullOrWhiteSpace(picked.PhoneNumber))
        {
            Notice = Text.Get("Team_NoNumber");
            return;
        }

        if (!_session.ObGyns.Any(c => c.PhoneNumber == picked.PhoneNumber))
        {
            _session.ObGyns.Add(picked);
            await _session.SaveAsync(SetupStep.Care);
        }
    }

    /// <summary>
    /// Makes the plan, then adds her doctor and turns her reminders on. Only the plan decides whether setup is done: if
    /// it can't be made she stays here and can try again. When a try made it but something after it failed, the next
    /// try brings that plan in line with what she chose since, so no plan from an earlier choice is ever kept. Her
    /// doctor comes after: if that fails, her plan still opens and a toast says where to add her doctor.
    /// </summary>
    private async Task FinishAsync(bool withChoices)
    {
        if (_session.DueDate is not { } due)
        {
            return;
        }

        IsBusy = true;
        Notice = null;
        var request = new CreatePlanRequest(due, AppConfig.DefaultCountry, Text.Language, _session.IncludeOverrides);
        Notice = await PlanChange.RunAsync(
            async () =>
            {
                try
                {
                    await _plans.CreatePlanAsync(request);
                }
                catch (PlanAlreadyExistsException)
                {
                    // Made by an earlier try (or a double tap): it takes the choices she has now.
                    await _plans.ApplySetupAsync(request);
                }
            },
            "setup.finish",
            _errors);
        if (Notice is not null)
        {
            IsBusy = false;
            return;
        }

        var complete = !withChoices || await AddChoicesAsync();

        await _session.ClearAsync();
        await _navigator.GoToMainAsync();

        // After navigating, so the toast shows on Your week rather than on the page that is leaving.
        _notifier.Show(Text.Get(complete ? "Toast_PlanReady" : "Toast_PlanReadyWithGaps"));
    }

    /// <summary>Links her doctor and turns her reminders on. Each is tried even when the other fails.</summary>
    /// <returns>Whether her doctor went in.</returns>
    private async Task<bool> AddChoicesAsync()
    {
        var contacts = await PlanChange.RunAsync(
            () => _plans.LinkContactsAsync(_session.ObGyns.Select(c => new ContactToLink(CareRole.ObGyn, c.DisplayName, c.PhoneNumber, c.Label, c.DeviceContactId))),
            "setup.contacts",
            _errors);
        if (_session.RemindMe)
        {
            // Reported by code if it fails; she can still turn them on in Settings, where the switch shows they are off.
            await PlanChange.RunAsync(() => _reminders.SetOnAsync(true), "setup.reminders", _errors);
        }

        return contacts is null;
    }

    private void Show()
    {
        var items = _session.ObGyns.Select(ContactItem.From).ToList();
        CollectionSync.Apply(ObGyns, items, (a, b) => a.Number == b.Number);
        PickText = Text.Get(ObGyns.Count == 0 ? "SetupCare_Choose" : "SetupCare_LinkAnother");
        RemindMe = _session.RemindMe;
    }
}
