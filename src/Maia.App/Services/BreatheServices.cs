using Maia.Presentation.Calm;

namespace Maia.App.Services;

/// <summary>
/// Her Timer choices in the phone's preferences: the vibration on and the sound off until she changes them, and the
/// times she set. Settings, nothing about her plan.
/// </summary>
public sealed class PreferencesBreatheSettings : IBreatheSettings
{
    private const string VibrationKey = "breathe.vibration";
    private const string SoundKey = "breathe.sound";

    // The Breathing mode's keys, from when the Timer had two modes: the times she set there stay hers.
    private const string RhythmPrefix = "timer.breathing.";

    public Rhythm Rhythm
    {
        get => new(
            Preferences.Default.Get(RhythmPrefix + "first", Rhythm.Default.FirstSeconds),
            Preferences.Default.Get(RhythmPrefix + "second", Rhythm.Default.SecondSeconds),
            Preferences.Default.Get(RhythmPrefix + "rounds", Rhythm.Default.Rounds));
        set
        {
            Preferences.Default.Set(RhythmPrefix + "first", value.FirstSeconds);
            Preferences.Default.Set(RhythmPrefix + "second", value.SecondSeconds);
            Preferences.Default.Set(RhythmPrefix + "rounds", value.Rounds);
        }
    }

    public bool VibrationOn
    {
        get => Preferences.Default.Get(VibrationKey, true);
        set => Preferences.Default.Set(VibrationKey, value);
    }

    public bool SoundOn
    {
        get => Preferences.Default.Get(SoundKey, false);
        set => Preferences.Default.Set(SoundKey, value);
    }
}

/// <summary>Breathe's vibration is Android-only for now: elsewhere there is none, and no switch for it.</summary>
public sealed class NoBreathVibration : IBreathVibration
{
    public bool IsSupported => false;

    public void Cue(BreathCue cue)
    {
    }
}

/// <summary>Breathe's soft tones are Android-only for now: elsewhere there are none, and no switch for them.</summary>
public sealed class NoBreathSound : IBreathSound
{
    public bool IsSupported => false;

    public void Cue(BreathCue cue)
    {
    }

    public void Silence()
    {
    }
}
