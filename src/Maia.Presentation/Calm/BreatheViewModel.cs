using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Calm;

/// <summary>
/// The Timer (design screen 12), opened from Today: one timer for calm breathing or the exercises her doctor suggested.
/// A first part, then a second, a number of rounds, all as she sets them and remembered on this phone; it starts at 4
/// seconds, then 6, twelve times: two calm minutes. The sphere counts down each part, with the time left of the session
/// under it. Then it settles with "Well done" and can start again. She can pause at any time and resume where she was,
/// with the time left kept, or start over; leaving the screen ends it. The screen stays on while it runs, and a screen
/// reader hears each part. So she can close her eyes, a gentle vibration (on until she turns it off) and a soft tone
/// (off until she turns it on) mark the start of each part, and the end; both choices are remembered. Maia times; it
/// names and suggests no exercise and keeps no record of what she did. The view animates the sphere from
/// <see cref="IsInhaling"/>; with reduced motion only the words change, and the vibration and tone, which are not
/// motion, stay.
/// </summary>
public sealed partial class BreatheViewModel : ObservableObject
{
    // The rhythm moves a second at a time, so the time left counts down evenly. Both parts are whole seconds.
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    private readonly Func<TimeSpan, Task> _wait;
    private readonly TimeProvider _clock;
    private readonly IScreenAwake _screen;
    private readonly IAnnouncer _announcer;
    private readonly IBreathVibration _vibration;
    private readonly IBreathSound _sound;
    private readonly IBreatheSettings _settings;
    private readonly INavigator? _navigator;
    private bool _vibrationOn;
    private bool _soundOn;
    private Rhythm _rhythm;

    // Each start gets a new number; a rhythm whose number is no longer current ends at its next step.
    private int _run;

    // The whole seconds of the session she has done: kept while she pauses, so she resumes where she was.
    private TimeSpan _done;

    public BreatheViewModel(IScreenAwake screen, IAnnouncer announcer, IBreathVibration vibration, IBreathSound sound, IBreatheSettings settings, INavigator navigator)
        : this(Task.Delay, TimeProvider.System, screen, announcer, vibration, sound, settings, navigator)
    {
    }

    internal BreatheViewModel(
        Func<TimeSpan, Task> wait,
        TimeProvider clock,
        IScreenAwake screen,
        IAnnouncer announcer,
        IBreathVibration vibration,
        IBreathSound sound,
        IBreatheSettings settings,
        INavigator? navigator = null)
    {
        _navigator = navigator;
        _wait = wait;
        _clock = clock;
        _screen = screen;
        _announcer = announcer;
        _vibration = vibration;
        _sound = sound;
        _settings = settings;
        _vibrationOn = vibration.IsSupported && settings.VibrationOn;
        _soundOn = sound.IsSupported && settings.SoundOn;
        _rhythm = settings.Rhythm.Within();
        PhaseText = Text.Get("Breathe_Ready");
        ButtonText = Text.Get("Timer_Start");
        TimeLeft = string.Empty;
        PartLeft = string.Empty;
        TimeLeftSpoken = string.Empty;
        ShowTimeLeft(Length);
    }

    /// <summary>The times she set.</summary>
    public Rhythm Rhythm => _rhythm;

    /// <summary>The whole session: both parts, every round.</summary>
    public TimeSpan Length => _rhythm.Length;

    /// <summary>"4 seconds, then 6, 12 times. For calm breathing, or the exercises your doctor suggested."</summary>
    public string Intro => Text.Format(
        "Timer_Intro", Seconds(_rhythm.FirstSeconds), _rhythm.SecondSeconds, DisplayFormat.Count("Timer_OneTime", "Timer_Times", _rhythm.Rounds));

    /// <summary>The first part's name, in the sphere and the times card: "First".</summary>
    private static string FirstName => Text.Get("Timer_First");

    /// <summary>The second part's name: "Then".</summary>
    private static string SecondName => Text.Get("Timer_Then");

    /// <summary>"4 seconds".</summary>
    public string FirstValue => Seconds(_rhythm.FirstSeconds);

    /// <summary>"6 seconds".</summary>
    public string SecondValue => Seconds(_rhythm.SecondSeconds);

    /// <summary>"12".</summary>
    public string RoundsValue => _rhythm.Rounds.ToString(CultureInfo.CurrentCulture);

    /// <summary>What a screen reader hears for the first row: "First, 4 seconds".</summary>
    public string FirstSpoken => Text.Format("Timer_Setting", FirstName, FirstValue);

    /// <summary>"Then, 6 seconds".</summary>
    public string SecondSpoken => Text.Format("Timer_Setting", SecondName, SecondValue);

    /// <summary>"Rounds, 12".</summary>
    public string RoundsSpoken => Text.Format("Timer_Setting", Text.Get("Timer_Rounds"), RoundsValue);

    /// <summary>Running. Her times can be changed only while it isn't.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTimeLeft))]
    [NotifyCanExecuteChangedFor(nameof(ShorterFirstCommand), nameof(LongerFirstCommand), nameof(ShorterSecondCommand), nameof(LongerSecondCommand), nameof(FewerRoundsCommand), nameof(MoreRoundsCommand))]
    public partial bool IsBreathing { get; private set; }

    /// <summary>
    /// Paused part way: the time left stays, the button resumes from there, and "Start over" begins the session again.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTimeLeft))]
    public partial bool IsPaused { get; private set; }

    /// <summary>The time left shows while she breathes and while she has paused.</summary>
    public bool ShowsTimeLeft => IsBreathing || IsPaused;

    /// <summary>True during the first part, false during the second (and when stopped or paused).</summary>
    [ObservableProperty]
    public partial bool IsInhaling { get; private set; }

    /// <summary>
    /// How long the part that has just begun lasts: its full seconds, or what is left of it when she resumes part way,
    /// so the sphere grows or settles in step.
    /// </summary>
    public TimeSpan PhaseLeft { get; private set; }

    /// <summary>"Ready?", "First" / "Then" while it runs, "Paused", or "Well done" at the end.</summary>
    [ObservableProperty]
    public partial string PhaseText { get; private set; }

    /// <summary>"Start", "Pause", "Resume" while paused, or "Start again" at the end.</summary>
    [ObservableProperty]
    public partial string ButtonText { get; private set; }

    /// <summary>The seconds left of the part under way, "3", counted down in the sphere while it runs.</summary>
    [ObservableProperty]
    public partial string PartLeft { get; private set; }

    /// <summary>The time left of the session, "1:52"; shown while she breathes and while she has paused.</summary>
    [ObservableProperty]
    public partial string TimeLeft { get; private set; }

    /// <summary>The same in words for a screen reader, "Time left: 1 minute and 52 seconds".</summary>
    [ObservableProperty]
    public partial string TimeLeftSpoken { get; private set; }

    /// <summary>Whether this phone can vibrate: the vibration switch shows only then.</summary>
    public bool CanVibrate => _vibration.IsSupported;

    /// <summary>Whether this phone can play the tones: the sound switch shows only then.</summary>
    public bool CanPlaySound => _sound.IsSupported;

    /// <summary>Whether there is a choice to offer at all.</summary>
    public bool HasCueChoices => CanVibrate || CanPlaySound;

    /// <summary>Both switches show, with a line between them.</summary>
    public bool HasBothCueChoices => CanVibrate && CanPlaySound;

    /// <summary>
    /// The vibration switch: on until she turns it off, and remembered. Turned on while she isn't breathing, it gives
    /// one pulse, so she feels what it will be like.
    /// </summary>
    public bool VibrationOn
    {
        get => _vibrationOn;
        set
        {
            if (!SetProperty(ref _vibrationOn, value))
            {
                return;
            }

            _settings.VibrationOn = value;
            if (value && !IsBreathing && CanVibrate)
            {
                _vibration.Cue(BreathCue.In);
            }
        }
    }

    /// <summary>
    /// The sound switch: off until she turns it on, and remembered. Turned on while she isn't breathing, it plays the
    /// breath-in tone once, so she can set the volume; turned off, a tone still sounding stops.
    /// </summary>
    public bool SoundOn
    {
        get => _soundOn;
        set
        {
            if (!SetProperty(ref _soundOn, value))
            {
                return;
            }

            _settings.SoundOn = value;
            if (!value)
            {
                _sound.Silence();
            }
            else if (!IsBreathing && CanPlaySound)
            {
                _sound.Cue(BreathCue.In);
            }
        }
    }

    /// <summary>One second less for the first part, down to one.</summary>
    [RelayCommand(CanExecute = nameof(CanShortenFirst))]
    private void ShorterFirst() => Adjust(_rhythm with { FirstSeconds = _rhythm.FirstSeconds - 1 });

    /// <summary>One second more for the first part, up to thirty.</summary>
    [RelayCommand(CanExecute = nameof(CanLengthenFirst))]
    private void LongerFirst() => Adjust(_rhythm with { FirstSeconds = _rhythm.FirstSeconds + 1 });

    /// <summary>One second less for the second part, down to one.</summary>
    [RelayCommand(CanExecute = nameof(CanShortenSecond))]
    private void ShorterSecond() => Adjust(_rhythm with { SecondSeconds = _rhythm.SecondSeconds - 1 });

    /// <summary>One second more for the second part, up to thirty.</summary>
    [RelayCommand(CanExecute = nameof(CanLengthenSecond))]
    private void LongerSecond() => Adjust(_rhythm with { SecondSeconds = _rhythm.SecondSeconds + 1 });

    /// <summary>One round fewer, down to one.</summary>
    [RelayCommand(CanExecute = nameof(CanHaveFewerRounds))]
    private void FewerRounds() => Adjust(_rhythm with { Rounds = _rhythm.Rounds - 1 });

    /// <summary>One round more, up to fifty.</summary>
    [RelayCommand(CanExecute = nameof(CanHaveMoreRounds))]
    private void MoreRounds() => Adjust(_rhythm with { Rounds = _rhythm.Rounds + 1 });

    private bool CanShortenFirst() => !IsBreathing && _rhythm.FirstSeconds > Rhythm.MinSeconds;

    private bool CanLengthenFirst() => !IsBreathing && _rhythm.FirstSeconds < Rhythm.MaxSeconds;

    private bool CanShortenSecond() => !IsBreathing && _rhythm.SecondSeconds > Rhythm.MinSeconds;

    private bool CanLengthenSecond() => !IsBreathing && _rhythm.SecondSeconds < Rhythm.MaxSeconds;

    private bool CanHaveFewerRounds() => !IsBreathing && _rhythm.Rounds > Rhythm.MinRounds;

    private bool CanHaveMoreRounds() => !IsBreathing && _rhythm.Rounds < Rhythm.MaxRounds;

    /// <summary>"Back", to Today.</summary>
    [RelayCommand]
    private Task BackAsync() => _navigator?.GoBackAsync() ?? Task.CompletedTask;

    /// <summary>
    /// "Start", "Pause", "Resume" or "Start again": pausing keeps the time left, and resuming carries on
    /// from there, with the breath she paused in.
    /// </summary>
    [RelayCommand]
    private void Toggle()
    {
        if (IsBreathing)
        {
            Pause();
            return;
        }

        if (!IsPaused)
        {
            _done = TimeSpan.Zero;
        }

        Breathe();
    }

    /// <summary>"Start over", while paused: the session begins again from the first part.</summary>
    [RelayCommand]
    private void StartOver()
    {
        _run++;
        _done = TimeSpan.Zero;
        Breathe();
    }

    /// <summary>
    /// Pauses, keeping the time left: the vibration and tone stop with the rhythm, a tone still sounding stops, and the
    /// screen may sleep again. Also when Maia goes to the background (home, another app, the screen turned off), so
    /// nothing buzzes in her pocket and she can carry on when she comes back.
    /// </summary>
    public void Pause()
    {
        if (!IsBreathing)
        {
            return;
        }

        Settle(Text.Get("Breathe_Paused"), Text.Get("Breathe_Resume"));
        IsPaused = true;
    }

    /// <summary>
    /// Stops breathing and starts afresh, for example when she leaves the screen: a tone still sounding stops, and the
    /// screen may sleep again.
    /// </summary>
    public void Stop()
    {
        Settle(Text.Get("Breathe_Ready"), Text.Get("Timer_Start"));
        _done = TimeSpan.Zero;
        IsPaused = false;
        ShowTimeLeft(Length);
    }

    private void Breathe()
    {
        IsPaused = false;
        IsBreathing = true;
        ButtonText = Text.Get("Breathe_Pause");
        _screen.KeepOn(true);
        _ = RunAsync(++_run);
    }

    private async Task RunAsync(int run)
    {
        // Her times can't change while it runs, and a change while paused starts afresh, so the rhythm holds throughout.
        var rhythm = _rhythm;
        var started = _clock.GetTimestamp();
        var from = _done;
        while (_done < rhythm.Length)
        {
            // Each part begins with its words and cue. Resumed part way, the part she paused in begins again for what
            // is left of it, so she knows which way to go.
            var intoRound = TimeSpan.FromTicks(_done.Ticks % (rhythm.First + rhythm.Second).Ticks);
            if (intoRound == TimeSpan.Zero || (_done == from && intoRound < rhythm.First))
            {
                Begin(BreathCue.In, rhythm.First - intoRound);
            }
            else if (intoRound == rhythm.First || _done == from)
            {
                Begin(BreathCue.Out, rhythm.First + rhythm.Second - intoRound);
            }

            var partLeft = intoRound < rhythm.First ? rhythm.First - intoRound : rhythm.First + rhythm.Second - intoRound;
            PartLeft = ((int)partLeft.TotalSeconds).ToString(CultureInfo.CurrentCulture);
            ShowTimeLeft(rhythm.Length - _done);

            // Each second is timed from the start, so a late one is made up and two minutes stay two minutes.
            var due = _done - from + Tick - _clock.GetElapsedTime(started);
            await _wait(due > TimeSpan.Zero ? due : TimeSpan.Zero);
            if (run != _run)
            {
                return;
            }

            _done += Tick;
        }

        // The whole session: a quiet finish, felt or heard too, and the choice to go again. The screen may sleep only
        // once all of it is done, so "may sleep" always means the session has ended.
        Settle(Text.Get("Breathe_Finished"), Text.Get("Timer_Again"), keepScreenOn: true);
        _done = TimeSpan.Zero;
        ShowTimeLeft(Length);
        _announcer.Announce(PhaseText);
        Cue(BreathCue.Finished);
        _screen.KeepOn(false);
    }

    private void Begin(BreathCue cue, TimeSpan left)
    {
        PhaseLeft = left;
        IsInhaling = cue == BreathCue.In;
        PhaseText = cue == BreathCue.In ? FirstName : SecondName;
        _announcer.Announce(PhaseText);
        Cue(cue);
    }

    private void Cue(BreathCue cue)
    {
        if (VibrationOn && CanVibrate)
        {
            _vibration.Cue(cue);
        }

        if (SoundOn && CanPlaySound)
        {
            _sound.Cue(cue);
        }
    }

    private void ShowTimeLeft(TimeSpan left)
    {
        var minutes = (int)left.TotalMinutes;
        var seconds = left.Seconds;
        TimeLeft = string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, seconds);

        var minutesSpoken = DisplayFormat.Count("Breathe_OneMinute", "Breathe_Minutes", minutes);
        var secondsSpoken = DisplayFormat.Count("Breathe_OneSecond", "Breathe_Seconds", seconds);
        TimeLeftSpoken = Text.Format("Breathe_TimeLeft", (minutes, seconds) switch
        {
            (0, _) => secondsSpoken,
            (_, 0) => minutesSpoken,
            _ => Text.Format("Breathe_MinutesAndSeconds", minutesSpoken, secondsSpoken),
        });
    }

    /// <summary>The rhythm ends: the sphere rests, a tone still sounding stops, and the screen may sleep again.</summary>
    private void Settle(string phaseText, string buttonText, bool keepScreenOn = false)
    {
        _run++;
        IsBreathing = false;
        IsInhaling = false;
        PhaseText = phaseText;
        ButtonText = buttonText;
        _sound.Silence();
        if (!keepScreenOn)
        {
            _screen.KeepOn(false);
        }
    }

    /// <summary>A new time, kept on this phone. A paused session starts afresh with it.</summary>
    private void Adjust(Rhythm rhythm)
    {
        _rhythm = rhythm;
        _settings.Rhythm = rhythm;
        Stop();
        OnPropertyChanged(nameof(Rhythm));
        OnPropertyChanged(nameof(Length));
        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(FirstValue));
        OnPropertyChanged(nameof(SecondValue));
        OnPropertyChanged(nameof(RoundsValue));
        OnPropertyChanged(nameof(FirstSpoken));
        OnPropertyChanged(nameof(SecondSpoken));
        OnPropertyChanged(nameof(RoundsSpoken));
        NotifyAdjusters();
    }

    private void NotifyAdjusters()
    {
        ShorterFirstCommand.NotifyCanExecuteChanged();
        LongerFirstCommand.NotifyCanExecuteChanged();
        ShorterSecondCommand.NotifyCanExecuteChanged();
        LongerSecondCommand.NotifyCanExecuteChanged();
        FewerRoundsCommand.NotifyCanExecuteChanged();
        MoreRoundsCommand.NotifyCanExecuteChanged();
    }

    private static string Seconds(int seconds) => DisplayFormat.Count("Breathe_OneSecond", "Breathe_Seconds", seconds);
}
