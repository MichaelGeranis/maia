using Maia.Application.Abstractions;
using Maia.Domain.Plans;
using Maia.Presentation.Settings;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>The Reminders card in Settings: the switch, "Remind me" and a discreet lock screen (review #22).</summary>
public sealed class RemindersTests : IDisposable
{
    private static readonly Appointment GlucoseSlot = new(new DateOnly(2026, 10, 12), new TimeOnly(8, 30));

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private async Task<PhoneRemindersSetting> OpenAsync()
    {
        var setting = _app.CreateRemindersSetting();
        await setting.LoadAsync();
        return setting;
    }

    private async Task SettledAsync(PhoneRemindersSetting setting)
    {
        await setting.WhenSavedAsync();
        await _app.Reminders.WhenIdleAsync();
    }

    [Fact]
    public async Task Turning_reminders_on_asks_for_notifications_then_sets_them()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        var setting = await OpenAsync();
        Assert.True(setting.IsSupported);
        Assert.False(setting.IsOn);
        Assert.Equal(0, _app.NotificationAccess.Requests);

        setting.IsOn = true;
        await SettledAsync(setting);

        Assert.Equal(1, _app.NotificationAccess.Requests);
        Assert.True(_app.ReminderSettings.IsOn);
        Assert.Null(setting.Notice);
        Assert.Equal("Glucose test", Assert.Single(_app.ReminderScheduler.Set).Title);
    }

    [Fact]
    public async Task If_she_says_no_to_notifications_the_switch_goes_back_and_says_where_to_allow_them()
    {
        await _app.CreatePlanAsync();
        _app.NotificationAccess.WillGrant = false;
        var setting = await OpenAsync();

        setting.IsOn = true;
        await SettledAsync(setting);

        Assert.False(setting.IsOn);
        Assert.False(_app.ReminderSettings.IsOn);
        Assert.Equal("Maia can't show notifications yet. You can allow them for Maia in your phone's settings.", setting.Notice);
        setting.OpenPhoneSettingsCommand.Execute(null);
        Assert.Equal(1, _app.NotificationAccess.SettingsOpened);
    }

    [Fact]
    public async Task Turning_them_off_cancels_every_reminder_without_asking()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        var setting = await OpenAsync();
        setting.IsOn = true;
        await SettledAsync(setting);

        setting.IsOn = false;
        await SettledAsync(setting);

        Assert.False(_app.ReminderSettings.IsOn);
        Assert.Empty(_app.ReminderScheduler.Set);
        Assert.Equal(1, _app.NotificationAccess.Requests);
    }

    [Fact]
    public async Task Reminders_left_on_with_notifications_turned_off_since_say_so()
    {
        await _app.CreatePlanAsync();
        _app.ReminderSettings.IsOn = true;
        _app.NotificationAccess.Granted = false;

        var setting = await OpenAsync();

        Assert.True(setting.IsOn);
        Assert.Equal("Reminders are on, but notifications for Maia are off in your phone's settings.", setting.Notice);
        Assert.Equal(0, _app.NotificationAccess.Requests);
    }

    [Fact]
    public async Task Where_the_phone_cannot_show_them_the_switch_is_not_offered()
    {
        _app.NotificationAccess.IsSupported = false;
        _app.ReminderSettings.IsOn = true;

        var setting = await OpenAsync();

        Assert.False(setting.IsSupported);
        Assert.False(setting.IsOn);
    }

    [Fact]
    public async Task Settings_shows_the_switch_as_she_left_it()
    {
        await _app.CreatePlanAsync();
        _app.ReminderSettings.IsOn = true;
        _app.NotificationAccess.Granted = true;
        var vm = _app.CreateSettings();

        await vm.LoadAsync();

        Assert.True(vm.Reminders.IsOn);
        Assert.Null(vm.Reminders.Notice);
    }

    [Fact]
    public async Task Remind_me_shows_her_choice_and_a_new_choice_sets_the_reminders_again()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        _app.ReminderSettings.IsOn = true;
        _app.NotificationAccess.Granted = true;
        var setting = await OpenAsync();
        await _app.Reminders.WhenIdleAsync();
        Assert.True(setting.RemindDayBefore);

        setting.SetRemindersCommand.Execute("Both");
        await SettledAsync(setting);

        Assert.Equal((false, false, true), (setting.RemindDayBefore, setting.RemindTwoHours, setting.RemindBoth));
        Assert.Equal(ReminderPreference.Both, _app.ReminderSettings.Settings.When);
        Assert.Equal(new[] { "check:ogtt:daybefore", "check:ogtt:soon" }, _app.ReminderScheduler.Set.Select(r => r.Key));
    }

    [Fact]
    public async Task Discreet_lock_screen_takes_every_name_off_her_notifications()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, GlucoseSlot);
        _app.ReminderSettings.IsOn = true;
        var setting = await OpenAsync();
        Assert.False(setting.DiscreetTitles);

        setting.DiscreetTitles = true;
        await SettledAsync(setting);

        Assert.True(_app.ReminderSettings.Settings.DiscreetTitles);
        Assert.Equal("Appointment", Assert.Single(_app.ReminderScheduler.Set).Title);
    }

    [Fact]
    public async Task Showing_her_choices_saves_nothing_and_an_unknown_choice_is_ignored()
    {
        _app.ReminderSettings.Settings = new ReminderSettings { When = ReminderPreference.TwoHoursBefore, DiscreetTitles = true };
        var setting = await OpenAsync();
        _app.ReminderSettings.Settings = ReminderSettings.Default;

        setting.SetRemindersCommand.Execute("Sometimes");
        setting.SetRemindersCommand.Execute("TwoHoursBefore");
        await setting.WhenSavedAsync();

        Assert.True(setting.RemindTwoHours);
        Assert.True(setting.DiscreetTitles);
        Assert.Equal(ReminderSettings.Default, _app.ReminderSettings.Settings);
    }

    [Fact]
    public async Task A_choice_that_cannot_be_saved_is_said_calmly_and_reported_by_code()
    {
        var setting = await OpenAsync();
        _app.ReminderSettings.FailNextSave = new IOException("preferences unavailable");

        setting.SetRemindersCommand.Execute("Both");
        await setting.WhenSavedAsync();

        Assert.Equal("Something went wrong. Please try again.", setting.Notice);
        Assert.Equal("settings.reminder_choices", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task The_choices_she_made_with_calendar_sync_are_kept_until_she_chooses_again()
    {
        var values = new MemoryPreferences();
        values.Ints[ReminderPreferences.OldWhenKey] = (int)ReminderPreference.Both;
        values.Bools[ReminderPreferences.OldDiscreetKey] = true;
        var store = new ReminderPreferences(values);

        Assert.Equal(new ReminderSettings { When = ReminderPreference.Both, DiscreetTitles = true }, await store.GetAsync(CancellationToken.None));

        await store.SaveAsync(new ReminderSettings { When = ReminderPreference.TwoHoursBefore }, CancellationToken.None);

        Assert.Equal(new ReminderSettings { When = ReminderPreference.TwoHoursBefore }, await store.GetAsync(CancellationToken.None));
        Assert.Equal((int)ReminderPreference.TwoHoursBefore, values.Ints[ReminderPreferences.WhenKey]);
        Assert.False(values.Bools[ReminderPreferences.DiscreetKey]);
    }

    [Fact]
    public async Task With_nothing_stored_reminders_are_off_at_24_hours_before_and_a_stored_value_maia_does_not_know_reads_as_that()
    {
        var values = new MemoryPreferences();
        var store = new ReminderPreferences(values);

        Assert.False(await store.IsOnAsync(CancellationToken.None));
        Assert.Equal(ReminderSettings.Default, await store.GetAsync(CancellationToken.None));

        values.Ints[ReminderPreferences.WhenKey] = 7;
        Assert.Equal(ReminderPreference.DayBefore, (await store.GetAsync(CancellationToken.None)).When);

        await store.SetOnAsync(true, CancellationToken.None);
        Assert.True(values.Bools[ReminderPreferences.OnKey]);
    }

    /// <summary>The phone's preferences, in memory.</summary>
    private sealed class MemoryPreferences : IPreferenceValues
    {
        public Dictionary<string, int> Ints { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, bool> Bools { get; } = new(StringComparer.Ordinal);

        public bool Has(string key) => Ints.ContainsKey(key) || Bools.ContainsKey(key);

        public int ReadInt(string key, int fallback) => Ints.GetValueOrDefault(key, fallback);

        public bool ReadBool(string key, bool fallback) => Bools.GetValueOrDefault(key, fallback);

        public void WriteInt(string key, int value) => Ints[key] = value;

        public void WriteBool(string key, bool value) => Bools[key] = value;
    }
}
