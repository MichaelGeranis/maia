using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Maia.Application.Abstractions;
using Maia.Presentation.Calm;
using AudioEncoding = Android.Media.Encoding;

namespace Maia.App.Platforms.Android;

/// <summary>
/// Breathe's gentle vibration (see <see cref="IBreathVibration"/>): one soft pulse to breathe in, two to breathe out, a
/// longer one at the end, at a low strength where the phone can set one (shorter where it can't). From Android 13 it is
/// a media vibration, so the phone's own vibration settings ("Vibration &amp; haptics" off, media vibration off) stop it
/// there; before, "Touch feedback" off does. Never on silent or in Do not disturb. Android 8 and later; needs VIBRATE,
/// granted at install.
/// </summary>
public sealed class AndroidBreathVibration : IBreathVibration
{
    /// <summary>The strength of a pulse, of 255: soft, but still felt in a hand or on a lap.</summary>
    private const int Soft = 90;

    private readonly IErrorReporter _errors;
    private readonly Lazy<Vibrator?> _vibrator = new(Find);

    public AndroidBreathVibration(IErrorReporter errors) => _errors = errors;

    public bool IsSupported => OperatingSystem.IsAndroidVersionAtLeast(26) && _vibrator.Value?.HasVibrator == true;

    public void Cue(BreathCue cue)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

#pragma warning disable CA1031 // A cue that can't be given must never stop her breathing; the failure is reported by code.
        try
        {
            if (_vibrator.Value is not { HasVibrator: true } vibrator || PhoneQuiet.Now || TouchFeedbackOff())
            {
                return;
            }

            var effect = Effect(cue, vibrator.HasAmplitudeControl);
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                vibrator.Vibrate(effect, VibrationAttributes.CreateForUsage((int)VibrationAttributesUsageType.Media));
            }
            else
            {
                vibrator.Vibrate(effect, new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media)!.Build());
            }
        }
        catch (Exception exception)
        {
            _errors.Report(exception, "breathe.vibrate");
        }
#pragma warning restore CA1031
    }

    [SupportedOSPlatform("android26.0")]
    private static VibrationEffect Effect(BreathCue cue, bool soft)
    {
        // Without strength control every pulse is at full strength, so it is kept shorter.
        var amplitude = soft ? Soft : VibrationEffect.DefaultAmplitude;
        var scale = soft ? 1.0 : 0.5;
        long Ms(int milliseconds) => (long)(milliseconds * scale);

        return cue switch
        {
            BreathCue.In => VibrationEffect.CreateOneShot(Ms(90), amplitude)!,
            BreathCue.Out => VibrationEffect.CreateWaveform([0, Ms(70), 160, Ms(70)], [0, amplitude, 0, amplitude], -1)!,
            _ => VibrationEffect.CreateOneShot(Ms(400), amplitude)!,
        };
    }

    /// <summary>
    /// Below Android 13 the phone's "Touch feedback" switch is its haptics switch. From 13 the phone applies its own
    /// vibration settings to the media vibration.
    /// </summary>
    private static bool TouchFeedbackOff()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return false;
        }

        var resolver = global::Android.App.Application.Context.ContentResolver;
        return global::Android.Provider.Settings.System.GetInt(resolver, global::Android.Provider.Settings.System.HapticFeedbackEnabled, 1) == 0;
    }

    private static Vibrator? Find()
    {
        var context = global::Android.App.Application.Context;
        return OperatingSystem.IsAndroidVersionAtLeast(31)
            ? (context.GetSystemService(Context.VibratorManagerService) as VibratorManager)?.DefaultVibrator
            : context.GetSystemService(Context.VibratorService) as Vibrator;
    }
}

/// <summary>
/// Breathe's soft tones (see <see cref="IBreathSound"/>): made once by <see cref="BreathTones"/>, kept in memory and
/// played on the media volume, mixed with anything already playing rather than interrupting it. Never while the ringer
/// is on silent or Do not disturb is on; on vibrate it plays, as her music would, since vibrate only quiets the ringer.
/// The work runs off the screen's thread, one step at a time and in the order asked; <see cref="Silence"/> stops a tone
/// at once and lets the players go.
/// </summary>
public sealed class AndroidBreathSound : IBreathSound
{
    private static readonly Dictionary<BreathCue, Lazy<short[]>> Samples =
        Enum.GetValues<BreathCue>().ToDictionary(cue => cue, cue => new Lazy<short[]>(() => BreathTones.For(cue)));

    private readonly IErrorReporter _errors;
    private readonly Lock _gate = new();

    // Only ever touched by the queued steps, which run one after another.
    private readonly Dictionary<BreathCue, AudioTrack> _tracks = [];

    private Task _queue = Task.CompletedTask;

    // Silence moves this on, so a tone asked for before it never starts after it.
    private int _generation;

    public AndroidBreathSound(IErrorReporter errors) => _errors = errors;

    public bool IsSupported => true;

    public void Cue(BreathCue cue)
    {
        var generation = Volatile.Read(ref _generation);
        Enqueue(() => Play(cue, generation), "breathe.sound");
    }

    public void Silence()
    {
        Interlocked.Increment(ref _generation);
        Enqueue(ReleaseAll, "breathe.sound_release");
    }

    /// <summary>Runs <paramref name="step"/> after the steps asked for before it, off the screen's thread.</summary>
    private void Enqueue(Action step, string operation)
    {
        lock (_gate)
        {
            _queue = _queue.ContinueWith(_ => Run(step, operation), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    private void Run(Action step, string operation)
    {
#pragma warning disable CA1031 // A tone that can't play must never stop her breathing or end Maia; the failure is reported by code.
        try
        {
            step();
        }
        catch (Exception exception)
        {
            _errors.Report(exception, operation);
        }
#pragma warning restore CA1031
    }

    private void Play(BreathCue cue, int generation)
    {
        if (generation != Volatile.Read(ref _generation) || PhoneQuiet.Now)
        {
            return;
        }

        // One tone at a time: one still fading stops as the next begins.
        foreach (var playing in _tracks.Values.Where(t => t.PlayState == PlayState.Playing))
        {
            playing.Pause();
        }

        if (Track(cue) is not { } track)
        {
            return;
        }

        // A tone played before is wound back to its start.
        track.Stop();
        track.ReloadStaticData();
        track.Play();
    }

    private AudioTrack? Track(BreathCue cue)
    {
        if (_tracks.TryGetValue(cue, out var known))
        {
            return known;
        }

        var samples = Samples[cue].Value;
        using var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Sonification)!
            .Build()!;
        using var format = new AudioFormat.Builder()
            .SetSampleRate(BreathTones.SampleRate)!
            .SetEncoding(AudioEncoding.Pcm16bit)!
            .SetChannelMask(ChannelOut.Mono)!
            .Build()!;
        var track = new AudioTrack(attributes, format, samples.Length * sizeof(short), AudioTrackMode.Static, AudioManager.AudioSessionIdGenerate);
        if (track.Write(samples, 0, samples.Length) != samples.Length || track.State != AudioTrackState.Initialized)
        {
            track.Release();
            track.Dispose();
            return null;
        }

        _tracks[cue] = track;
        return track;
    }

    private void ReleaseAll()
    {
        foreach (var track in _tracks.Values)
        {
            track.Release();
            track.Dispose();
        }

        _tracks.Clear();
    }
}

/// <summary>
/// Whether the phone asks for quiet, as far as it tells an app: Do not disturb in any form, or the ringer on silent.
/// Read at each cue, so a change counts from the next breath.
/// </summary>
internal static class PhoneQuiet
{
    /// <summary>
    /// No vibration and no tone on silent or in Do not disturb. Vibrate is not quiet: it only swaps the ringer for a buzz
    /// while the media volume still plays, and many keep their phone on it all day, so Breathe's hints name only silent
    /// and Do not disturb.
    /// </summary>
    public static bool Now => DoNotDisturb || Ringer == RingerMode.Silent;

    private static Context App => global::Android.App.Application.Context;

    private static bool DoNotDisturb =>
        (App.GetSystemService(Context.NotificationService) as NotificationManager)?.CurrentInterruptionFilter
            is InterruptionFilter.Priority or InterruptionFilter.Alarms or InterruptionFilter.None;

    private static RingerMode Ringer => (App.GetSystemService(Context.AudioService) as AudioManager)?.RingerMode ?? RingerMode.Normal;
}
