using System.Globalization;
using System.Text.RegularExpressions;
using Maia.Presentation.About;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Tests;

public sealed class AboutTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();
    private readonly FakeLicenceTexts _texts = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task About_shows_the_version_and_leads_to_the_privacy_notice_the_terms_and_licences()
    {
        var vm = new AboutViewModel(new FixedVersion(), _app.Navigator);

        await vm.OpenPrivacyCommand.ExecuteAsync(null);
        await vm.OpenTermsCommand.ExecuteAsync(null);
        await vm.OpenLicencesCommand.ExecuteAsync(null);

        Assert.Equal("Version 0.1 (1)", vm.Version);
        Assert.Equal(new[] { "privacy", "terms", "licences" }, _app.Navigator.Visited);
    }

    [Fact]
    public void About_says_maia_organises_and_her_doctor_decides()
    {
        var intro = Text.Get("About_Intro");

        Assert.Contains("organises", intro, StringComparison.Ordinal);
        Assert.Contains("Your doctor decides", intro, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("el-GR")]
    public void The_privacy_notice_has_every_part_in_both_languages(string culture)
    {
        using var _ = new CultureScope(culture);
        var keys = Text.KeysFor(CultureInfo.GetCultureInfo(culture == "el-GR" ? "el" : string.Empty));

        var notice = new PrivacyViewModel(_app.Navigator);

        Assert.Equal(PrivacyViewModel.SectionKeys.Length, notice.Sections.Count);
        Assert.All(PrivacyViewModel.SectionKeys, k => Assert.Contains($"Privacy_{k}Title", keys));
        Assert.All(PrivacyViewModel.SectionKeys, k => Assert.Contains($"Privacy_{k}Body", keys));
        Assert.All(notice.Sections, s => Assert.DoesNotContain("Privacy_", s.Title + s.Body, StringComparison.Ordinal));
        Assert.Contains(notice.Sections, s => s.Body.Contains("112", StringComparison.Ordinal));
    }

    [Fact]
    public void The_privacy_notice_says_where_her_data_is_and_what_she_can_do()
    {
        var text = string.Join(" ", new PrivacyViewModel(_app.Navigator).Sections.Select(s => s.Title + " " + s.Body));

        Assert.Contains("Only on this phone, encrypted", text, StringComparison.Ordinal);
        Assert.Contains("doesn't use the internet", text, StringComparison.Ordinal);
        Assert.Contains("no trackers", text, StringComparison.Ordinal);
        Assert.Contains("Maia doesn't read your contacts", text, StringComparison.Ordinal);
        Assert.Contains("Delete all data", text, StringComparison.Ordinal);
        Assert.Contains("GDPR", text, StringComparison.Ordinal);
        Assert.Contains("gives no medical advice", text, StringComparison.Ordinal);
        Assert.DoesNotContain("replace", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("en-US", "“Maia reminder”", "never name a check", "Open in another app", "may keep one", "last made a backup", "Maia never sees them", "recent apps", "error code", "Share diagnostics")]
    [InlineData("el-GR", "«Υπενθύμιση από τη Maia»", "δεν αναφέρουν ποτέ εξέταση", "Άνοιγμα σε άλλη εφαρμογή", "μπορεί να κρατήσει δικό της", "τελευταία φορά αντίγραφο", "δεν τα βλέπει ποτέ", "πρόσφατων εφαρμογών", "κωδικό σφάλματος", "Κοινοποίηση διαγνωστικών")]
    public void The_privacy_notice_covers_reminders_shared_files_the_lock_and_diagnostics(string culture, params string[] phrases)
    {
        using var _ = new CultureScope(culture);

        var text = string.Join(" ", new PrivacyViewModel(_app.Navigator).Sections.Select(s => s.Title + " " + s.Body));

        // Each names a feature that keeps or hands over something (review #5): the notice says what, where and who sees it.
        Assert.All(phrases, phrase => Assert.Contains(phrase, text, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en-US", "Maia never writes to your calendar.")]
    [InlineData("el-GR", "Η Maia δεν γράφει ποτέ στο ημερολόγιό σου.")]
    public void The_privacy_notice_says_reminders_are_maias_own_and_nothing_goes_to_her_calendar(string culture, string phrase)
    {
        using var _ = new CultureScope(culture);

        var sections = new PrivacyViewModel(_app.Navigator).Sections;
        var reminders = sections.Single(s => s.Title == Text.Get("Privacy_RemindersTitle")).Body;

        Assert.EndsWith(phrase, reminders, StringComparison.Ordinal);
        Assert.DoesNotContain(sections, s => s.Body.Contains("Google", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en-US", "keeps it, encrypted, with your results", "If you link your doctors")]
    [InlineData("el-GR", "το κρατά, κρυπτογραφημένο, στα αποτελέσματά σου", "Αν συνδέσεις τους γιατρούς σου")]
    public void The_privacy_notice_says_a_shared_file_is_kept_at_once_and_names_every_doctor_she_links(string culture, string sharedFile, string doctors)
    {
        using var _ = new CultureScope(culture);

        var text = string.Join(" ", new PrivacyViewModel(_app.Navigator).Sections.Select(s => s.Body));

        // A file shared into Maia lands in Results at once, untagged; she no longer files it under a check first. And
        // Care team links her other doctors too, not only her OB-GYN.
        Assert.Contains(sharedFile, text, StringComparison.Ordinal);
        Assert.Contains(doctors, text, StringComparison.Ordinal);
        Assert.DoesNotContain("file it under a check", text, StringComparison.Ordinal);
        Assert.DoesNotContain("καταχωρίσεις σε μια εξέταση", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("el-GR")]
    public void The_privacy_notice_names_the_buttons_as_the_app_shows_them(string culture)
    {
        using var _ = new CultureScope(culture);

        var notice = string.Join(" ", new PrivacyViewModel(_app.Navigator).Sections.Select(s => s.Body));

        // If a label changes, the notice changes with it, so she can find what it describes.
        Assert.Contains(Text.Get("Viewer_OpenOutside"), notice, StringComparison.Ordinal);
        Assert.Contains(Text.Get("Diagnostics_Open"), notice, StringComparison.Ordinal);
        Assert.Contains(Text.Get("Settings_Delete"), notice, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("en-US", "Changes to this notice", "the times you set on the timer")]
    [InlineData("el-GR", "Αλλαγές σε αυτή την ενημέρωση", "οι χρόνοι που ορίζεις στο χρονόμετρο")]
    public void The_privacy_notice_says_how_it_changes_and_that_settings_are_kept_apart(string culture, string changes, string settings)
    {
        using var _ = new CultureScope(culture);

        var sections = new PrivacyViewModel(_app.Navigator).Sections;

        // The notice ships inside the app: a new one comes with a new version, dated. The care part stays last.
        Assert.Contains(sections, s => s.Title == changes);
        Assert.Contains(settings, sections[0].Body, StringComparison.Ordinal);
        Assert.Equal(Text.Get("Privacy_CareTitle"), sections[^1].Title);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("el-GR")]
    public void The_terms_have_every_part_in_both_languages(string culture)
    {
        using var _ = new CultureScope(culture);
        var keys = Text.KeysFor(CultureInfo.GetCultureInfo(culture == "el-GR" ? "el" : string.Empty));

        var terms = new TermsViewModel(_app.Navigator);

        Assert.Equal(TermsViewModel.SectionKeys.Length, terms.Sections.Count);
        Assert.All(TermsViewModel.SectionKeys, k => Assert.Contains($"Terms_{k}Title", keys));
        Assert.All(TermsViewModel.SectionKeys, k => Assert.Contains($"Terms_{k}Body", keys));
        Assert.All(terms.Sections, s => Assert.DoesNotContain("Terms_", s.Title + s.Body, StringComparison.Ordinal));
        Assert.Contains(terms.Sections, s => s.Body.Contains("112", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en-US", "isn't a medical device", "gives no medical advice", "Your doctor decides", "isn't for emergencies", "As far as the law allows", "gross negligence", "your rights as a consumer", "Greek law")]
    [InlineData("el-GR", "Δεν είναι ιατροτεχνολογικό προϊόν", "Δεν δίνει ιατρικές συμβουλές", "Αποφασίζει ο γιατρός σου", "δεν είναι για επείγοντα", "Στον βαθμό που επιτρέπει ο νόμος", "βαριά αμέλεια", "τα δικαιώματά σου ως καταναλώτρια", "ελληνικό δίκαιο")]
    public void The_terms_say_maia_organises_her_doctor_decides_and_liability_is_limited_only_as_far_as_the_law_allows(string culture, params string[] phrases)
    {
        using var _ = new CultureScope(culture);

        var text = string.Join(" ", new TermsViewModel(_app.Navigator).Sections.Select(s => s.Title + " " + s.Body));

        // The disclaimers that matter most, and the line that keeps the limits lawful: liability the law won't let
        // anyone limit, and her rights as a consumer, stay as they are.
        Assert.All(phrases, phrase => Assert.Contains(phrase, text, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("el-GR")]
    public void The_terms_name_the_screens_and_words_as_the_app_shows_them(string culture)
    {
        using var _ = new CultureScope(culture);

        var text = string.Join(" ", new TermsViewModel(_app.Navigator).Sections.Select(s => s.Title + " " + s.Body));

        // If a name changes, the terms change with it, so she can find what they point to.
        Assert.Contains(Text.Get("Privacy_Title"), text, StringComparison.Ordinal);
        Assert.Contains(Text.Get("Licences_Title"), text, StringComparison.Ordinal);
        Assert.Contains(Text.Get("SetupPlan_Title"), text, StringComparison.Ordinal);
        Assert.Contains(Text.Get("Breathe_Title"), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("replace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("monitor", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("track", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_licence_opens_in_place_with_its_full_text()
    {
        var vm = new LicencesViewModel(_texts, _app.Navigator, _app.Errors);
        var font = vm.Fonts[0];

        Assert.Equal("Show licence", font.ToggleText);
        await font.ToggleCommand.ExecuteAsync(null);

        Assert.True(font.IsOpen);
        Assert.Equal("text of mplusrounded1c-OFL.txt", font.FullText);
        Assert.Equal("Hide licence", font.ToggleText);

        await font.ToggleCommand.ExecuteAsync(null);
        Assert.False(font.IsOpen);
    }

    [Fact]
    public async Task A_licence_text_that_cannot_be_read_is_said_calmly()
    {
        _texts.Missing = true;
        var vm = new LicencesViewModel(_texts, _app.Navigator, _app.Errors);

        await vm.Libraries[0].ToggleCommand.ExecuteAsync(null);

        Assert.Equal("The licence text couldn't be opened.", vm.Libraries[0].FullText);
        Assert.Equal("licences.read", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public void Every_licence_text_named_on_the_licences_screen_ships_with_maia()
    {
        var vm = new LicencesViewModel(_texts, _app.Navigator, _app.Errors);
        var shipped = Directory.GetFiles(Path.Combine(RepoRoot(), "licenses"), "*.txt", SearchOption.AllDirectories).Select(Path.GetFileName).ToHashSet();

        Assert.All(vm.Fonts.Concat(vm.Icons).Concat(vm.Libraries), item => Assert.Contains(item.File, shipped));
        Assert.Equal("Lucide", Assert.Single(vm.Icons).Name);
        Assert.Equal("M PLUS Rounded 1c", Assert.Single(vm.Fonts).Name);
    }

    [Fact]
    public void Every_package_the_app_references_is_on_the_licences_screen_and_in_the_notices()
    {
        var root = RepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "Maia.App", "Maia.App.csproj"));
        var notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md"));
        var listed = new LicencesViewModel(_texts, _app.Navigator, _app.Errors).Libraries.SelectMany(l => l.Packages).ToHashSet();
        var referenced = Regex.Matches(app, "PackageReference Include=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(referenced);
        Assert.All(referenced, p => Assert.Contains(p, listed));
        Assert.All(listed, p => Assert.Contains(p, notices, StringComparison.Ordinal));
    }

    [Fact]
    public void A_library_lists_its_packages_and_a_font_lists_none()
    {
        var vm = new LicencesViewModel(_texts, _app.Navigator, _app.Errors);
        var maui = vm.Libraries[0];
        var font = vm.Fonts[0];

        Assert.True(maui.HasPackages);
        Assert.StartsWith("Packages: Microsoft.Maui.Controls, Microsoft.Maui.Controls.Core", maui.PackageList, StringComparison.Ordinal);
        Assert.False(font.HasPackages);
        Assert.Equal(string.Empty, font.PackageList);
    }

    [Fact]
    public async Task Back_on_about_the_privacy_notice_the_terms_and_licences_goes_back_each_time()
    {
        await new AboutViewModel(new FixedVersion(), _app.Navigator).BackCommand.ExecuteAsync(null);
        await new PrivacyViewModel(_app.Navigator).BackCommand.ExecuteAsync(null);
        await new TermsViewModel(_app.Navigator).BackCommand.ExecuteAsync(null);
        await new LicencesViewModel(_texts, _app.Navigator, _app.Errors).BackCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "back", "back", "back", "back" }, _app.Navigator.Visited);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Maia.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Maia.slnx not found above the test output.");
    }

    private sealed class FixedVersion : IAppVersion
    {
        public string Version => "0.1";

        public string Build => "1";
    }

    private sealed class FakeLicenceTexts : ILicenceTexts
    {
        public bool Missing { get; set; }

        public Task<string> ReadAsync(string fileName) =>
            Missing ? throw new FileNotFoundException("not bundled") : Task.FromResult("text of " + fileName);
    }
}
