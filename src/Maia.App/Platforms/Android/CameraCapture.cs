using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Maia.Presentation.Results;
using FileProvider = AndroidX.Core.Content.FileProvider;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Takes one photo with the phone's camera app, written straight into Maia's private cache through its file
/// provider. Maia holds no camera permission (the camera app has its own) and no storage permission; MAUI's
/// MediaPicker would ask for both on older Android versions. The photo is deleted once it has been filed.
/// </summary>
internal static class CameraCapture
{
    private const int RequestCode = 4473;
    private static TaskCompletionSource<bool>? _pending;

    /// <summary>Private cache folder the camera app writes into; emptied with the rest of the cache at start-up.</summary>
    public static string Folder => Path.Combine(FileSystem.CacheDirectory, "camera");

    public static bool IsAvailable
    {
        get
        {
            var context = Platform.AppContext;
            return context.PackageManager is { } packages
                && packages.HasSystemFeature(PackageManager.FeatureCameraAny)
                && new Intent(MediaStore.ActionImageCapture).ResolveActivity(packages) is not null;
        }
    }

    public static async Task<PickedFile?> TakeAsync()
    {
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to open the camera from.");
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".jpg");
        var uri = FileProvider.GetUriForFile(activity, activity.PackageName + ".fileProvider", new Java.IO.File(path))
            ?? throw new CameraUnavailableException();

        var intent = new Intent(MediaStore.ActionImageCapture);
        intent.PutExtra(MediaStore.ExtraOutput, uri);
        intent.ClipData = ClipData.NewRawUri(string.Empty, uri);
        intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
        if (activity.PackageManager is not { } packages || intent.ResolveActivity(packages) is null)
        {
            throw new CameraUnavailableException();
        }

        _pending?.TrySetResult(false);
        var pending = new TaskCompletionSource<bool>();
        _pending = pending;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => activity.StartActivityForResult(intent, RequestCode));
        }
        catch (ActivityNotFoundException ex)
        {
            throw new CameraUnavailableException("No camera app.", ex);
        }
        catch (Java.Lang.SecurityException ex)
        {
            throw new CameraUnavailableException("The camera is switched off on this phone.", ex);
        }

        var taken = await pending.Task;
        var file = new FileInfo(path);
        if (!taken || !file.Exists || file.Length == 0)
        {
            Delete(path);
            return null;
        }

        return new PickedFile("photo.jpg", "image/jpeg", file.Length, () => Task.FromResult<Stream>(File.OpenRead(path)))
        {
            Cleanup = () => Delete(path),
        };
    }

    /// <summary>Called from <see cref="MainActivity.OnActivityResult"/>.</summary>
    public static bool OnResult(int requestCode, Result resultCode)
    {
        if (requestCode != RequestCode)
        {
            return false;
        }

        _pending?.TrySetResult(resultCode == Result.Ok);
        _pending = null;
        return true;
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Emptied with the rest of the cache at the next start.
        }
    }
}
