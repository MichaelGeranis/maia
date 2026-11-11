using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Care;

/// <summary>
/// A linked contact as a row: initials avatar, name, and "Mobile · ••• 4567". The full number is only used to call.
/// </summary>
public sealed record ContactItem(CareContactId? Id, string Name, string Initials, string Meta, string Number, bool AltAvatar)
{
    /// <summary>What a screen reader says for the call button: "Call Dr. Eleni Papadaki".</summary>
    public string CallLabel => Text.Format("Team_Call", Name);

    /// <summary>What a screen reader says for the × button: "Unlink Dr. Eleni Papadaki".</summary>
    public string UnlinkLabel => Text.Format("Team_Unlink", Name);

    public static ContactItem From(CareContact contact, int index)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return new ContactItem(contact.Id, contact.DisplayName, InitialsOf(contact.DisplayName), MetaOf(contact.Label, contact.PhoneNumber), contact.PhoneNumber, index % 2 == 1);
    }

    public static ContactItem From(PickedContact contact, int index)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return new ContactItem(null, contact.DisplayName, InitialsOf(contact.DisplayName), MetaOf(contact.Label, contact.PhoneNumber), contact.PhoneNumber, index % 2 == 1);
    }

    /// <summary>"Dr. Eleni Papadaki" → "EP": the first letters of the first two words, skipping titles such as "Dr.".</summary>
    internal static string InitialsOf(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !w.EndsWith('.') && char.IsLetter(w[0]))
            .Take(2)
            .Select(w => char.ToUpper(w[0], Text.Culture));
        var initials = string.Concat(words);
        return initials.Length > 0 ? initials : "·";
    }

    /// <summary>"Mobile · ••• 4567", or just the masked number when the phone gave no label.</summary>
    internal static string MetaOf(string? label, string number)
    {
        var digits = new string(number.Where(char.IsAsciiDigit).ToArray());
        var masked = "••• " + (digits.Length > 4 ? digits[^4..] : digits);
        return string.IsNullOrWhiteSpace(label) ? masked : $"{label} · {masked}";
    }
}
