using System.Globalization;
using Maia.App.Controls;
using Maia.App.Views;
using Maia.Presentation.Localization;

namespace Maia.App.Services;

/// <summary>
/// The language Maia shows. The phone's language by default; once she picks one in Settings, that choice is kept
/// and applied at every start. Switching redraws the whole app (every text is read when a page is built) and
/// comes back to Settings.
/// </summary>
public sealed class AppLanguage : ILanguageSwitcher
{
    private const string PreferenceKey = "language";

    // The phone's own culture, captured before Maia changes anything, so English keeps her date format.
    private static readonly CultureInfo Phone = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Applies the saved choice, if any. Without one, a phone in a language Maia doesn't have (German, Albanian…)
    /// gets English throughout, dates and pickers included, so no other language's month names mix into it. Call
    /// once at start-up, before any page is built.
    /// </summary>
    public static void ApplySaved()
    {
        if (Preferences.Default.Get<string?>(PreferenceKey, null) is { } saved)
        {
            Apply(saved);
        }
        else if (Phone.TwoLetterISOLanguageName is not (Text.English or Text.Greek))
        {
            Apply(Text.English);
        }
    }

    /// <summary>Points the style font keys at Maia's font (see Typography); the same in every language.</summary>
    public static void ApplyFonts(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var fonts = Typography.Fonts;
        resources["FontTitle"] = fonts.Title;
        resources["FontText"] = fonts.Text;
        resources["FontTextSemiBold"] = fonts.TextSemiBold;
        resources["FontTextBold"] = fonts.TextBold;
    }

    public Task SwitchAsync(string language, LanguageReturn returnTo = LanguageReturn.Settings) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            Preferences.Default.Set(PreferenceKey, language);
            Apply(language);

            var app = Microsoft.Maui.Controls.Application.Current!;
            ApplyFonts(app.Resources);
            // Without a plan the start-up screen resumes setup, at the step she reached, by itself.
            StartupPage.ThenOpen = returnTo == LanguageReturn.Settings ? $"//main/{Tabs.Settings}" : null;
            app.Windows[0].Page = new AppShell();
        });

    private static void Apply(string language)
    {
        var culture = Phone.TwoLetterISOLanguageName == language
            ? Phone
            : CultureInfo.GetCultureInfo(language == Text.Greek ? "el-GR" : "en-GB");

        // Only the process-wide defaults: every thread reads them live. Setting CurrentUICulture here would only
        // last for this async flow (it is async-local) and would then pin the main thread to the old language.
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
