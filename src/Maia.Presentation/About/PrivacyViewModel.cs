using CommunityToolkit.Mvvm.Input;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.About;

/// <summary>One part of the privacy notice or the terms of use: a short heading and a few plain sentences.</summary>
public sealed record NoticeSection(string Title, string Body);

/// <summary>
/// The privacy notice, inside Maia so she can read it offline: what Maia keeps and where (only on this phone,
/// encrypted), what it reads from her contacts, that there are no ads, trackers or network in the free app,
/// reminders on the phone and what the lock screen shows, files shared into Maia and a result opened in another app,
/// that backups are files she controls (and the two backup dates kept in the phone's settings), the app lock and the
/// recents screen, the problem log and Share diagnostics, how to export and delete, her rights under the GDPR, and how
/// the notice changes. Reached from About Maia and from setup step 1.
/// </summary>
public sealed partial class PrivacyViewModel
{
    /// <summary>The notice's parts, in order; each has <c>Privacy_{key}Title</c> and <c>Privacy_{key}Body</c>.</summary>
    internal static readonly string[] SectionKeys =
        ["Keeps", "Where", "Network", "Reminders", "Contacts", "Files", "Backup", "Lock", "Diagnostics", "Control", "Rights", "Changes", "Care"];

    private readonly INavigator _navigator;

    public PrivacyViewModel(INavigator navigator)
    {
        _navigator = navigator;
        Sections = SectionKeys
            .Select(key => new NoticeSection(Text.Get($"Privacy_{key}Title"), Text.Format($"Privacy_{key}Body", AppConfig.EmergencyNumber)))
            .ToList();
    }

    public IReadOnlyList<NoticeSection> Sections { get; }

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();
}
