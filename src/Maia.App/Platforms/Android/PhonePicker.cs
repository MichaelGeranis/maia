using Android.App;
using Android.Content;
using Android.Provider;
using Maia.Presentation.Care;
using Phone = Android.Provider.ContactsContract.CommonDataKinds.Phone;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Picks one phone number with the system contacts app. The result URI carries a one-off read grant for that entry
/// only, so Maia never holds the contacts permission and never sees the rest of the address book.
/// </summary>
internal static class PhonePicker
{
    private const int RequestCode = 4471;
    private static TaskCompletionSource<global::Android.Net.Uri?>? _pending;
    private static Task<PickedContact?>? _inFlight;

    // A pick that came back after Android closed Maia in the background: nobody was waiting for it any more.
    private static global::Android.Net.Uri? _interrupted;

    /// <summary>
    /// Opens the contacts app. While it is open, another call waits for the same pick instead of stacking a second
    /// contacts app over the first (whose result would then be lost).
    /// </summary>
    public static Task<PickedContact?> PickAsync()
    {
        if (_inFlight is { IsCompleted: false } running)
        {
            return running;
        }

        return _inFlight = PickOnceAsync();
    }

    private static async Task<PickedContact?> PickOnceAsync()
    {
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to show the contact picker.");
        _pending?.TrySetResult(null);
        var pending = new TaskCompletionSource<global::Android.Net.Uri?>();
        _pending = pending;
        await MainThread.InvokeOnMainThreadAsync(() => activity.StartActivityForResult(new Intent(Intent.ActionPick, Phone.ContentUri), RequestCode));

        var uri = await pending.Task;
        return uri is null ? null : Read(activity, uri);
    }

    /// <summary>The contact picked while Maia was closed in the background, once; null when there is none.</summary>
    public static PickedContact? TakeInterrupted()
    {
        var uri = Interlocked.Exchange(ref _interrupted, null);
        return uri is not null && Platform.CurrentActivity is { } activity ? Read(activity, uri) : null;
    }

    /// <summary>Called from <see cref="MainActivity.OnActivityResult"/>.</summary>
    public static bool OnResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode)
        {
            return false;
        }

        var uri = resultCode == Result.Ok ? data?.Data : null;
        if (_pending is { } pending)
        {
            pending.TrySetResult(uri);
        }
        else
        {
            // Android closed Maia while the contacts app was open, and started it again for this result.
            _interrupted = uri;
        }

        _pending = null;
        return true;
    }

    private static PickedContact? Read(Activity activity, global::Android.Net.Uri uri)
    {
        string[] columns = [ContactsContract.Contacts.InterfaceConsts.DisplayName, Phone.Number, Phone.InterfaceConsts.Type, Phone.InterfaceConsts.Label, Phone.InterfaceConsts.ContactId];
        using var cursor = activity.ContentResolver?.Query(uri, columns, null, null, null);
        if (cursor is null || !cursor.MoveToFirst())
        {
            return null;
        }

        var name = cursor.GetString(0) ?? string.Empty;
        var number = cursor.GetString(1) ?? string.Empty;
        var type = (PhoneDataKind)cursor.GetInt(2);
        var custom = cursor.GetString(3);
        var label = Phone.GetTypeLabel(activity.Resources, type, custom);
        return new PickedContact(string.IsNullOrWhiteSpace(name) ? number : name, number, label, cursor.GetString(4));
    }
}
