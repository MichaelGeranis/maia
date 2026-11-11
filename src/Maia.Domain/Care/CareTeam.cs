using System.Collections.ObjectModel;
using Maia.Domain.Common;

namespace Maia.Domain.Care;

/// <summary>
/// People and places in the pregnancy. Care team links contacts to <see cref="ObGyn"/> (her doctor, used for bookings)
/// and to her other doctors, <see cref="Dentist"/> to <see cref="Gastroenterologist"/>; Midwife to Family are not in
/// use. A role is stored by its name, so a new one goes at the end and none is renamed.
/// </summary>
public enum CareRole
{
    ObGyn = 0,
    Midwife = 1,
    Partner = 2,
    Hospital = 3,
    Lab = 4,
    Pediatrician = 5,
    Family = 6,
    Dentist = 7,

    /// <summary>The internal-medicine doctor: "παθολόγος" in Greek.</summary>
    Internist = 8,
    Endocrinologist = 9,
    Haematologist = 10,
    Cardiologist = 11,
    Dermatologist = 12,
    Gastroenterologist = 13,
}

/// <summary>
/// A phone contact linked to a role. Only what the app needs is copied from the address book;
/// <see cref="DeviceContactId"/> lets the app refresh details if the contact changes.
/// </summary>
public sealed record CareContact
{
    public const int MaxFieldLength = 120;

    public CareContact(
        CareContactId id,
        CareRole role,
        string displayName,
        string phoneNumber,
        string? label = null,
        string? deviceContactId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        if (displayName.Length > MaxFieldLength || phoneNumber.Length > MaxFieldLength || label?.Length > MaxFieldLength)
        {
            throw new DomainException("contact.field.too_long", "Contact field is too long.");
        }

        Id = id;
        Role = role;
        DisplayName = displayName.Trim();
        PhoneNumber = phoneNumber.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        DeviceContactId = deviceContactId;
    }

    public CareContactId Id { get; }

    public CareRole Role { get; }

    public string DisplayName { get; }

    public string PhoneNumber { get; }

    /// <summary>The phone's label for the number, such as "Mobile" or "Reception".</summary>
    public string? Label { get; }

    public string? DeviceContactId { get; }

    /// <summary>Contacts are the same person/number when role and normalized number match.</summary>
    public bool IsSameLinkAs(CareContact other) =>
        other is not null && Role == other.Role && Normalize(PhoneNumber) == Normalize(other.PhoneNumber);

    private static string Normalize(string phone) => new(phone.Where(c => char.IsAsciiDigit(c) || c == '+').ToArray());
}

/// <summary>The care team: one or many contacts per role, ordered as the parent linked them.</summary>
public sealed class CareTeam
{
    public const int MaxContactsPerRole = 10;

    private readonly List<CareContact> _contacts;
    private readonly ReadOnlyCollection<CareContact> _contactsView;

    public CareTeam(IEnumerable<CareContact>? contacts = null)
    {
        _contacts = contacts?.ToList() ?? [];
        _contactsView = _contacts.AsReadOnly();
    }

    public IReadOnlyList<CareContact> Contacts => _contactsView;

    public IReadOnlyList<CareContact> For(CareRole role) => _contacts.Where(c => c.Role == role).ToList();

    /// <summary>
    /// The first contact linked to a role: for the OB-GYN, the one named as her doctor (on her next visit, say). "Call
    /// to book" lets her choose among all of them.
    /// </summary>
    public CareContact? PrimaryFor(CareRole role) => _contacts.FirstOrDefault(c => c.Role == role);

    public CareContact? Find(CareContactId id) => _contacts.FirstOrDefault(c => c.Id == id);

    internal bool Link(CareContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        if (_contacts.Any(c => c.IsSameLinkAs(contact)))
        {
            return false;
        }

        if (_contacts.Count(c => c.Role == contact.Role) >= MaxContactsPerRole)
        {
            throw new DomainException("care_team.role_full", $"A role can have at most {MaxContactsPerRole} contacts.");
        }

        _contacts.Add(contact);
        return true;
    }

    internal CareContact Unlink(CareContactId id)
    {
        var contact = Find(id) ?? throw new DomainException("care_team.contact_not_found", $"Contact '{id}' is not linked.");
        _contacts.Remove(contact);
        return contact;
    }
}
