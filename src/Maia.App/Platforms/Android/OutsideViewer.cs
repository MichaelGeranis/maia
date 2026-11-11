using Android.Content;
using Android.OS;
using FileProvider = AndroidX.Core.Content.FileProvider;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Opens a decrypted file from Maia's private cache in another app, for the few files Maia can't draw itself. The
/// app gets a read grant on that one file through Maia's file provider (no second copy, unlike MAUI's Launcher), and
/// Maia's own "Open with" entry is left out of the list, so the file is not filed into Maia again.
/// </summary>
internal static class OutsideViewer
{
    public static void Open(string path, string mediaType, string title)
    {
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to open the file from.");
        var uri = FileProvider.GetUriForFile(activity, activity.PackageName + ".fileProvider", new Java.IO.File(path))
            ?? throw new IOException("The file could not be shared.");

        var view = new Intent(Intent.ActionView);
        view.SetDataAndType(uri, mediaType);
        view.AddFlags(ActivityFlags.GrantReadUriPermission);

        var chooser = Intent.CreateChooser(view, title) ?? view;
        chooser.PutExtra(Intent.ExtraExcludeComponents, new IParcelable[] { new ComponentName(activity, Java.Lang.Class.FromType(typeof(ShareActivity))) });
        chooser.AddFlags(ActivityFlags.GrantReadUriPermission);
        activity.StartActivity(chooser);
    }
}
