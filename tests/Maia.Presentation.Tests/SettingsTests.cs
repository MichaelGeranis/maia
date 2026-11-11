using Maia.Domain.Plans;
using Maia.Presentation.Localization;
using Maia.Presentation.Settings;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();
    private readonly RecordingLanguageSwitcher _languages = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private SettingsViewModel Create() => _app.CreateSettings(_languages);

    [Fact]
    public async Task Choosing_greek_changes_the_plan_and_redraws_the_app()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        Assert.True(vm.IsEnglish);
        await vm.ChooseLanguageCommand.ExecuteAsync("el");

        Assert.Equal("el", _app.Repository.Stored!.Language);
        Assert.Equal(new[] { "el" }, _languages.Switched);
    }

    [Fact]
    public async Task Choosing_the_language_already_shown_does_nothing()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        await vm.ChooseLanguageCommand.ExecuteAsync("en");

        Assert.Empty(_languages.Switched);
    }

    [Fact]
    public async Task If_the_plan_cannot_be_saved_the_language_stays()
    {
        await _app.CreatePlanAsync();
        _app.Repository.FailNextSave = new IOException("disk full");
        var vm = Create();

        await vm.ChooseLanguageCommand.ExecuteAsync("el");

        Assert.Empty(_languages.Switched);
        Assert.Equal("Something went wrong. Please try again.", _app.Notifier.Shown.Single());
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "settings.language");
        Assert.Equal("en", _app.Repository.Stored!.Language);
    }

    [Fact]
    public async Task Delete_asks_first_and_can_be_called_off()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        vm.AskDeleteCommand.Execute(null);
        Assert.True(vm.IsConfirmingDelete);

        vm.KeepDataCommand.Execute(null);

        Assert.False(vm.IsConfirmingDelete);
        Assert.NotNull(_app.Repository.Stored);
    }

    [Fact]
    public async Task Deleting_everything_clears_the_phone_and_starts_over()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.BookCheckAsync(TestData.Glucose, new Appointment(new DateOnly(2026, 10, 12), new TimeOnly(8, 30)));
        _app.Setup.DueDate = TestData.DueDate;
        var vm = Create();
        vm.AskDeleteCommand.Execute(null);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Null(_app.Repository.Stored);
        Assert.Equal(1, _app.Viewer.Cleared);
        Assert.Null(_app.Setup.DueDate);
        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.Equal("Everything is deleted from this phone.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task Deleting_everything_says_so_on_setup_not_on_settings_as_it_closes()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        vm.AskDeleteCommand.Execute(null);
        var toastsOnTheWay = -1;
        _app.Navigator.WhenVisiting = _ => toastsOnTheWay = _app.Notifier.Shown.Count;

        await vm.DeleteCommand.ExecuteAsync(null);

        // Raised while Settings was still on screen, it was taken there and lost as Settings closed.
        Assert.Equal(0, toastsOnTheWay);
        Assert.Equal("Everything is deleted from this phone.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public void A_backup_needs_a_long_enough_password_typed_twice()
    {
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);

        Assert.True(vm.IsBackupFormOpen);
        Assert.Equal("At least 10 characters. A few words you'll remember work well.", vm.BackupHint);
        Assert.Null(vm.BackupStrength);

        vm.BackupPassword = "sea-breeze-17";
        vm.BackupPasswordAgain = "sea-breeze-1";
        Assert.Equal("The two passwords don't match yet.", vm.BackupHint);
        Assert.False(vm.CreateBackupCommand.CanExecute(null));

        vm.BackupPasswordAgain = "sea-breeze-17";
        Assert.StartsWith("Keep this password safe", vm.BackupHint, StringComparison.Ordinal);
        Assert.Equal("Good password.", vm.BackupStrength);
        Assert.False(vm.IsBackupHintAttention);
        Assert.True(vm.CreateBackupCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("17/01/2027", "Numbers alone, like dates or phone numbers, are easy to guess. Add a few words, or use at least 14 digits.")]
    [InlineData("Password2027!", "Lots of people use that one, so it's guessed early. Try a few words only you would put together.")]
    [InlineData("qwertyuiop", "Runs like abcd or 1234 are easy to guess. Try a few words you'll remember.")]
    [InlineData("aaaaaaaaaaaa", "That's the same few characters over and over. Try a few words you'll remember.")]
    public void A_password_that_is_guessed_early_is_explained_calmly_and_not_accepted(string password, string hint)
    {
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);

        vm.BackupPassword = vm.BackupPasswordAgain = password;

        Assert.Equal(hint, vm.BackupHint);
        Assert.True(vm.IsBackupHintAttention);
        Assert.Null(vm.BackupStrength);
        Assert.False(vm.CreateBackupCommand.CanExecute(null));
    }

    [Fact]
    public void A_few_words_make_a_strong_password()
    {
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);

        vm.BackupPassword = vm.BackupPasswordAgain = "olive tree by the sea";

        Assert.Equal("Strong password.", vm.BackupStrength);
        Assert.True(vm.CreateBackupCommand.CanExecute(null));
    }

    [Fact]
    public void The_password_hints_read_in_greek_too()
    {
        using var _ = new CultureScope("el-GR");
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);

        Assert.Equal("Τουλάχιστον 10 χαρακτήρες. Λίγες λέξεις που θα θυμάσαι κάνουν καλό κωδικό.", vm.BackupHint);
        vm.BackupPassword = vm.BackupPasswordAgain = "θάλασσα και ελιές";
        Assert.Equal("Δυνατός κωδικός.", vm.BackupStrength);
    }

    [Fact]
    public async Task About_maia_is_one_tap_away()
    {
        var vm = Create();

        await vm.OpenAboutCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "about" }, _app.Navigator.Visited);
    }

    [Fact]
    public async Task Creating_a_backup_hands_one_file_to_the_share_sheet()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";

        await vm.CreateBackupCommand.ExecuteAsync(null);

        Assert.Equal("Maia-backup-2026-09-30.maia", _app.BackupFiles.Shared.Single().Name);
        Assert.Equal(1, _app.Backups.Written);
        Assert.False(vm.IsBackupFormOpen);
        Assert.Equal(string.Empty, vm.BackupPassword);
        Assert.True(vm.IsAskingIfBackupKept);
        Assert.False(vm.CanStartBackup);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Creating_a_backup_says_it_is_working_until_the_share_sheet_opens()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";
        _app.BackupFiles.Hold = new TaskCompletionSource();

        var creating = vm.CreateBackupCommand.ExecuteAsync(null);
        Assert.True(vm.IsCreatingBackup);

        _app.BackupFiles.Hold.SetResult();
        await creating;
        Assert.False(vm.IsCreatingBackup);
        Assert.Equal("Making your backup… this can take a few seconds.", Text.Get("Backup_Creating"));
    }

    [Fact]
    public async Task A_backup_names_the_results_it_could_not_include()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AttachFileAsync(new Maia.Application.Plans.FileToAttach("Anomaly scan report.pdf", "application/pdf", new byte[1024]));
        _app.Attachments.Blobs.Clear();
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";

        await vm.CreateBackupCommand.ExecuteAsync(null);

        Assert.Single(_app.BackupFiles.Shared);
        Assert.Equal(
            "Your backup is ready, but one result couldn't go in because its file no longer opens on this phone: Anomaly scan report.pdf.",
            vm.BackupNotice);
        Assert.Empty(_app.Notifier.Shown);

        vm.OpenBackupFormCommand.Execute(null);
        Assert.Null(vm.BackupNotice);
    }

    [Fact]
    public async Task A_backup_counts_once_she_says_the_file_is_saved()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        Assert.Equal("No backup yet.", vm.LastBackupText);
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";
        await vm.CreateBackupCommand.ExecuteAsync(null);
        Assert.Null(_app.BackupHistory.LastBackup);

        vm.BackupKeptCommand.Execute(null);

        Assert.False(vm.IsAskingIfBackupKept);
        Assert.True(vm.CanStartBackup);
        Assert.Equal(_app.Clock.UtcNow, _app.BackupHistory.LastBackup);
        Assert.Equal("Last backup: Wed 30 Sep", vm.LastBackupText);
        Assert.Equal("Last backup: Wed 30 Sep", Create().LastBackupText);
        Assert.Equal("Good. Keep the backup password safe too.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task A_share_sheet_closed_without_saving_the_file_is_not_counted_as_a_backup()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";
        await vm.CreateBackupCommand.ExecuteAsync(null);

        vm.BackupNotKeptCommand.Execute(null);

        Assert.False(vm.IsAskingIfBackupKept);
        Assert.True(vm.CanStartBackup);
        Assert.Null(_app.BackupHistory.LastBackup);
        Assert.Equal("No backup yet.", vm.LastBackupText);
        Assert.Equal("No backup yet.", Create().LastBackupText);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Leaving_before_answering_is_not_counted_as_a_backup()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";
        await vm.CreateBackupCommand.ExecuteAsync(null);

        vm.Leave();
        await vm.LoadAsync();

        Assert.False(vm.IsAskingIfBackupKept);
        Assert.True(vm.CanStartBackup);
        Assert.Null(_app.BackupHistory.LastBackup);
        Assert.Equal("No backup yet.", vm.LastBackupText);
    }

    [Fact]
    public async Task Settings_is_a_tab_so_what_she_left_open_closes_when_she_goes_elsewhere()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();
        vm.DueDate.EditCommand.Execute(null);
        vm.AskDeleteCommand.Execute(null);
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";

        vm.Leave();

        Assert.False(vm.IsBackupFormOpen);
        Assert.Equal(string.Empty, vm.BackupPassword);
        Assert.Equal(string.Empty, vm.BackupPasswordAgain);
        Assert.False(vm.IsConfirmingDelete);
        Assert.False(vm.DueDate.IsEditing);
        Assert.True(vm.CanStartBackup);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public async Task Share_diagnostics_in_settings_hands_the_log_straight_to_the_share_sheet()
    {
        await _app.CreatePlanAsync();
        _app.Diagnostics.Entries.Add("2026-09-30T09:12:03Z 1.0(12) plan.book IOException");
        var vm = Create();

        await vm.Diagnostics.ShareCommand.ExecuteAsync(null);

        Assert.Equal("2026-09-30T09:12:03Z 1.0(12) plan.book IOException", Assert.Single(Assert.Single(_app.Diagnostics.Shared).Entries));
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Coming_back_to_settings_after_deleting_everything_shows_no_backup()
    {
        await _app.CreatePlanAsync();
        _app.BackupHistory.BackupMade(_app.Clock.UtcNow);
        var vm = Create();
        Assert.Equal("Last backup: Wed 30 Sep", vm.LastBackupText);
        vm.AskDeleteCommand.Execute(null);
        await vm.DeleteCommand.ExecuteAsync(null);
        await _app.CreatePlanAsync();

        await vm.LoadAsync();

        Assert.Equal("No backup yet.", vm.LastBackupText);
    }

    [Fact]
    public async Task A_backup_that_fails_is_not_recorded()
    {
        await _app.CreatePlanAsync();
        _app.Repository.FailLoads = new IOException("disk");
        var vm = Create();
        vm.OpenBackupFormCommand.Execute(null);
        vm.BackupPassword = vm.BackupPasswordAgain = "sea-breeze-17";

        await vm.CreateBackupCommand.ExecuteAsync(null);

        Assert.Null(_app.BackupHistory.LastBackup);
        Assert.Equal("No backup yet.", vm.LastBackupText);
    }

    [Fact]
    public async Task Deleting_everything_forgets_when_the_last_backup_was_made()
    {
        await _app.CreatePlanAsync();
        _app.BackupHistory.BackupMade(_app.Clock.UtcNow);
        _app.BackupHistory.DismissReminder(_app.Clock.UtcNow);
        var vm = Create();
        vm.AskDeleteCommand.Execute(null);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Null(_app.BackupHistory.LastBackup);
        Assert.Null(_app.BackupHistory.ReminderDismissed);
    }

    [Fact]
    public async Task The_reminders_card_shows_her_choices_each_time()
    {
        await _app.CreatePlanAsync();
        _app.ReminderSettings.IsOn = true;
        _app.ReminderSettings.Settings = new Application.Abstractions.ReminderSettings { When = Application.Abstractions.ReminderPreference.Both, DiscreetTitles = true };
        _app.NotificationAccess.Granted = true;
        var vm = Create();

        await vm.LoadAsync();

        Assert.True(vm.Reminders.IsOn);
        Assert.True(vm.Reminders.RemindBoth);
        Assert.True(vm.Reminders.DiscreetTitles);
        Assert.Null(vm.Reminders.Notice);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public void One_reminders_card_holds_the_switch_remind_me_and_a_discreet_lock_screen_and_no_card_speaks_of_a_calendar()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "SettingsPage.xaml"));
        var title = page.Descendants().Single(e => AccessibilityTests.Attr(e, "Text") == "{loc:Translate Settings_RemindersTitle}");
        var card = title.Ancestors().First(e => e.Name.LocalName == "Border");

        Assert.Equal(3, card.Descendants().Count(e => AccessibilityTests.Attr(e, "Command") == "{Binding SetRemindersCommand}"));
        Assert.Contains(card.Descendants(), e => e.Name.LocalName == "PillSwitch" && AccessibilityTests.Attr(e, "IsToggled") == "{Binding IsOn}");
        Assert.Contains(card.Descendants(), e => e.Name.LocalName == "PillSwitch" && AccessibilityTests.Attr(e, "IsToggled") == "{Binding DiscreetTitles}");
        Assert.DoesNotContain("Calendar", System.IO.File.ReadAllText(Repo.App("Views", "SettingsPage.xaml")).Replace("11-calendar-sync.jpg", string.Empty, StringComparison.Ordinal).Replace("old Calendar sync", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(3, page.Descendants().Count(e => AccessibilityTests.Attr(e, "Command") == "{Binding SetRemindersCommand}"));
        Assert.Equal(("Reminders", "Υπενθυμίσεις"), (Text.In("Settings_RemindersTitle", Text.English), Text.In("Settings_RemindersTitle", Text.Greek)));
    }

    [Fact]
    public void The_reminders_card_offers_24_hours_before_where_evening_before_was()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "SettingsPage.xaml"));
        var pills = page.Descendants().Where(e => AccessibilityTests.Attr(e, "Command") == "{Binding SetRemindersCommand}").ToList();

        Assert.Equal(new[] { "DayBefore", "TwoHoursBefore", "Both" }, pills.Select(p => AccessibilityTests.Attr(p, "CommandParameter")));
        Assert.Equal("{loc:Translate Reminder_DayBefore}", AccessibilityTests.Attr(pills[0], "Text"));
        Assert.Equal(("24 hours before", "24 ώρες πριν"), (Text.In("Reminder_DayBefore", Text.English), Text.In("Reminder_DayBefore", Text.Greek)));
        Assert.DoesNotContain("Reminder_EveningBefore", Text.KeysFor(System.Globalization.CultureInfo.InvariantCulture));
    }
}
