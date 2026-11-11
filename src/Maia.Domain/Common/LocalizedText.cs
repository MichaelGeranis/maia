using System.Collections.ObjectModel;

namespace Maia.Domain.Common;

/// <summary>
/// Text that exists in several languages, keyed by two-letter ISO 639-1 code.
/// English is mandatory: it is the fallback for every language the app does not yet cover.
/// </summary>
public sealed class LocalizedText : IEquatable<LocalizedText>
{
    public const string English = "en";
    public const string Greek = "el";

    private readonly ReadOnlyDictionary<string, string> _values;

    public LocalizedText(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, text) in values)
        {
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            normalized[language.Trim().ToLowerInvariant()] = text.Trim();
        }

        if (!normalized.ContainsKey(English))
        {
            throw new DomainException("text.english.missing", "Localized text must include English.");
        }

        _values = new ReadOnlyDictionary<string, string>(normalized);
    }

    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Creates text that has only an English (or user-typed) value.</summary>
    public static LocalizedText Plain(string text) =>
        new(new Dictionary<string, string> { [English] = text });

    /// <summary>Returns the text in <paramref name="language"/>, falling back to English.</summary>
    public string In(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return _values.TryGetValue(language, out var text) ? text : _values[English];
    }

    public bool Has(string language) => _values.ContainsKey(language);

    public override string ToString() => _values[English];

    public bool Equals(LocalizedText? other) =>
        other is not null
        && _values.Count == other._values.Count
        && _values.All(pair => other._values.TryGetValue(pair.Key, out var value) && value == pair.Value);

    public override bool Equals(object? obj) => Equals(obj as LocalizedText);

    public override int GetHashCode() => _values[English].GetHashCode(StringComparison.Ordinal);
}
