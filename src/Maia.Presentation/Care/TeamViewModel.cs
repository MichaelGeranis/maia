using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Care;

/// <summary>
/// "Care team" (design screen 09), a card in Settings that folds: her OB-GYN contacts, linked from the phone, to call and
/// book; and under them a smaller card for each of her other doctors (<see cref="Doctors"/>) she has linked someone to,
/// to call. "Link from contacts" shows only until she has linked an OB-GYN; one "Link another doctor" asks which doctor
/// (her OB-GYN first, for a second contact), then for the contact, so the screen never shows a wall of empty roles.
/// Unlinking asks once more, in place, next to the contact.
/// </summary>
public sealed partial class TeamViewModel : ObservableObject
{
    private readonly PlanQueries _queries;
    private readonly PlanService _plans;
    private readonly IContactPicker _picker;
    private readonly IDialer _dialer;
    private readonly IChooser _chooser;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;

    /// <summary>The contact she tapped × on, whichever role it is linked to, until she says "Unlink" or "Keep".</summary>
    private ContactItem? _pending;

    public TeamViewModel(
        PlanQueries queries,
        PlanService plans,
        IContactPicker picker,
        IDialer dialer,
        IChooser chooser,
        INotifier notifier,
        IErrorReporter errors)
    {
        _queries = queries;
        _plans = plans;
        _picker = picker;
        _dialer = dialer;
        _chooser = chooser;
        _notifier = notifier;
        _errors = errors;
        DoctorTiles = [.. Doctors.Select(role => new DoctorTile(role))];
        DoctorRows = [.. DoctorTiles.Select(tile => new DoctorRow([tile]))];
    }

    /// <summary>Her other doctors, in the order their tiles show.</summary>
    public static IReadOnlyList<CareRole> Doctors { get; } =
    [
        CareRole.Dentist,
        CareRole.Internist,
        CareRole.Endocrinologist,
        CareRole.Haematologist,
        CareRole.Cardiologist,
        CareRole.Dermatologist,
        CareRole.Gastroenterologist,
    ];

    public ObservableCollection<ContactItem> ObGyns { get; } = [];

    /// <summary>A tile for each of <see cref="Doctors"/>, in their order.</summary>
    public IReadOnlyList<DoctorTile> DoctorTiles { get; }

    /// <summary>
    /// One row per doctor, at full width, shown only once she has linked someone to it (<see cref="DoctorRow.IsShown"/>).
    /// </summary>
    public IReadOnlyList<DoctorRow> DoctorRows { get; }

    /// <summary>
    /// Whether the card is open, showing her doctors; folded, it is just its heading. It folds again each time she leaves
    /// Settings (<see cref="Fold"/>).
    /// </summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    public partial bool HasObGyns { get; private set; }

    /// <summary>A calm line under the OB-GYN card: about her OB-GYN, or that the screen could not load.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>A calm line under "Link another doctor", about a doctor she has not linked anyone to yet.</summary>
    [ObservableProperty]
    public partial string? DoctorsNotice { get; private set; }

    /// <summary>The OB-GYN contact she tapped × on, until she says "Unlink" or "Keep". A doctor's is on its row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmingUnlink), nameof(UnlinkQuestion))]
    public partial ContactItem? PendingUnlink { get; private set; }

    /// <summary>Whether the OB-GYN card asks before unlinking one of its contacts.</summary>
    public bool IsConfirmingUnlink => PendingUnlink is not null;

    /// <summary>"Unlink Dr. Eleni Papadaki? Your bookings stay in your plan."</summary>
    public string UnlinkQuestion => PendingUnlink is { } contact ? Text.Format("Team_UnlinkConfirm", contact.Name) : string.Empty;

    public async Task LoadAsync()
    {
        // In place, so a row that stays is not drawn again and only a newly linked contact pops in. A row keeps its
        // avatar colour when one above it is unlinked.
        var team = await _queries.GetCareContactsAsync();
        Sync(ObGyns, team, CareRole.ObGyn);
        HasObGyns = ObGyns.Count > 0;
        foreach (var tile in DoctorTiles)
        {
            Sync(tile.Contacts, team, tile.Role);
            tile.HasContacts = tile.Contacts.Count > 0;
        }

        foreach (var row in DoctorRows)
        {
            row.Refresh();
        }

        if (_pending is { } pending && RoleOf(pending) is null)
        {
            Ask(null);
        }
    }

    /// <summary>Folds the card, closing any question or note it was showing: a tap on its heading, or she left Settings.</summary>
    public void Fold()
    {
        IsOpen = false;
        Ask(null);
        ClearNotices();
    }

    /// <summary>A tap on the card's heading: opens it, or folds it again.</summary>
    [RelayCommand]
    private void Toggle()
    {
        if (IsOpen)
        {
            Fold();
        }
        else
        {
            IsOpen = true;
        }
    }

    /// <summary>Opens the phone's contact picker and links the contact she picks to her OB-GYN.</summary>
    [RelayCommand]
    private Task LinkAsync() => LinkToAsync(CareRole.ObGyn);

    /// <summary>
    /// "Link another doctor": she chooses which doctor in the phone's list (her OB-GYN first, for a second contact, then
    /// her other doctors), then the contact.
    /// </summary>
    [RelayCommand]
    private async Task LinkDoctorAsync()
    {
        ClearNotices();
        CareRole[] roles = [CareRole.ObGyn, .. Doctors];
        var choice = await _chooser.ChooseAsync(Text.Get("Team_WhichDoctor"), [Text.Get("Team_ObGyn"), .. DoctorTiles.Select(t => t.Name)]);
        if (choice is { } index && index >= 0 && index < roles.Length)
        {
            await LinkToAsync(roles[index]);
        }
    }

    [RelayCommand]
    private void Call(ContactItem contact)
    {
        _notifier.Show(Text.Format("Toast_Calling", contact.Name));
        _dialer.Dial(contact.Number);
    }

    /// <summary>The × on a contact: asks once more, in place, before unlinking.</summary>
    [RelayCommand]
    private void AskUnlink(ContactItem contact)
    {
        ClearNotices();
        Ask(contact);
    }

    [RelayCommand]
    private void KeepContact() => Ask(null);

    /// <summary>"Unlink": takes the contact off its role and out of any appointment that named it.</summary>
    [RelayCommand]
    private async Task UnlinkAsync()
    {
        var contact = _pending;
        Ask(null);
        if (contact is not { Id: { } id } || RoleOf(contact) is not { } role)
        {
            return;
        }

        var notice = await PlanChange.RunAsync(() => _plans.UnlinkContactAsync(id), "team.unlink", _errors);
        if (notice is null)
        {
            _notifier.Show(Text.Format("Toast_Unlinked", contact.Name));
            notice = await ScreenLoad.ReloadAsync(LoadAsync, "team.load", _errors);
        }

        ShowNotice(role, notice);
    }

    /// <summary>Opens the phone's contact picker and links the contact she picks to the role: her OB-GYN or a doctor.</summary>
    private async Task LinkToAsync(CareRole role)
    {
        ClearNotices();
        var picked = await _picker.PickAsync();
        if (picked is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(picked.PhoneNumber))
        {
            ShowNotice(role, Text.Get("Team_NoNumber"));
            return;
        }

        var added = 0;
        var notice = await PlanChange.RunAsync(
            async () => added = await _plans.LinkContactsAsync([new ContactToLink(role, picked.DisplayName, picked.PhoneNumber, picked.Label, picked.DeviceContactId)]),
            "team.link",
            _errors);
        if (notice is null && added > 0)
        {
            _notifier.Show(role == CareRole.ObGyn
                ? DisplayFormat.Count("Toast_LinkedOne", "Toast_Linked", added)
                : Text.Format("Toast_LinkedContact", picked.DisplayName));
            notice = await ScreenLoad.ReloadAsync(LoadAsync, "team.load", _errors);
        }

        ShowNotice(role, notice);
    }

    /// <summary>Opens the question before unlinking where the contact is (the OB-GYN card or its doctor's row), and closes any other.</summary>
    private void Ask(ContactItem? contact)
    {
        _pending = contact;
        var role = contact is null ? null : RoleOf(contact);
        PendingUnlink = role == CareRole.ObGyn ? contact : null;
        foreach (var row in DoctorRows)
        {
            row.PendingUnlink = role is { } r && row.Holds(r) ? contact : null;
        }
    }

    /// <summary>
    /// Shows a note where she acted (under the OB-GYN card, under the doctor's row, or under "Link another doctor" for a
    /// doctor not shown yet), and no other.
    /// </summary>
    private void ShowNotice(CareRole role, string? notice)
    {
        Notice = role == CareRole.ObGyn ? notice : null;
        var shown = false;
        foreach (var row in DoctorRows)
        {
            var here = row.Holds(role) && row.IsShown;
            row.Notice = here ? notice : null;
            shown |= here;
        }

        DoctorsNotice = role != CareRole.ObGyn && !shown ? notice : null;
    }

    private void ClearNotices() => ShowNotice(CareRole.ObGyn, null);

    /// <summary>The role a contact on screen is linked to, or null once it is no longer shown.</summary>
    private CareRole? RoleOf(ContactItem contact)
    {
        if (ObGyns.Any(c => c.Id == contact.Id))
        {
            return CareRole.ObGyn;
        }

        return DoctorTiles.FirstOrDefault(t => t.Contacts.Any(c => c.Id == contact.Id))?.Role;
    }

    private static void Sync(ObservableCollection<ContactItem> rows, IReadOnlyList<CareContact> team, CareRole role) =>
        CollectionSync.Apply(
            rows,
            team.Where(c => c.Role == role).Select(ContactItem.From).ToList(),
            (a, b) => a.Id == b.Id,
            (list, i, fresh) =>
            {
                if (list[i] != fresh with { AltAvatar = list[i].AltAvatar })
                {
                    list[i] = fresh;
                }
            });
}
