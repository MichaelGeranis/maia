using Maia.Presentation.Localization;
using Maia.Presentation.Settings;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>"Share diagnostics": one tap hands the problem log to the share sheet, or says there is nothing to share.</summary>
public sealed class DiagnosticsTests
{
    private const string Header = "Maia 1.0 (12) · Android 14";

    private readonly RecordingNotifier _notifier = new();
    private readonly RecordingErrorReporter _errors = new();
    private readonly FakeDiagnostics _diagnostics = new(Header);

    [Fact]
    public async Task One_tap_hands_every_noted_line_to_the_share_sheet()
    {
        _diagnostics.Entries.Add("2026-10-01T09:12:03Z 1.0(12) plan.book IOException");
        _diagnostics.Entries.Add("2026-10-01T09:14:40Z 1.0(12) reminders.set UnauthorizedAccessException");
        var vm = new DiagnosticsViewModel(_diagnostics, _notifier, _errors);

        await vm.ShareCommand.ExecuteAsync(null);

        var shared = Assert.Single(_diagnostics.Shared);
        Assert.Equal(Header, shared.Header);
        Assert.Equal(
            new[] { "2026-10-01T09:12:03Z 1.0(12) plan.book IOException", "2026-10-01T09:14:40Z 1.0(12) reminders.set UnauthorizedAccessException" },
            shared.Entries);
        Assert.Empty(_notifier.Shown);
        Assert.False(vm.IsBusy);
        Assert.True(vm.ShareCommand.CanExecute(null));
    }

    [Fact]
    public async Task Each_tap_shares_the_log_as_it_is_then()
    {
        _diagnostics.Entries.Add("2026-10-01T09:12:03Z 1.0(12) plan.book IOException");
        var vm = new DiagnosticsViewModel(_diagnostics, _notifier, _errors);
        await vm.ShareCommand.ExecuteAsync(null);

        _diagnostics.Entries.Add("2026-10-01T09:20:00Z 1.0(12) reminders.set IOException");
        await vm.ShareCommand.ExecuteAsync(null);

        Assert.Equal(new[] { 1, 2 }, _diagnostics.Shared.Select(r => r.Entries.Count));
    }

    [Theory]
    [InlineData("en-US", "Nothing to share. Maia hasn't noted any problems.")]
    [InlineData("el-GR", "Δεν υπάρχει κάτι για κοινοποίηση. Η Maia δεν έχει σημειώσει κανένα πρόβλημα.")]
    public async Task With_no_problem_noted_a_toast_says_there_is_nothing_to_share(string culture, string toast)
    {
        using var _ = new CultureScope(culture);
        var vm = new DiagnosticsViewModel(_diagnostics, _notifier, _errors);

        await vm.ShareCommand.ExecuteAsync(null);

        Assert.Equal(toast, Assert.Single(_notifier.Shown));
        Assert.Empty(_diagnostics.Shared);
        Assert.Empty(_errors.Reports);
    }

    [Fact]
    public async Task A_share_sheet_that_fails_says_so_calmly()
    {
        using var _ = new CultureScope("en-US");
        _diagnostics.Entries.Add("2026-10-01T09:12:03Z 1.0(12) plan.book IOException");
        _diagnostics.FailShare = true;
        var vm = new DiagnosticsViewModel(_diagnostics, _notifier, _errors);

        await vm.ShareCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", Assert.Single(_notifier.Shown));
        Assert.Equal("settings.diagnostics_share", Assert.Single(_errors.Reports).Operation);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task A_log_that_cannot_be_read_says_so_calmly_and_shares_nothing()
    {
        using var _ = new CultureScope("en-US");
        _diagnostics.Entries.Add("2026-10-01T09:12:03Z 1.0(12) plan.book IOException");
        _diagnostics.FailRead = true;
        var vm = new DiagnosticsViewModel(_diagnostics, _notifier, _errors);

        await vm.ShareCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", Assert.Single(_notifier.Shown));
        Assert.Equal("settings.diagnostics_read", Assert.Single(_errors.Reports).Operation);
        Assert.Empty(_diagnostics.Shared);
    }

    [Fact]
    public void Settings_offers_one_share_button_and_no_preview()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "SettingsPage.xaml"));
        var card = page.Descendants().Single(e => AccessibilityTests.Attr(e, "Name") == "DiagnosticsCard");
        var buttons = card.Descendants().Where(AccessibilityTests.IsTappable).ToList();

        var share = Assert.Single(buttons);
        Assert.Equal("{loc:Translate Diagnostics_Open}", AccessibilityTests.Attr(share, "Text"));
        Assert.Equal("{Binding ShareCommand}", AccessibilityTests.Attr(share, "Command"));
        Assert.DoesNotContain(card.Descendants(), e => AccessibilityTests.Attr(e, "Text") is "{Binding Preview}" or "{Binding Intro}");
        Assert.Equal("Share diagnostics", Text.In("Diagnostics_Open", Text.English));
        Assert.Equal("Κοινοποίηση διαγνωστικών", Text.In("Diagnostics_Open", Text.Greek));
    }
}
