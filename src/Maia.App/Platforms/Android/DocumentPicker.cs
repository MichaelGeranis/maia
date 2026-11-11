using Android.App;
using Android.Content;
using Maia.Presentation.Results;
using AndroidUri = Android.Net.Uri;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Picks PDFs and photos with the phone's document picker. The files are read straight from their provider, one at a
/// time and only once their size is known; MAUI's picker would first copy each one, in full and in plaintext, into
/// Maia's cache.
/// </summary>
internal static class DocumentPicker
{
    private const int RequestCode = 4472;
    private static readonly string[] MediaTypes = ["application/pdf", "image/*"];
    private static TaskCompletionSource<IReadOnlyList<AndroidUri>>? _pending;

    public static async Task<IReadOnlyList<PickedFile>> PickAsync()
    {
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to show the document picker.");
        _pending?.TrySetResult([]);
        var pending = new TaskCompletionSource<IReadOnlyList<AndroidUri>>();
        _pending = pending;

        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, MediaTypes);
        intent.PutExtra(Intent.ExtraAllowMultiple, true);
        await MainThread.InvokeOnMainThreadAsync(() => activity.StartActivityForResult(intent, RequestCode));

        var uris = await pending.Task;
        return uris.Select(uri => ContentFiles.From(activity, uri)).ToList();
    }

    /// <summary>Called from <see cref="MainActivity.OnActivityResult"/>.</summary>
    public static bool OnResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode)
        {
            return false;
        }

        _pending?.TrySetResult(resultCode == Result.Ok ? UrisOf(data) : []);
        _pending = null;
        return true;
    }

    /// <summary>Several files come back as clip data, one as the intent's data.</summary>
    internal static List<AndroidUri> UrisOf(Intent? data)
    {
        var uris = new List<AndroidUri>();
        if (data?.ClipData is { } clip)
        {
            for (var i = 0; i < clip.ItemCount; i++)
            {
                if (clip.GetItemAt(i)?.Uri is { } uri)
                {
                    uris.Add(uri);
                }
            }
        }

        if (uris.Count == 0 && data?.Data is { } single)
        {
            uris.Add(single);
        }

        return uris;
    }
}
