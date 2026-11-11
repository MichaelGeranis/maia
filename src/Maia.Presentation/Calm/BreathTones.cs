namespace Maia.Presentation.Calm;

/// <summary>
/// The soft tones Breathe can play, made from a few sine waves on the phone (no sound files, nothing to license): a
/// swell that rises and fades for a breath in, a lower one that sighs away for a breath out, and a quiet chord at the
/// end. Each starts and ends in silence, so nothing clicks, stays well below full volume, and is gone before the next
/// breath begins. 16-bit mono samples at <see cref="SampleRate"/>.
/// </summary>
public static class BreathTones
{
    /// <summary>Samples a second: plenty for tones this low.</summary>
    public const int SampleRate = 22050;

    /// <summary>The loudest a tone gets, as a share of full scale: quiet even with the media volume turned up.</summary>
    public const double Peak = 0.18;

    // A breath in: C and G, swelling for most of the breath, then fading.
    private static readonly Tone InTone = new([261.63, 392.00], Seconds: 3.4, Rise: 1.5);

    // A breath out: a lower G and D that arrive softly and sigh away over most of the breath.
    private static readonly Tone OutTone = new([196.00, 293.66], Seconds: 4.8, Rise: 0.5);

    // The end: a quiet C major chord, like a small bell.
    private static readonly Tone FinishedTone = new([261.63, 329.63, 392.00], Seconds: 3.2, Rise: 0.06);

    /// <summary>How long the tone for <paramref name="cue"/> lasts.</summary>
    public static TimeSpan Length(BreathCue cue) => TimeSpan.FromSeconds(ToneFor(cue).Seconds);

    /// <summary>The samples of the tone for <paramref name="cue"/>.</summary>
    public static short[] For(BreathCue cue) => Synthesize(ToneFor(cue));

    private static Tone ToneFor(BreathCue cue) => cue switch
    {
        BreathCue.In => InTone,
        BreathCue.Out => OutTone,
        _ => FinishedTone,
    };

    private static short[] Synthesize(Tone tone)
    {
        var count = (int)(tone.Seconds * SampleRate);
        var samples = new short[count];
        var end = (double)(count - 1) / SampleRate;

        // The first note leads; the others sit a little lower. Each note has a faint octave to warm it, and a twin a
        // hair sharper, whose slow beat gives the tone its shimmer.
        var weights = tone.Notes.Select((_, i) => i == 0 ? 1.0 : 0.6).ToArray();
        var total = weights.Sum() * 2.2;
        for (var n = 0; n < count; n++)
        {
            var t = (double)n / SampleRate;
            var sum = 0.0;
            for (var i = 0; i < tone.Notes.Count; i++)
            {
                var f = tone.Notes[i];
                sum += weights[i] * (Math.Sin(2 * Math.PI * f * t)
                    + (0.8 * Math.Sin(2 * Math.PI * f * 1.003 * t))
                    + (0.4 * Math.Sin(2 * Math.PI * f * 2 * t)));
            }

            samples[n] = (short)Math.Round(sum / total * Envelope(tone, t, end) * Peak * short.MaxValue);
        }

        return samples;
    }

    /// <summary>A smooth rise from silence, then a smooth fade back to it at the last sample, at <paramref name="end"/>.</summary>
    private static double Envelope(Tone tone, double t, double end)
    {
        if (t < tone.Rise)
        {
            return 0.5 * (1 - Math.Cos(Math.PI * t / tone.Rise));
        }

        var fade = (t - tone.Rise) / (end - tone.Rise);
        var level = 0.5 * (1 + Math.Cos(Math.PI * Math.Min(fade, 1)));
        return level * level;
    }

    private sealed record Tone(IReadOnlyList<double> Notes, double Seconds, double Rise);
}
