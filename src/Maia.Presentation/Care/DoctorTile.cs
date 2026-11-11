using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Maia.Domain.Care;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Care;

/// <summary>
/// One of her other doctors on Care team, as a small card under the OB-GYN card: the role ("Dentist") and the contacts
/// she linked to it, each to call or unlink. Another contact for it is linked through "Link another doctor".
/// </summary>
public sealed partial class DoctorTile : ObservableObject
{
    internal DoctorTile(CareRole role)
    {
        Role = role;
        Name = Text.Get($"Role_{role}");
    }

    public CareRole Role { get; }

    /// <summary>"Dentist", "Οδοντίατρος".</summary>
    public string Name { get; }

    /// <summary>The contacts linked to this doctor, in the order she linked them.</summary>
    public ObservableCollection<ContactItem> Contacts { get; } = [];

    [ObservableProperty]
    public partial bool HasContacts { get; internal set; }
}

/// <summary>
/// A doctor's row on Care team: its card, and under it the question before unlinking one of its contacts or a note
/// about it. Shown only once she has linked someone to that doctor.
/// </summary>
public sealed partial class DoctorRow : ObservableObject
{
    internal DoctorRow(IReadOnlyList<DoctorTile> tiles) => Tiles = tiles;

    public IReadOnlyList<DoctorTile> Tiles { get; }

    /// <summary>The contact of this row she tapped × on, until she says "Unlink" or "Keep".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmingUnlink), nameof(UnlinkQuestion))]
    public partial ContactItem? PendingUnlink { get; internal set; }

    public bool IsConfirmingUnlink => PendingUnlink is not null;

    /// <summary>"Unlink Dr. Maria Kosta? Your bookings stay in your plan."</summary>
    public string UnlinkQuestion => PendingUnlink is { } contact ? Text.Format("Team_UnlinkConfirm", contact.Name) : string.Empty;

    /// <summary>A calm line about what she just did on one of this row's tiles, such as a contact with no number.</summary>
    [ObservableProperty]
    public partial string? Notice { get; internal set; }

    /// <summary>Whether the row shows: she has linked someone to its doctor.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; private set; }

    internal void Refresh() => IsShown = Tiles.Any(t => t.HasContacts);

    internal bool Holds(CareRole role) => Tiles.Any(t => t.Role == role);
}
