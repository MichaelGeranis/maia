using Android.Content;
using Android.OS;
using Maia.Presentation.Results;
using FileProvider = AndroidX.Core.Content.FileProvider;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Opens the phone's share sheet with results decrypted into Maia's private cache: one file, or several at once
/// (email, WhatsApp, Viber…). The app she picks gets a read grant on just those files through Maia's file provider, and
/// Maia's own "Share" entry is left out of the list, so they are not filed into Maia again.
/// </summary>
internal static class ResultSharer
{
    public static void Share(IReadOnlyList<(string Path, string MediaType)> files, string title)
    {
        ArgumentNullException.ThrowIfNull(files);
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to share the files from.");
        var uris = files
            .Select(file => FileProvider.GetUriForFile(activity, activity.PackageName + ".fileProvider", new Java.IO.File(file.Path))
                ?? throw new IOException("The file could not be shared."))
            .ToList();
        if (uris.Count == 0)
        {
            return;
        }

        Intent send;
        if (uris.Count == 1)
        {
            send = new Intent(Intent.ActionSend);
            send.PutExtra(Intent.ExtraStream, uris[0]);
        }
        else
        {
            send = new Intent(Intent.ActionSendMultiple);
            send.PutParcelableArrayListExtra(Intent.ExtraStream, uris.Cast<IParcelable>().ToList());
        }

        send.SetType(ShareFiles.CommonType(files.Select(f => f.MediaType)));

        // The grant reaches the receiving app through the clip, on every Android version.
        var clip = ClipData.NewRawUri(string.Empty, uris[0])!;
        foreach (var uri in uris.Skip(1))
        {
            clip.AddItem(new ClipData.Item(uri));
        }

        send.ClipData = clip;
        send.AddFlags(ActivityFlags.GrantReadUriPermission);

        var chooser = Intent.CreateChooser(send, title) ?? send;
        chooser.PutExtra(Intent.ExtraExcludeComponents, new IParcelable[] { new ComponentName(activity, Java.Lang.Class.FromType(typeof(ShareActivity))) });
        chooser.AddFlags(ActivityFlags.GrantReadUriPermission);
        activity.StartActivity(chooser);
    }
}
