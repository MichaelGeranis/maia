using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.Content;
using Maia.Application.Abstractions;
using Maia.Presentation.Results;
using AndroidUri = Android.Net.Uri;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Hands files shared with Maia (see <see cref="ShareActivity"/>) to <see cref="ShareIntake"/>. Maia's main screen is
/// open to every app (it is the launcher's), so it takes files only from <see cref="ShareActivity"/>: that one stamps
/// what it passes on with a token made fresh each time Maia starts, which no other app can know. Without it, any app
/// could start Maia with files of its choosing, and Maia would read them with its own rights and keep them.
/// </summary>
internal static class SharedIntents
{
    public const string FileResultsAction = "com.michailgeranis.maia.action.FILE_RESULTS";

    private const string ExtraToken = "com.michailgeranis.maia.extra.SHARE_TOKEN";

    private static readonly byte[] Token = Encoding.ASCII.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    /// <summary>Marks an intent as passed on by <see cref="ShareActivity"/>.</summary>
    public static void Stamp(Intent intent) => intent.PutExtra(ExtraToken, Encoding.ASCII.GetString(Token));

    /// <summary>
    /// Whether a file is another app's to hand over: a content link from a provider that isn't Maia's own. A plain
    /// file:// path, or a link into Maia's own file provider, would have Maia read its own private files.
    /// </summary>
    public static bool IsFromAnotherApp(Context context, AndroidUri uri) =>
        string.Equals(uri.Scheme, ContentResolver.SchemeContent, StringComparison.OrdinalIgnoreCase)
        && uri.Authority is { Length: > 0 } authority
        && context.PackageName is { Length: > 0 } own
        && !string.Equals(authority, own, StringComparison.OrdinalIgnoreCase)
        && !authority.StartsWith(own + ".", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Takes in the files an intent carries. On a cold start the first screen opens Results itself; when Maia is
    /// already running (<paramref name="open"/>), Results is opened here.
    /// </summary>
    public static void Receive(Activity activity, Intent? intent, bool open)
    {
        // Reopened from recents, the old intent comes back: it was filed or put aside already.
        if (intent?.Action != FileResultsAction || intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory))
        {
            return;
        }

        intent.SetAction(Intent.ActionMain);

        // Started by another app, not by ShareActivity: nothing is read.
        if (!IsStamped(intent))
        {
            return;
        }

        var services = IPlatformApplication.Current?.Services;
        if (services?.GetService<ShareIntake>() is not { } intake)
        {
            return;
        }

        var files = DocumentPicker.UrisOf(intent)
            .Where(uri => IsFromAnotherApp(activity, uri))
            .Select(uri => ContentFiles.From(activity, uri))
            .ToList();
        if (files.Count == 0)
        {
            return;
        }

        intake.Receive(files);
        if (open)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await intake.OpenAsync();
                }
#pragma warning disable CA1031 // Opening Results is best effort: the files stay waiting there; failures are reported by code.
                catch (Exception ex)
                {
                    services.GetService<IErrorReporter>()?.Report(ex, "share.open");
                }
#pragma warning restore CA1031
            });
        }
    }

    private static bool IsStamped(Intent intent)
    {
        string? token;
        try
        {
            token = intent.GetStringExtra(ExtraToken);
        }
        catch (Java.Lang.RuntimeException)
        {
            // Extras another app packed so they can't be read: not from ShareActivity.
            return false;
        }

        return token is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(token), Token);
    }
}
