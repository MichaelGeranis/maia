using Android.App;
using Android.Content;
using Android.OS;
using AndroidUri = Android.Net.Uri;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Where "Share" and "Open with" land: PDFs and photos from email, a lab portal's download or the gallery. It shows
/// nothing; it passes the files on to Maia's own window (a share would otherwise start a second Maia inside the other
/// app) together with the right to read them, and a token that tells Maia's window the files came through here
/// (see <see cref="SharedIntents"/>), and closes. Only other apps' content links are passed on, never a file:// path
/// or a link into Maia's own provider. Its own empty task affinity keeps it out of Maia's task, so a
/// share started as a new task still reaches it rather than just bringing Maia to the front.
/// </summary>
[Activity(Exported = true, NoHistory = true, ExcludeFromRecents = true, TaskAffinity = "", Theme = "@android:style/Theme.NoDisplay")]
[IntentFilter([Intent.ActionSend, Intent.ActionSendMultiple], Categories = [Intent.CategoryDefault], DataMimeType = "application/pdf")]
[IntentFilter([Intent.ActionSend, Intent.ActionSendMultiple], Categories = [Intent.CategoryDefault], DataMimeType = "image/*")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault], DataMimeType = "application/pdf", DataScheme = "content")]
public sealed class ShareActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Only other apps' files that the other app let Maia read can be passed on (a share without that right would
        // fail anyway).
        var uris = SharedUris(Intent).Where(uri => SharedIntents.IsFromAnotherApp(this, uri) && CanRead(uri)).ToList();
        if (uris.Count > 0)
        {
            var forward = new Intent(this, typeof(MainActivity));
            forward.SetAction(SharedIntents.FileResultsAction);
            SharedIntents.Stamp(forward);
            var clip = ClipData.NewRawUri(string.Empty, uris[0])!;
            foreach (var uri in uris.Skip(1))
            {
                clip.AddItem(new ClipData.Item(uri));
            }

            forward.ClipData = clip;
            forward.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.GrantReadUriPermission);
            try
            {
                StartActivity(forward);
            }
            catch (Java.Lang.SecurityException)
            {
                // The other app took the right back: there is nothing Maia can read, so nothing to file.
            }
        }

        Finish();
    }

    private bool CanRead(AndroidUri uri) =>
        CheckCallingOrSelfUriPermission(uri, ActivityFlags.GrantReadUriPermission) == global::Android.Content.PM.Permission.Granted;

    /// <summary>
    /// The files a share or "Open with" carries: Android moves a share's streams into its clip data when the other
    /// app sends it; the stream extra is read as well, for apps that start Maia some other way.
    /// </summary>
    private static List<AndroidUri> SharedUris(Intent? intent)
    {
        var uris = DocumentPicker.UrisOf(intent);
        if (uris.Count > 0 || intent is null)
        {
            return uris;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var type = Java.Lang.Class.FromType(typeof(AndroidUri));
            if (intent.GetParcelableExtra(Intent.ExtraStream, type) is AndroidUri single)
            {
                uris.Add(single);
            }
            else if (intent.GetParcelableArrayListExtra(Intent.ExtraStream, type) is { } list)
            {
                uris.AddRange(list.OfType<AndroidUri>());
            }

            return uris;
        }

#pragma warning disable CA1422, CS0618 // The typed overloads above exist only from Android 13.
        if (intent.GetParcelableExtra(Intent.ExtraStream) is AndroidUri older)
        {
            uris.Add(older);
        }
        else if (intent.GetParcelableArrayListExtra(Intent.ExtraStream) is { } olderList)
        {
            uris.AddRange(olderList.OfType<AndroidUri>());
        }
#pragma warning restore CA1422, CS0618

        return uris;
    }
}
