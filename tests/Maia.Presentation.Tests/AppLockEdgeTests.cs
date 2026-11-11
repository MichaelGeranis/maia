using Maia.Presentation.Privacy;
using Maia.Presentation.Settings;

namespace Maia.Presentation.Tests;

/// <summary>"Lock Maia" when there is nothing to do: already open, already on or off, or a second tap while she confirms.</summary>
public sealed class AppLockEdgeTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private AppLock Lock => _app.AppLock;

    [Fact]
    public async Task Unlocking_maia_when_it_is_open_asks_for_nothing()
    {
        Lock.Start();

        Assert.True(await Lock.UnlockAsync());

        Assert.Empty(_app.ScreenLock.Prompts);
    }

    [Fact]
    public async Task Locked_maia_on_a_phone_whose_screen_lock_was_removed_opens_without_asking()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();
        Assert.True(Lock.IsLocked);
        _app.ScreenLock.IsSet = false;

        Assert.True(await Lock.UnlockAsync());

        Assert.False(Lock.IsLocked);
        Assert.Empty(_app.ScreenLock.Prompts);
    }

    [Fact]
    public async Task Everyone_waiting_for_maia_to_open_waits_for_the_same_unlock()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();

        var first = Lock.WhenUnlockedAsync();
        var second = Lock.WhenUnlockedAsync();

        Assert.Same(first, second);
        Assert.False(first.IsCompleted);
        await Lock.UnlockAsync();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Coming_back_without_having_left_does_not_lock()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();
        await Lock.UnlockAsync();
        _app.Time.Advance(TimeSpan.FromMinutes(5));

        Lock.Return();

        Assert.False(Lock.IsLocked);
    }

    [Fact]
    public async Task Turning_on_a_lock_already_on_asks_for_nothing_and_off_one_already_off_changes_nothing()
    {
        _app.LockPreference.IsOn = true;
        Assert.True(await Lock.TurnOnAsync());
        Assert.Empty(_app.ScreenLock.Prompts);

        _app.LockPreference.IsOn = false;
        var changes = new List<string?>();
        Lock.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Lock.TurnOff();

        Assert.Empty(changes);
        Assert.False(Lock.IsOn);
    }

    [Fact]
    public async Task The_switch_turned_on_asks_once_and_a_second_tap_while_she_confirms_is_ignored()
    {
        var card = new AppLockSettings(Lock, _app.Notifier, _app.Errors);
        var prompt = new SlowStep();
        _app.ScreenLock.Slow = prompt;

        card.IsOn = false;
        Assert.Empty(_app.ScreenLock.Prompts);

        card.IsOn = true;
        await prompt.Started.WaitAsync(TimeSpan.FromSeconds(5));
        card.IsOn = true;
        prompt.Finish();
        await WaitUntilAsync(() => _app.Notifier.Shown.Count > 0);

        Assert.Single(_app.ScreenLock.Prompts);
        Assert.True(card.IsOn);
        Assert.Equal("Maia now locks with your screen lock.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Unlock_can_be_tapped_until_the_screen_lock_prompt_is_up()
    {
        _app.LockPreference.IsOn = true;
        Lock.Start();
        var screen = new LockViewModel(Lock);
        var prompt = new SlowStep();
        _app.ScreenLock.Slow = prompt;
        Assert.True(screen.UnlockCommand.CanExecute(null));

        var unlocking = screen.UnlockCommand.ExecuteAsync(null);
        await prompt.Started.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(screen.UnlockCommand.CanExecute(null));

        prompt.Finish();
        await unlocking;
        Assert.False(Lock.IsLocked);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
