using Maia.Presentation.Privacy;
using Maia.Presentation.Settings;

namespace Maia.Presentation.Tests;

public sealed class AppLockTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private AppLock Lock => _app.AppLock;

    private AppLockSettings Card() => new(Lock, _app.Notifier, _app.Errors);

    [Fact]
    public void The_lock_is_off_until_she_turns_it_on()
    {
        Lock.Start();

        Assert.False(Lock.IsOn);
        Assert.False(Lock.IsLocked);
        Assert.False(Card().IsOn);
    }

    [Fact]
    public async Task Turning_it_on_asks_for_her_screen_lock_first()
    {
        var card = Card();

        await card.ChangeAsync(true);

        Assert.True(_app.LockPreference.IsOn);
        Assert.True(card.IsOn);
        Assert.Equal(new[] { "Turning on the lock for Maia" }, _app.ScreenLock.Prompts);
        Assert.Equal("Maia now locks with your screen lock.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task If_she_cancels_her_screen_lock_the_switch_stays_off()
    {
        _app.ScreenLock.WillConfirm = false;
        var card = Card();
        var changes = new List<string?>();
        card.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        await card.ChangeAsync(true);

        Assert.False(_app.LockPreference.IsOn);
        Assert.False(card.IsOn);
        Assert.Contains(nameof(AppLockSettings.IsOn), changes);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task A_phone_without_a_screen_lock_is_told_calmly_what_it_needs()
    {
        _app.ScreenLock.IsSet = false;
        var card = Card();

        await card.ChangeAsync(true);

        Assert.False(card.IsOn);
        Assert.Empty(_app.ScreenLock.Prompts);
        Assert.Equal(
            "Your phone has no screen lock yet. Set a PIN, pattern or password in your phone's settings, then turn this on.",
            card.Notice);

        card.OpenPhoneSettingsCommand.Execute(null);
        Assert.Equal(1, _app.ScreenLock.SettingsOpened);

        _app.ScreenLock.IsSet = true;
        card.Refresh();
        Assert.Null(card.Notice);
    }

    [Fact]
    public async Task Turning_it_off_needs_no_prompt()
    {
        _app.LockPreference.IsOn = true;
        var card = Card();

        await card.ChangeAsync(false);

        Assert.False(_app.LockPreference.IsOn);
        Assert.Empty(_app.ScreenLock.Prompts);
        Assert.Equal("Maia no longer locks.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public void With_the_lock_on_maia_starts_locked()
    {
        _app.LockPreference.IsOn = true;

        Lock.Start();

        Assert.True(Lock.IsLocked);
    }

    [Fact]
    public void Before_it_starts_maia_knows_whether_it_will_start_locked()
    {
        Assert.False(Lock.LocksOnStart);

        _app.LockPreference.IsOn = true;
        Assert.True(Lock.LocksOnStart);

        _app.ScreenLock.IsSet = false;
        Assert.False(Lock.LocksOnStart);
    }

    [Fact]
    public async Task A_minute_or_less_away_opens_without_asking()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();
        await Lock.UnlockAsync();

        Lock.Leave();
        _app.Time.Advance(AppLock.GracePeriod);
        Lock.Return();

        Assert.False(Lock.IsLocked);
        Assert.Single(_app.ScreenLock.Prompts);
    }

    [Fact]
    public async Task More_than_a_minute_away_locks_until_her_screen_lock_is_confirmed()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();
        await Lock.UnlockAsync();
        Assert.False(Lock.IsLocked);

        Lock.Leave();
        _app.Time.Advance(AppLock.GracePeriod + TimeSpan.FromSeconds(1));
        Lock.Return();

        Assert.True(Lock.IsLocked);
        Assert.True(await Lock.UnlockAsync());
        Assert.False(Lock.IsLocked);
        Assert.Equal(new[] { "Unlocking Maia", "Unlocking Maia" }, _app.ScreenLock.Prompts);
    }

    [Fact]
    public async Task While_she_is_away_with_the_lock_on_maia_hides_its_pages()
    {
        Lock.Start();
        Assert.False(Lock.Leave());
        Lock.Return();

        _app.LockPreference.IsOn = true;
        Lock.Start();
        Assert.False(Lock.Leave());
        await Lock.UnlockAsync();

        Assert.True(Lock.Leave());
    }

    [Fact]
    public async Task Start_up_waits_until_she_unlocks()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();

        var waiting = Lock.WhenUnlockedAsync();
        Assert.False(waiting.IsCompleted);

        await Lock.UnlockAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(Lock.WhenUnlockedAsync().IsCompleted);
    }

    [Fact]
    public void Away_with_the_lock_off_never_locks()
    {
        Lock.Start();
        Lock.Leave();
        _app.Time.Advance(TimeSpan.FromHours(3));
        Lock.Return();

        Assert.False(Lock.IsLocked);
    }

    [Fact]
    public async Task Her_own_trip_to_the_screen_lock_prompt_does_not_count_as_away()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();
        var slow = new SlowStep();
        _app.ScreenLock.Slow = slow;

        var unlocking = Lock.UnlockAsync();
        await slow.Started;
        Lock.Leave();
        _app.Time.Advance(TimeSpan.FromMinutes(5));
        Lock.Return();
        slow.Finish();

        Assert.True(await unlocking);
        Assert.False(Lock.IsLocked);
    }

    [Fact]
    public void A_phone_whose_screen_lock_was_removed_does_not_lock_maia_for_good()
    {
        _app.LockPreference.IsOn = true;
        _app.ScreenLock.IsSet = false;

        Lock.Start();

        Assert.False(Lock.IsLocked);
    }

    [Fact]
    public async Task The_lock_screen_asks_again_calmly_after_a_cancel()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();
        _app.ScreenLock.WillConfirm = false;
        var screen = new LockViewModel(Lock);

        await screen.UnlockCommand.ExecuteAsync(null);

        Assert.True(Lock.IsLocked);
        Assert.Equal("Maia stays locked. Tap Unlock when you're ready.", screen.Notice);

        _app.ScreenLock.WillConfirm = true;
        await screen.UnlockCommand.ExecuteAsync(null);

        Assert.False(Lock.IsLocked);
        Assert.Null(screen.Notice);
    }

    [Fact]
    public async Task A_prompt_that_fails_is_reported_by_code_and_the_switch_stays_off()
    {
        var card = new AppLockSettings(new AppLock(_app.LockPreference, new BrokenScreenLock(), _app.Time), _app.Notifier, _app.Errors);

        await card.ChangeAsync(true);

        Assert.False(card.IsOn);
        Assert.Equal("settings.lock", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Equal("Something went wrong. Please try again.", _app.Notifier.Shown.Single());
    }

    private sealed class BrokenScreenLock : IScreenLock
    {
        public bool IsSet => true;

        public Task<bool> ConfirmAsync(string title, string subtitle) => throw new InvalidOperationException("no activity");

        public void OpenSettings()
        {
        }
    }
}
