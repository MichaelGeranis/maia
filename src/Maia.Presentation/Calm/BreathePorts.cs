namespace Maia.Presentation.Calm;

/// <summary>Keeps the phone's screen on, so it doesn't dim or lock while she breathes. Implemented by the app.</summary>
public interface IScreenAwake
{
    void KeepOn(bool keepOn);
}

/// <summary>Says a short text to her screen reader, when one is on. Implemented by the app.</summary>
public interface IAnnouncer
{
    void Announce(string text);
}

/// <summary>What a cue marks: the start of the first part, of the second, or the end of the session.</summary>
public enum BreathCue
{
    In,
    Out,
    Finished,
}

/// <summary>
/// A gentle vibration as each breath begins, so she can breathe with her eyes closed. Implemented by the app (Android);
/// elsewhere it is not supported and Breathe offers no switch for it.
/// </summary>
public interface IBreathVibration
{
    /// <summary>Whether this phone can vibrate.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// One soft pulse to breathe in, two to breathe out, a longer one at the end. Nothing while the phone is on silent
    /// or Do not disturb, or has vibration turned off, as far as the phone says so. Never throws.
    /// </summary>
    void Cue(BreathCue cue);
}

/// <summary>
/// A soft tone as each breath begins, made on the phone (no sound files), quiet and on the media volume. Implemented by
/// the app (Android); elsewhere it is not supported and Breathe offers no switch for it.
/// </summary>
public interface IBreathSound
{
    /// <summary>Whether this phone can play the tones.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Plays the tone for <paramref name="cue"/>; nothing while the phone is on silent or in Do not disturb, as far as
    /// the phone says so (on vibrate it plays, as media does). Never throws.
    /// </summary>
    void Cue(BreathCue cue);

    /// <summary>Stops a tone still sounding, at once. Never throws.</summary>
    void Silence();
}

/// <summary>
/// Her Timer choices on this phone, kept between visits: the vibration is on until she turns it off, the sound off
/// until she turns it on, and the times she set. Settings, not health data. Implemented by the app.
/// </summary>
public interface IBreatheSettings
{
    bool VibrationOn { get; set; }

    bool SoundOn { get; set; }

    /// <summary>The times she set, or <see cref="Rhythm.Default"/>; the caller keeps them within the limits.</summary>
    Rhythm Rhythm { get; set; }
}
