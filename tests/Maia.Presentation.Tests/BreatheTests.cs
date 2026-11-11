using System.Threading.Channels;
using Maia.Presentation.Calm;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Tests;

public sealed class BreatheTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly ManualTimeProvider _clock = new();
    private readonly ControlledWait _wait;
    private readonly FakeScreen _screen = new();
    private readonly FakeCues _cues = new();
    private readonly InMemoryBreatheSettings _settings = new();

    public BreatheTests() => _wait = new ControlledWait(_clock);

    public void Dispose() => _culture.Dispose();

    private BreatheViewModel Create() => new(_wait.WaitAsync, _clock, _screen, _screen, _cues, _cues, _settings);

    [Fact]
    public void It_waits_until_she_is_ready()
    {
        var vm = Create();

        Assert.False(vm.IsBreathing);
        Assert.Equal("Ready?", vm.PhaseText);
        Assert.Equal("Start", vm.ButtonText);
        Assert.False(_screen.KeptOn);
        Assert.Empty(_cues.Felt);
        Assert.Empty(_cues.Heard);
    }

    [Fact]
    public async Task Breathes_in_for_four_seconds_and_out_for_six()
    {
        var vm = Create();

        vm.ToggleCommand.Execute(null);
        Assert.Equal(("First", true, "Pause"), (vm.PhaseText, vm.IsInhaling, vm.ButtonText));

        await _wait.PassAsync(3);
        Assert.Equal("First", vm.PhaseText);
        await _wait.PassAsync(1);
        Assert.Equal(("Then", false), (vm.PhaseText, vm.IsInhaling));

        await _wait.PassAsync(5);
        Assert.Equal("Then", vm.PhaseText);
        await _wait.PassAsync(1);
        Assert.Equal(("First", true), (vm.PhaseText, vm.IsInhaling));
    }

    [Fact]
    public async Task The_time_left_of_the_two_minutes_counts_down_each_second()
    {
        var vm = Create();

        vm.ToggleCommand.Execute(null);
        Assert.Equal(("2:00", "Time left: 2 minutes"), (vm.TimeLeft, vm.TimeLeftSpoken));

        await _wait.PassAsync(1);
        Assert.Equal(("1:59", "Time left: 1 minute and 59 seconds"), (vm.TimeLeft, vm.TimeLeftSpoken));

        await _wait.PassAsync(59);
        Assert.Equal(("1:00", "Time left: 1 minute"), (vm.TimeLeft, vm.TimeLeftSpoken));

        await _wait.PassAsync(1);
        Assert.Equal(("0:59", "Time left: 59 seconds"), (vm.TimeLeft, vm.TimeLeftSpoken));

        await _wait.PassAsync(58);
        Assert.Equal(("0:01", "Time left: 1 second"), (vm.TimeLeft, vm.TimeLeftSpoken));
    }

    [Fact]
    public async Task In_greek_the_time_left_reads_naturally()
    {
        using var greek = new CultureScope("el-GR");
        var vm = Create();

        vm.ToggleCommand.Execute(null);
        Assert.Equal("Χρόνος που μένει: 2 λεπτά", vm.TimeLeftSpoken);

        await _wait.PassAsync(1);
        Assert.Equal(("1:59", "Χρόνος που μένει: 1 λεπτό και 59 δευτερόλεπτα"), (vm.TimeLeft, vm.TimeLeftSpoken));
    }

    [Fact]
    public async Task A_second_that_ends_late_is_made_up_so_two_minutes_stay_two_minutes()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);

        Assert.Equal(TimeSpan.FromSeconds(1), await _wait.NextAsync());
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        _wait.Release();

        Assert.Equal(TimeSpan.FromMilliseconds(700), await _wait.NextAsync());
        Assert.Equal("1:59", vm.TimeLeft);
    }

    [Fact]
    public async Task Pausing_keeps_the_time_left_and_offers_to_resume_or_start_over()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(6);
        await _wait.NextAsync();
        var felt = _cues.Felt.Count;

        vm.ToggleCommand.Execute(null);

        Assert.False(vm.IsBreathing);
        Assert.True(vm.IsPaused);
        Assert.False(vm.IsInhaling);
        Assert.True(vm.ShowsTimeLeft);
        Assert.Equal(("Paused", "Resume", "1:54"), (vm.PhaseText, vm.ButtonText, vm.TimeLeft));
        Assert.Equal("Time left: 1 minute and 54 seconds", vm.TimeLeftSpoken);
        Assert.False(_screen.KeptOn);

        // The second she paused in ends, but nothing moves on, buzzes or sounds while she is paused.
        _wait.Release();
        await Task.Delay(50);
        Assert.False(_wait.HasPending);
        Assert.Equal(("Paused", "1:54"), (vm.PhaseText, vm.TimeLeft));
        Assert.Equal(felt, _cues.Felt.Count);
    }

    [Fact]
    public async Task Resuming_carries_on_with_the_time_left_and_the_breath_she_paused_in()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(6);
        await _wait.NextAsync();
        vm.ToggleCommand.Execute(null);
        _cues.Felt.Clear();
        _screen.Announced.Clear();

        vm.ToggleCommand.Execute(null);

        // She paused two seconds into breathing out: it begins again, felt and heard, for the four seconds left of it.
        Assert.True(vm.IsBreathing);
        Assert.False(vm.IsPaused);
        Assert.True(_screen.KeptOn);
        Assert.Equal(("Then", "Pause", "1:54"), (vm.PhaseText, vm.ButtonText, vm.TimeLeft));
        Assert.Equal(TimeSpan.FromSeconds(4), vm.PhaseLeft);
        Assert.Equal(new[] { BreathCue.Out }, _cues.Felt);
        Assert.Equal(new[] { "Then" }, _screen.Announced);

        Assert.Equal(TimeSpan.FromSeconds(1), await _wait.NextAsync());
        _wait.Release();
        await _wait.PassAsync(3);
        Assert.Equal(("First", "1:50"), (vm.PhaseText, vm.TimeLeft));
        Assert.Equal(new[] { BreathCue.Out, BreathCue.In }, _cues.Felt);
    }

    [Fact]
    public async Task Resumed_while_breathing_in_the_sphere_grows_in_what_is_left_of_the_breath()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        Assert.Equal(Rhythm.Default.First, vm.PhaseLeft);
        await _wait.PassAsync(3);
        await _wait.NextAsync();
        vm.ToggleCommand.Execute(null);

        vm.ToggleCommand.Execute(null);

        Assert.Equal(("First", true, "1:57"), (vm.PhaseText, vm.IsInhaling, vm.TimeLeft));
        Assert.Equal(TimeSpan.FromSeconds(1), vm.PhaseLeft);
        await _wait.NextAsync();
        _wait.Release();
        await _wait.PassAsync(0);
        Assert.Equal(("Then", Rhythm.Default.Second), (vm.PhaseText, vm.PhaseLeft));
    }

    [Fact]
    public async Task Start_over_begins_the_two_minutes_again()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(30);
        await _wait.NextAsync();
        vm.ToggleCommand.Execute(null);
        _cues.Felt.Clear();

        vm.StartOverCommand.Execute(null);

        Assert.True(vm.IsBreathing);
        Assert.False(vm.IsPaused);
        Assert.Equal(("First", "Pause", "2:00"), (vm.PhaseText, vm.ButtonText, vm.TimeLeft));
        Assert.Equal(new[] { BreathCue.In }, _cues.Felt);
        Assert.Equal(TimeSpan.FromSeconds(1), await _wait.NextAsync());
        _wait.Release();
        await _wait.PassAsync(0);
        Assert.Equal("1:59", vm.TimeLeft);
    }

    [Fact]
    public async Task Leaving_the_screen_after_a_pause_starts_afresh()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(20);
        await _wait.NextAsync();
        vm.ToggleCommand.Execute(null);

        vm.Stop();

        Assert.False(vm.IsPaused);
        Assert.False(vm.ShowsTimeLeft);
        Assert.Equal(("Ready?", "Start", "2:00"), (vm.PhaseText, vm.ButtonText, vm.TimeLeft));

        vm.ToggleCommand.Execute(null);
        Assert.Equal(("First", "2:00"), (vm.PhaseText, vm.TimeLeft));
    }

    [Fact]
    public void Pausing_when_she_is_not_breathing_changes_nothing()
    {
        var vm = Create();

        vm.Pause();

        Assert.False(vm.IsPaused);
        Assert.Equal(("Ready?", "Start"), (vm.PhaseText, vm.ButtonText));
    }

    [Fact]
    public async Task In_greek_pause_reads_resume_and_start_over()
    {
        using var greek = new CultureScope("el-GR");
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.NextAsync();

        vm.ToggleCommand.Execute(null);

        Assert.Equal(("Σε παύση", "Συνέχεια"), (vm.PhaseText, vm.ButtonText));
        Assert.Equal("Από την αρχή", Text.Get("Breathe_StartOver"));
    }

    [Fact]
    public async Task After_two_minutes_it_settles_calmly_and_can_start_again()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);

        var waited = await _wait.PassAsync((int)Rhythm.Default.Length.TotalSeconds - 1);
        waited += await _wait.NextAsync();
        _wait.Release();

        await _screen.SleepsAsync();
        Assert.Equal(TimeSpan.FromMinutes(2), waited);
        Assert.False(vm.IsBreathing);
        Assert.False(vm.IsInhaling);
        Assert.Equal(("Well done", "Start again"), (vm.PhaseText, vm.ButtonText));
        Assert.False(_wait.HasPending);

        vm.ToggleCommand.Execute(null);
        Assert.Equal(TimeSpan.FromSeconds(1), await _wait.NextAsync());
        Assert.True(vm.IsBreathing);
        Assert.Equal(("First", "2:00"), (vm.PhaseText, vm.TimeLeft));
    }

    [Fact]
    public async Task The_screen_stays_on_while_she_breathes_and_may_sleep_once_she_stops()
    {
        var vm = Create();

        vm.ToggleCommand.Execute(null);
        await _wait.NextAsync();
        Assert.True(_screen.KeptOn);

        vm.Stop();
        Assert.False(_screen.KeptOn);
    }

    [Fact]
    public async Task A_screen_reader_hears_each_phase_and_the_finish()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);

        await BreatheToTheEndAsync();

        Assert.Equal(new[] { "First", "Then" }, _screen.Announced.Take(2));
        Assert.Equal((Rhythm.Default.Rounds * 2) + 1, _screen.Announced.Count);
        Assert.Equal("Well done", _screen.Announced[^1]);
        Assert.False(vm.IsBreathing);
    }

    [Fact]
    public async Task A_gentle_vibration_marks_each_breath_in_and_out_and_the_end()
    {
        var vm = Create();

        Assert.True(vm.VibrationOn);
        vm.ToggleCommand.Execute(null);
        Assert.Equal(new[] { BreathCue.In }, _cues.Felt);

        await _wait.PassAsync(4);
        Assert.Equal(new[] { BreathCue.In, BreathCue.Out }, _cues.Felt);

        await _wait.PassAsync(6);
        Assert.Equal(new[] { BreathCue.In, BreathCue.Out, BreathCue.In }, _cues.Felt);

        await _wait.PassAsync((int)Rhythm.Default.Length.TotalSeconds - 11);
        await _wait.NextAsync();
        _wait.Release();
        await _screen.SleepsAsync();

        Assert.Equal((Rhythm.Default.Rounds * 2) + 1, _cues.Felt.Count);
        Assert.Equal(BreathCue.Finished, _cues.Felt[^1]);
        Assert.Empty(_cues.Heard);
    }

    [Fact]
    public async Task With_the_vibration_off_the_phone_stays_still_and_the_choice_is_kept()
    {
        var vm = Create();

        vm.VibrationOn = false;
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(4);

        Assert.Empty(_cues.Felt);
        Assert.False(_settings.VibrationOn);
        Assert.False(Create().VibrationOn);
    }

    [Fact]
    public void Turning_the_vibration_back_on_gives_one_pulse_so_she_feels_it()
    {
        _settings.VibrationOn = false;
        var vm = Create();
        Assert.False(vm.VibrationOn);

        vm.VibrationOn = true;

        Assert.Equal(new[] { BreathCue.In }, _cues.Felt);
        Assert.True(_settings.VibrationOn);
    }

    [Fact]
    public async Task The_soft_sound_is_off_until_she_turns_it_on_and_then_marks_each_breath()
    {
        var vm = Create();
        Assert.False(vm.SoundOn);

        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(4);
        Assert.Empty(_cues.Heard);

        // Turned on while she breathes, it waits for the next breath.
        vm.SoundOn = true;
        Assert.Empty(_cues.Heard);
        Assert.True(_settings.SoundOn);

        await _wait.PassAsync(6);
        Assert.Equal(new[] { BreathCue.In }, _cues.Heard);
        await _wait.PassAsync(4);
        Assert.Equal(new[] { BreathCue.In, BreathCue.Out }, _cues.Heard);
        Assert.True(Create().SoundOn);
    }

    [Fact]
    public void Turning_the_sound_on_before_breathing_plays_one_tone_so_she_can_set_the_volume()
    {
        var vm = Create();

        vm.SoundOn = true;

        Assert.Equal(new[] { BreathCue.In }, _cues.Heard);
    }

    [Fact]
    public async Task A_tone_still_sounding_stops_when_she_turns_the_sound_off_pauses_or_leaves()
    {
        _settings.SoundOn = true;
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.NextAsync();
        Assert.Equal(new[] { BreathCue.In }, _cues.Heard);

        vm.SoundOn = false;
        Assert.Equal(1, _cues.Silenced);

        vm.ToggleCommand.Execute(null);
        Assert.Equal(2, _cues.Silenced);

        vm.Stop();
        Assert.Equal(3, _cues.Silenced);
    }

    [Fact]
    public async Task A_phone_that_cannot_vibrate_or_play_tones_offers_no_switches_and_stays_quiet()
    {
        _cues.CanVibrate = false;
        _cues.CanPlay = false;
        _settings.SoundOn = true;
        var vm = Create();

        Assert.False(vm.HasCueChoices);
        Assert.False(vm.CanVibrate);
        Assert.False(vm.CanPlaySound);
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(4);

        Assert.Empty(_cues.Felt);
        Assert.Empty(_cues.Heard);
    }

    [Fact]
    public void Each_tone_starts_and_ends_in_silence_and_stays_quiet()
    {
        var loudest = BreathTones.Peak * short.MaxValue;

        Assert.All(Enum.GetValues<BreathCue>(), cue =>
        {
            var samples = BreathTones.For(cue);

            Assert.Equal(0, samples[0]);
            Assert.Equal(0, samples[^1]);
            Assert.InRange(samples.Max(s => Math.Abs((int)s)), loudest / 4, loudest + 1);
            Assert.InRange(samples.Length, (BreathTones.Length(cue).TotalSeconds * BreathTones.SampleRate) - 1, BreathTones.Length(cue).TotalSeconds * BreathTones.SampleRate);
        });
    }

    [Fact]
    public void A_breaths_tone_fades_before_the_next_breath_begins()
    {
        Assert.True(BreathTones.Length(BreathCue.In) < Rhythm.Default.First);
        Assert.True(BreathTones.Length(BreathCue.Out) < Rhythm.Default.Second);
    }

    [Fact]
    public void Breathing_pauses_when_maia_goes_to_the_background_so_nothing_buzzes_or_sounds_in_her_pocket()
    {
        var page = Repo.CodeOf(Repo.App("Views", "BreathePage.xaml.cs"));

        // In the background it pauses, keeping the time left for when she comes back; leaving the screen ends it. Shell
        // shows the page before it is in the window, so the window is watched once it is there.
        Assert.Contains("_window.Stopped += OnStopped", page, StringComparison.Ordinal);
        Assert.Contains("if (propertyName == nameof(Window) && _showing)", page, StringComparison.Ordinal);
        Assert.Contains("OnStopped(object? sender, EventArgs e) => _viewModel.Pause()", page, StringComparison.Ordinal);
        Assert.Contains("_viewModel.Stop()", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Back_goes_back_to_your_week()
    {
        var navigator = new RecordingNavigator();
        var vm = new BreatheViewModel(_wait.WaitAsync, _clock, _screen, _screen, _cues, _cues, _settings, navigator);

        await vm.BackCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "back" }, navigator.Visited);
    }

    [Fact]
    public async Task The_screen_as_the_app_makes_it_waits_until_she_is_ready_and_back_goes_back()
    {
        var navigator = new RecordingNavigator();
        var vm = new BreatheViewModel(_screen, _screen, _cues, _cues, _settings, navigator);

        Assert.Equal(("Ready?", "Start", false), (vm.PhaseText, vm.ButtonText, vm.IsBreathing));
        await vm.BackCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "back" }, navigator.Visited);
    }

    [Fact]
    public async Task Back_without_a_way_back_does_nothing()
    {
        var vm = Create();

        await vm.BackCommand.ExecuteAsync(null);

        Assert.Equal("Ready?", vm.PhaseText);
    }

    [Fact]
    public async Task The_time_left_shows_while_she_breathes_and_not_before()
    {
        var vm = Create();
        Assert.False(vm.ShowsTimeLeft);

        vm.ToggleCommand.Execute(null);
        await _wait.NextAsync();

        Assert.True(vm.ShowsTimeLeft);
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    public void The_switches_show_for_what_the_phone_can_do(bool vibrates, bool plays, bool anyChoice, bool both)
    {
        _cues.CanVibrate = vibrates;
        _cues.CanPlay = plays;

        var vm = Create();

        Assert.Equal((anyChoice, both), (vm.HasCueChoices, vm.HasBothCueChoices));
    }

    [Fact]
    public void Setting_a_switch_to_what_it_already_is_neither_cues_nor_saves()
    {
        var vm = Create();
        _settings.VibrationOn = false;
        _settings.SoundOn = true;

        vm.VibrationOn = true;
        vm.SoundOn = false;

        Assert.Empty(_cues.Felt);
        Assert.Empty(_cues.Heard);
        Assert.Equal(0, _cues.Silenced);
        Assert.Equal((false, true), (_settings.VibrationOn, _settings.SoundOn));
    }

    [Fact]
    public async Task Switches_turned_on_for_a_phone_that_cannot_vibrate_or_play_tones_stay_quiet()
    {
        _cues.CanVibrate = false;
        _cues.CanPlay = false;
        var vm = Create();

        vm.VibrationOn = true;
        vm.SoundOn = true;
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(4);

        Assert.Empty(_cues.Felt);
        Assert.Empty(_cues.Heard);
        Assert.True(_settings.SoundOn);
    }

    [Fact]
    public async Task Vibration_turned_on_while_she_breathes_waits_for_the_next_breath()
    {
        _settings.VibrationOn = false;
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.PassAsync(1);

        vm.VibrationOn = true;
        Assert.Empty(_cues.Felt);

        await _wait.PassAsync(3);
        Assert.Equal(new[] { BreathCue.Out }, _cues.Felt);
    }

    [Fact]
    public async Task A_second_that_ends_more_than_a_second_late_starts_the_next_at_once()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);

        Assert.Equal(TimeSpan.FromSeconds(1), await _wait.NextAsync());
        _clock.Advance(TimeSpan.FromMilliseconds(1500));
        _wait.Release();

        Assert.Equal(TimeSpan.Zero, await _wait.NextAsync());
        Assert.Equal("1:59", vm.TimeLeft);
    }

    [Theory]
    [InlineData("BreathePage.xaml")]
    [InlineData("QuestionsPage.xaml")]
    [InlineData("TasksPage.xaml")]
    public void Pages_opened_from_your_week_have_a_back_pill_and_no_bottom_bar(string file)
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", file));

        Assert.Null(AccessibilityTests.Attr(page.Root!, "Tab"));
        var back = Assert.Single(page.Descendants(), e => e.Name.LocalName == "BackPill");
        Assert.Equal("{Binding BackCommand}", AccessibilityTests.Attr(back, "Command"));
    }

    [Fact]
    public void The_time_left_shows_while_paused_and_start_over_only_then()
    {
        var page = File.ReadAllText(Repo.App("Views", "BreathePage.xaml"));

        Assert.Contains("Text=\"{Binding TimeLeft}\" SemanticProperties.Description=\"{Binding TimeLeftSpoken}\" IsVisible=\"{Binding ShowsTimeLeft}\"", page, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding StartOverCommand}\"", page, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsPaused}\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_breathe_tile_on_your_week_shows_a_dandelion_seed_drifting_with_the_breath()
    {
        var week = File.ReadAllText(Repo.App("Views", "WeekPage.xaml"));
        var drawings = Repo.CodeOf(Repo.App("Controls", "AnimatedDrawings.cs"));

        Assert.Contains("<controls:DriftingSeed />", week, StringComparison.Ordinal);
        Assert.DoesNotContain("BreathingDot", week, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"IconDandelionSeed\"", File.ReadAllText(Repo.App("Resources", "Styles", "Icons.xaml")), StringComparison.Ordinal);
        Assert.Contains("AmbientLoop.Attach(this, Tick)", drawings, StringComparison.Ordinal);
    }

    [Fact]
    public void On_android_both_cues_keep_quiet_on_silent_or_in_do_not_disturb_but_the_tone_still_plays_on_vibrate()
    {
        var cues = Repo.CodeOf(Repo.App("Platforms", "Android", "BreathCues.cs"));
        var manifest = File.ReadAllText(Repo.App("Platforms", "Android", "AndroidManifest.xml"));

        Assert.Contains("SetUsage(AudioUsageKind.Media)", cues, StringComparison.Ordinal);
        Assert.Contains("public static bool Now => DoNotDisturb || Ringer == RingerMode.Silent;", cues, StringComparison.Ordinal);
        Assert.Equal(2, cues.Split("|| PhoneQuiet.Now").Length - 1);

        // Vibrate only swaps the ringer for a buzz; the media volume, where the tone plays, is still on.
        Assert.DoesNotContain("RingerMode.Vibrate", cues, StringComparison.Ordinal);
        Assert.DoesNotContain("Ringer !=", cues, StringComparison.Ordinal);
        Assert.Contains("android.permission.VIBRATE", manifest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "silent", "Do not disturb", "vibration settings", "vibrate")]
    [InlineData("el-GR", "σίγαση", "«Μην ενοχλείτε»", "ρυθμίσεις δόνησης", "δόνηση")]
    public void Each_switch_says_when_the_phone_keeps_it_quiet_so_a_still_phone_never_looks_broken(
        string culture, string silent, string doNotDisturb, string vibrationSettings, string vibrate)
    {
        using var language = new CultureScope(culture);
        var vibration = Text.Get("Timer_VibrationHint");
        var sound = Text.Get("Timer_SoundHint");

        Assert.All([vibration, sound], hint =>
        {
            Assert.Contains(silent, hint, StringComparison.Ordinal);
            Assert.Contains(doNotDisturb, hint, StringComparison.Ordinal);
        });
        Assert.Contains(vibrationSettings, vibration, StringComparison.Ordinal);
        Assert.DoesNotContain(vibrate, sound, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void One_timer_names_neither_breathing_nor_an_exercise_in_its_parts()
    {
        var vm = Create();

        Assert.Equal(Rhythm.Default, vm.Rhythm);
        Assert.Equal("4 seconds, then 6, 12 times. For calm breathing, or the exercises your doctor suggested.", vm.Intro);
        Assert.Equal("Feeling dizzy, in pain or unwell? Stop and rest.", Text.Get("Timer_Caution"));
        Assert.Equal(("Ready?", "Start", "2:00"), (vm.PhaseText, vm.ButtonText, vm.TimeLeft));
        Assert.Equal(("4 seconds", "6 seconds", "12"), (vm.FirstValue, vm.SecondValue, vm.RoundsValue));
        Assert.Equal(("First, 4 seconds", "Then, 6 seconds", "Rounds, 12"), (vm.FirstSpoken, vm.SecondSpoken, vm.RoundsSpoken));
    }

    [Fact]
    public void In_greek_the_timer_reads_first_and_then()
    {
        using var greek = new CultureScope("el-GR");
        var vm = Create();

        Assert.Equal("4 δευτερόλεπτα, μετά 6, 12 φορές. Για ήρεμη αναπνοή ή για τις ασκήσεις που σου πρότεινε ο γιατρός σου.", vm.Intro);
        Assert.Equal(("Πρώτα, 4 δευτερόλεπτα", "Μετά, 6 δευτερόλεπτα", "Ξεκίνα"), (vm.FirstSpoken, vm.SecondSpoken, vm.ButtonText));
    }

    [Fact]
    public async Task The_sphere_counts_down_the_seconds_left_of_each_part()
    {
        var vm = Create();

        vm.ToggleCommand.Execute(null);
        Assert.Equal(("First", "4"), (vm.PhaseText, vm.PartLeft));
        await _wait.PassAsync(3);
        Assert.Equal(("First", "1"), (vm.PhaseText, vm.PartLeft));
        await _wait.PassAsync(1);
        Assert.Equal(("Then", "6"), (vm.PhaseText, vm.PartLeft));
        await _wait.PassAsync(5);
        Assert.Equal("1", vm.PartLeft);

        // Each part is announced once as it begins, never every second.
        Assert.Equal(new[] { "First", "Then" }, _screen.Announced);
    }

    [Fact]
    public async Task The_timer_ends_with_well_done_and_start_again()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);

        await BreatheToTheEndAsync();

        Assert.Equal(("Well done", "Start again"), (vm.PhaseText, vm.ButtonText));
    }

    [Fact]
    public void The_times_she_set_are_kept_on_the_phone()
    {
        var vm = Create();
        vm.LongerFirstCommand.Execute(null);
        vm.FewerRoundsCommand.Execute(null);

        var again = Create();

        Assert.Equal(new Rhythm(5, 6, 11), again.Rhythm);
        Assert.Equal("5 seconds, then 6, 11 times. For calm breathing, or the exercises your doctor suggested.", again.Intro);
    }

    [Fact]
    public void Each_time_moves_a_second_or_a_round_at_a_time_and_the_session_length_follows()
    {
        var vm = Create();

        vm.ShorterFirstCommand.Execute(null);
        vm.LongerSecondCommand.Execute(null);
        vm.LongerSecondCommand.Execute(null);
        vm.MoreRoundsCommand.Execute(null);
        vm.FewerRoundsCommand.Execute(null);
        vm.FewerRoundsCommand.Execute(null);

        Assert.Equal(new Rhythm(3, 8, 11), vm.Rhythm);
        Assert.Equal(TimeSpan.FromSeconds(121), vm.Length);
        Assert.Equal(("3 seconds", "8 seconds", "11", "2:01"), (vm.FirstValue, vm.SecondValue, vm.RoundsValue, vm.TimeLeft));

        vm.ShorterSecondCommand.Execute(null);
        Assert.Equal(7, vm.Rhythm.SecondSeconds);
    }

    [Fact]
    public void A_time_stops_at_its_limits()
    {
        _settings.Rhythm = new Rhythm(Rhythm.MinSeconds, Rhythm.MaxSeconds, Rhythm.MinRounds);
        var vm = Create();

        Assert.False(vm.ShorterFirstCommand.CanExecute(null));
        Assert.False(vm.LongerSecondCommand.CanExecute(null));
        Assert.False(vm.FewerRoundsCommand.CanExecute(null));
        Assert.True(vm.LongerFirstCommand.CanExecute(null));
        Assert.True(vm.ShorterSecondCommand.CanExecute(null));
        Assert.True(vm.MoreRoundsCommand.CanExecute(null));
        Assert.Equal("1 second, then 30, once. For calm breathing, or the exercises your doctor suggested.", vm.Intro);
        Assert.Equal("1 second", vm.FirstValue);

        _settings.Rhythm = new Rhythm(Rhythm.MaxSeconds, Rhythm.MinSeconds, Rhythm.MaxRounds);
        vm = Create();

        Assert.False(vm.LongerFirstCommand.CanExecute(null));
        Assert.False(vm.ShorterSecondCommand.CanExecute(null));
        Assert.False(vm.MoreRoundsCommand.CanExecute(null));
    }

    [Fact]
    public void Times_read_back_beyond_the_limits_are_kept_within_them()
    {
        _settings.Rhythm = new Rhythm(0, 99, -3);

        Assert.Equal(new Rhythm(Rhythm.MinSeconds, Rhythm.MaxSeconds, Rhythm.MinRounds), Create().Rhythm);
    }

    [Fact]
    public async Task Her_times_wait_while_it_runs_and_a_change_while_paused_starts_afresh()
    {
        var vm = Create();
        vm.ToggleCommand.Execute(null);
        await _wait.NextAsync();

        Assert.False(vm.LongerFirstCommand.CanExecute(null));
        Assert.False(vm.ShorterFirstCommand.CanExecute(null));
        Assert.False(vm.LongerSecondCommand.CanExecute(null));
        Assert.False(vm.ShorterSecondCommand.CanExecute(null));
        Assert.False(vm.MoreRoundsCommand.CanExecute(null));
        Assert.False(vm.FewerRoundsCommand.CanExecute(null));

        vm.ToggleCommand.Execute(null);
        Assert.True(vm.IsPaused);
        Assert.True(vm.LongerFirstCommand.CanExecute(null));

        vm.LongerFirstCommand.Execute(null);

        Assert.False(vm.IsPaused);
        Assert.Equal(("Ready?", "Start", "2:12"), (vm.PhaseText, vm.ButtonText, vm.TimeLeft));
    }

    [Fact]
    public async Task A_rhythm_she_set_is_timed_part_by_part()
    {
        _settings.Rhythm = new Rhythm(2, 3, 2);
        var vm = Create();

        vm.ToggleCommand.Execute(null);
        Assert.Equal(("First", TimeSpan.FromSeconds(2), "0:10"), (vm.PhaseText, vm.PhaseLeft, vm.TimeLeft));
        await _wait.PassAsync(2);
        Assert.Equal(("Then", TimeSpan.FromSeconds(3)), (vm.PhaseText, vm.PhaseLeft));
        await _wait.PassAsync(3);
        Assert.Equal(("First", "0:05"), (vm.PhaseText, vm.TimeLeft));
        await _wait.PassAsync(4);
        await _wait.NextAsync();
        _wait.Release();
        await _screen.SleepsAsync();

        Assert.Equal("Well done", vm.PhaseText);
        Assert.Equal(new[] { "First", "Then", "First", "Then", "Well done" }, _screen.Announced);
    }

    [Fact]
    public void The_default_is_two_calm_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), Rhythm.Default.Length);
    }

    [Fact]
    public void The_timer_has_no_modes_to_choose_between()
    {
        var page = File.ReadAllText(Repo.App("Views", "BreathePage.xaml"));

        Assert.DoesNotContain("ChoicePill", page, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding PartLeft}\" IsVisible=\"{Binding IsBreathing}\"", page, StringComparison.Ordinal);
    }

    /// <summary>Lets the whole two minutes pass, and returns once the screen may sleep again.</summary>
    private async Task BreatheToTheEndAsync()
    {
        await _wait.PassAsync((int)Rhythm.Default.Length.TotalSeconds - 1);
        await _wait.NextAsync();
        _wait.Release();
        await _screen.SleepsAsync();
    }

    /// <summary>A wait the test finishes by hand, reporting each duration asked for; finishing it moves the clock on.</summary>
    private sealed class ControlledWait(ManualTimeProvider clock)
    {
        private readonly Channel<TimeSpan> _asked = Channel.CreateUnbounded<TimeSpan>();
        private TaskCompletionSource? _current;
        private TimeSpan _duration;

        /// <summary>Whether the rhythm asked for another wait that the test has not read.</summary>
        public bool HasPending => _asked.Reader.Count > 0;

        public Task WaitAsync(TimeSpan duration)
        {
            var wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _current = wait;
            _duration = duration;
            _asked.Writer.TryWrite(duration);
            return wait.Task;
        }

        public async Task<TimeSpan> NextAsync() =>
            await _asked.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        /// <summary>Finishes the wait asked for last, as the time it asked for passes.</summary>
        public void Release()
        {
            clock.Advance(_duration);
            _current?.TrySetResult();
        }

        /// <summary>
        /// Lets <paramref name="seconds"/> waits pass, then returns once the rhythm has moved on and asks for the next
        /// one. Returns how long the passed waits were.
        /// </summary>
        public async Task<TimeSpan> PassAsync(int seconds)
        {
            var waited = TimeSpan.Zero;
            for (var i = 0; i < seconds; i++)
            {
                waited += await NextAsync();
                Release();
            }

            await _asked.Reader.WaitToReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            return waited;
        }
    }

    /// <summary>The phone's screen and its screen reader: whether the screen is kept on, and what was said.</summary>
    private sealed class FakeScreen : IScreenAwake, IAnnouncer
    {
        private readonly TaskCompletionSource _sleptAfterBreathing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _wasOn;

        public bool KeptOn { get; private set; }

        public List<string> Announced { get; } = [];

        public void KeepOn(bool keepOn)
        {
            KeptOn = keepOn;
            _wasOn |= keepOn;
            if (!keepOn && _wasOn)
            {
                _sleptAfterBreathing.TrySetResult();
            }
        }

        public void Announce(string text) => Announced.Add(text);

        /// <summary>Completes once the screen may sleep again after she breathed.</summary>
        public Task SleepsAsync() => _sleptAfterBreathing.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>The phone's vibration and its speaker: which cues were felt and heard, and how often a tone was silenced.</summary>
    private sealed class FakeCues : IBreathVibration, IBreathSound
    {
        public bool CanVibrate { get; set; } = true;

        public bool CanPlay { get; set; } = true;

        public List<BreathCue> Felt { get; } = [];

        public List<BreathCue> Heard { get; } = [];

        public int Silenced { get; private set; }

        bool IBreathVibration.IsSupported => CanVibrate;

        bool IBreathSound.IsSupported => CanPlay;

        void IBreathVibration.Cue(BreathCue cue) => Felt.Add(cue);

        void IBreathSound.Cue(BreathCue cue) => Heard.Add(cue);

        public void Silence() => Silenced++;
    }

    /// <summary>Her Breathe choices as the app keeps them: vibration on and sound off until she changes them.</summary>
    private sealed class InMemoryBreatheSettings : IBreatheSettings
    {
        public bool VibrationOn { get; set; } = true;

        public bool SoundOn { get; set; }

        public Rhythm Rhythm { get; set; } = Rhythm.Default;
    }
}
