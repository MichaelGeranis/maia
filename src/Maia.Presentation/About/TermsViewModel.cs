using CommunityToolkit.Mvvm.Input;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.About;

/// <summary>
/// The terms of use, inside Maia so she can read them offline, in plain words: the agreement and who Maia is for, that
/// Maia organises and her doctor decides (not a medical device, no advice, no interpretation), that a typical journey is
/// a common starting point, not for emergencies, what reminders and the timer can't promise, that she checks what she
/// enters, that her data is hers to keep and back up, Maia as it is, the limits of liability as far as the law allows,
/// her licence to use it, how the terms change, and Greek law. Reached from About Maia and from setup step 1.
/// </summary>
public sealed partial class TermsViewModel
{
    /// <summary>The terms' parts, in order; each has <c>Terms_{key}Title</c> and <c>Terms_{key}Body</c>.</summary>
    internal static readonly string[] SectionKeys =
        ["Agreement", "Organiser", "Journey", "Emergencies", "Reminders", "Entries", "Data", "AsIs", "Liability", "Use", "Changes", "Law"];

    private readonly INavigator _navigator;

    public TermsViewModel(INavigator navigator)
    {
        _navigator = navigator;
        Sections = SectionKeys
            .Select(key => new NoticeSection(Text.Get($"Terms_{key}Title"), Text.Format($"Terms_{key}Body", AppConfig.EmergencyNumber)))
            .ToList();
    }

    public IReadOnlyList<NoticeSection> Sections { get; }

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();
}
