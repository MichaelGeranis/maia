using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Domain.Pregnancy;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Setup;

/// <summary>
/// Setup step 1: the due date, entered directly or worked out from the last period. Each picker offers only the dates
/// Maia accepts (a pregnancy between 0 and 44 weeks today), and switching between them carries the date across. At the
/// top she chooses Maia's language (English or Ελληνικά, the phone's when it is one of them): the app is redrawn in it
/// and comes back here with her date kept.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject
{
    /// <summary>
    /// How far off the due date starts, in weeks from today, before she picks hers: a full 40-week pregnancy from today,
    /// so a new setup never opens on a date that looks like an earlier plan's.
    /// </summary>
    public const int WeeksToGoAtFirst = 40;

    private readonly SetupSession _session;
    private readonly INavigator _navigator;
    private readonly ILanguageSwitcher _languages;

    public SetupViewModel(IClock clock, SetupSession session, INavigator navigator, ILanguageSwitcher languages)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _navigator = navigator;
        _languages = languages;

        Entry = new DueDateEntry(clock, session.DueDate ?? clock.Today.AddDays(WeeksToGoAtFirst * GestationalAge.DaysPerWeek));
        Entry.PropertyChanged += OnEntryChanged;
    }

    /// <summary>The date she enters, and what it means today.</summary>
    public DueDateEntry Entry { get; }

    public bool IsEnglish { get; } = Text.Language == Text.English;

    public bool IsGreek { get; } = Text.Language == Text.Greek;

    /// <summary>"Moving from another phone? Restore a backup".</summary>
    [RelayCommand]
    private Task RestoreAsync() => _navigator.GoToRestoreAsync();

    /// <summary>The short line under the button: her plan stays on this phone, with the privacy notice one tap away.</summary>
    [RelayCommand]
    private Task OpenPrivacyAsync() => _navigator.GoToPrivacyAsync();

    /// <summary>The line under it: using Maia means she accepts the terms of use, one tap away.</summary>
    [RelayCommand]
    private Task OpenTermsAsync() => _navigator.GoToTermsAsync();

    /// <summary>
    /// Shows Maia in <paramref name="language"/>, before her plan is made in it. The date she entered is kept, so the
    /// redrawn setup opens on it.
    /// </summary>
    [RelayCommand]
    private async Task ChooseLanguageAsync(string language)
    {
        if (language == Text.Language || language is not (Text.English or Text.Greek))
        {
            return;
        }

        if (Entry.IsValid)
        {
            _session.DueDate = Entry.EffectiveDueDate;
        }

        await _session.SaveAsync(SetupStep.DueDate);
        await _languages.SwitchAsync(language, LanguageReturn.Setup);
    }

    private bool CanContinue() => Entry.IsValid;

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        _session.DueDate = Entry.EffectiveDueDate;
        await _session.SaveAsync(SetupStep.Plan);
        await _navigator.GoToSetupPlanAsync();
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DueDateEntry.IsValid))
        {
            ContinueCommand.NotifyCanExecuteChanged();
        }
    }
}
