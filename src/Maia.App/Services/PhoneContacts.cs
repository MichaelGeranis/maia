using Maia.Application.Abstractions;
using Maia.Presentation.Care;

namespace Maia.App.Services;

/// <summary>
/// The phone's own contact picker. Maia sees only the contact she picks, and keeps name, number, label and id.
/// On Android she picks a phone number directly: Android then grants read access to that one entry, so Maia needs
/// no contacts permission at all (MAUI's picker reads the numbers table afterwards, which would need one).
/// </summary>
public sealed class MauiContactPicker : IContactPicker
{
    private readonly IErrorReporter _errors;

    public MauiContactPicker(IErrorReporter errors)
    {
        _errors = errors;
    }

    public async Task<PickedContact?> PickAsync()
    {
        try
        {
#if ANDROID
            return await Platforms.Android.PhonePicker.PickAsync();
#else
            var contact = await MainThread.InvokeOnMainThreadAsync(() => Contacts.Default.PickContactAsync());
            if (contact is null)
            {
                return null;
            }

            var number = contact.Phones.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.PhoneNumber))?.PhoneNumber ?? string.Empty;
            return new PickedContact(contact.DisplayName, number, Label: null, contact.Id);
#endif
        }
#pragma warning disable CA1031 // A picker failure means "nothing picked"; details go to the error reporter.
        catch (Exception ex)
        {
            _errors.Report(ex, "contacts.pick");
            return null;
        }
#pragma warning restore CA1031
    }

    public Task<PickedContact?> TakeInterruptedPickAsync()
    {
#if ANDROID
        try
        {
            return Task.FromResult(Platforms.Android.PhonePicker.TakeInterrupted());
        }
#pragma warning disable CA1031 // A pick that can't be read back is simply not there; details go to the error reporter.
        catch (Exception ex)
        {
            _errors.Report(ex, "contacts.interrupted_pick");
            return Task.FromResult<PickedContact?>(null);
        }
#pragma warning restore CA1031
#else
        return Task.FromResult<PickedContact?>(null);
#endif
    }
}

/// <summary>Opens the phone's dialer with the number filled in; she presses call.</summary>
public sealed class MauiPhoneDialer : IDialer
{
    public void Dial(string number)
    {
        if (PhoneDialer.Default.IsSupported)
        {
            PhoneDialer.Default.Open(number);
        }
    }
}
