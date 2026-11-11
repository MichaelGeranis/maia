namespace Maia.Presentation.Care;

/// <summary>A contact she picked in the phone's contact picker: only what Maia keeps.</summary>
public sealed record PickedContact(string DisplayName, string PhoneNumber, string? Label, string? DeviceContactId);

/// <summary>The phone's own contact picker, one contact per pick. Maia never reads the address book itself.</summary>
public interface IContactPicker
{
    /// <returns>The picked contact, or null if she cancelled.</returns>
    Task<PickedContact?> PickAsync();

    /// <summary>
    /// A contact she picked just as Android closed Maia in the background (the pick reaches the Maia that starts
    /// again), handed over once. Null when there is none.
    /// </summary>
    Task<PickedContact?> TakeInterruptedPickAsync();
}

/// <summary>Starts a phone call with the phone's dialer. Implemented by the app.</summary>
public interface IDialer
{
    void Dial(string number);
}
