using System.Globalization;
using System.Resources;

namespace Maia.Presentation.Localization;

/// <summary>
/// UI text in the current UI culture. English is the neutral (fallback) language; Greek ships from v1.
/// Keys are stable and grouped by screen (<c>Setup_…</c>, <c>Week_…</c>).
/// </summary>
public static class Text
{
    public const string English = "en";
    public const string Greek = "el";

    private static readonly ResourceManager Resources = new("Maia.Presentation.Resources.Strings", typeof(Text).Assembly);

    /// <summary>Two-letter code of the language the app shows: Greek when the phone is in Greek, otherwise English.</summary>
    public static string Language =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Greek ? Greek : English;

    /// <summary>
    /// How dates and numbers are written: the phone's own format when the phone is in the language Maia shows,
    /// otherwise British English or Greek. A German or Albanian phone gets English, so its month and day names never
    /// end up inside an English sentence.
    /// </summary>
    public static CultureInfo Culture
    {
        get
        {
            var language = Language;
            var current = CultureInfo.CurrentCulture;
            return current.TwoLetterISOLanguageName == language
                ? current
                : CultureInfo.GetCultureInfo(language == Greek ? "el-GR" : "en-GB");
        }
    }

    /// <summary>Returns the text for <paramref name="key"/>, or the key itself if it is missing (visible in testing, never a crash).</summary>
    public static string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    /// <summary>
    /// The text for <paramref name="key"/> in <paramref name="language"/> (<see cref="English"/> or
    /// <see cref="Greek"/>), whichever language Maia shows now.
    /// </summary>
    public static string In(string key, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var culture = language == Greek ? CultureInfo.GetCultureInfo(Greek) : CultureInfo.InvariantCulture;
        return Resources.GetString(key, culture) ?? key;
    }

    /// <summary>The text for <paramref name="key"/> with its placeholders filled in, numbers written as in <see cref="Culture"/>.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(Culture, Get(key), args);

    /// <summary>All keys defined for a culture, without falling back. Used by tests to catch missing translations.</summary>
    internal static IReadOnlySet<string> KeysFor(CultureInfo culture)
    {
        var set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (set is null)
        {
            return keys;
        }

        foreach (System.Collections.DictionaryEntry entry in set)
        {
            keys.Add((string)entry.Key);
        }

        return keys;
    }
}
