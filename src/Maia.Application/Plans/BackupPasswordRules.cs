using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Maia.Application.Plans;

/// <summary>What stops a password from protecting a backup, or <see cref="None"/> when it is good enough.</summary>
public enum BackupPasswordIssue
{
    None,

    /// <summary>Fewer than <see cref="BackupPasswordRules.MinLength"/> characters.</summary>
    TooShort,

    /// <summary>
    /// One character, or a few, over and over ("aaaaaaaaaa", "abababab", "passpasspass"), also with a few characters
    /// added ("passwordpassword1").
    /// </summary>
    Repeated,

    /// <summary>
    /// A run of letters, digits or keys ("abcdefghij", "9876543210", "qwertyuiop"), also one that goes round again
    /// ("1234567890123456", "qwertyuiopqwerty") or has a few characters added ("1234567890!", "qwertyuiop12").
    /// </summary>
    Sequence,

    /// <summary>
    /// Only digits (dates, phone numbers), fewer than <see cref="BackupPasswordRules.MinDigitsOnly"/> of them. Symbols and
    /// separators around or between them don't count ("17/01/2027!!", "+306912345678").
    /// </summary>
    DigitsOnly,

    /// <summary>A password many people use, maybe with digits or symbols around it ("password2027!", "agapimou1").</summary>
    Common,
}

/// <summary>
/// The rule for a backup password (ADR 0005). The backup travels by email or Drive, so anyone who gets the file can guess
/// offline for as long as they like; PBKDF2 slows each guess down but can't save a password that is among the first
/// guesses. Following NIST SP 800-63B there are no composition rules (no "one capital, one symbol"): a length floor, and
/// passwords that are guessed first are turned away. A few words she'll remember pass easily.
/// </summary>
public static class BackupPasswordRules
{
    /// <summary>Shortest backup password Maia accepts.</summary>
    public const int MinLength = 10;

    /// <summary>A password of digits alone (a date, a phone number) needs at least this many.</summary>
    public const int MinDigitsOnly = 14;

    /// <summary>From this length an acceptable password counts as strong.</summary>
    public const int StrongLength = 16;

    /// <summary>
    /// How many characters added to a run or a repeat still leave it easy to guess: guessing programs try a digit or a
    /// symbol or two before and after everything first.
    /// </summary>
    private const int MaxAdded = 3;

    /// <summary>
    /// Runs of keys on English and Greek keyboards, the digit row and the alphabets. Each also counts going round again
    /// ("…7890123…", "qwertyuiopqwerty"). Folded (see <see cref="Fold"/>).
    /// </summary>
    private static readonly string[] KeyRuns =
    [
        "qwertyuiopasdfghjklzxcvbnm",
        "qwertyuiop",
        "asdfghjkl",
        "zxcvbnm",
        "1234567890",
        "!@#$%^&*()",
        "1q2w3e4r5t6y7u8i9o0p",
        "1qaz2wsx3edc4rfv5tgb6yhn7ujm8ik9ol0p",
        "qazwsxedcrfvtgbyhnujmikolp",
        "zaq1xsw2cde3vfr4bgt5nhy6mju7",
        "abcdefghijklmnopqrstuvwxyz",
        Fold("ςερτυθιοπασδφγηξκλζχψωβνμ"),
        Fold("ςερτυθιοπ"),
        Fold("ασδφγηξκλ"),
        Fold("ζχψωβνμ"),
        Fold("αβγδεζηθικλμνξοπρστυφχψω"),
    ];

    /// <summary>
    /// Passwords people pick first, in English, Greek and Greeklish, and words anyone would try against Maia. Compared
    /// folded, with digits and symbols around them taken off and common letter swaps undone ("P@ssw0rd2027!").
    /// </summary>
    private static readonly HashSet<string> CommonWords = new(
        new[]
        {
            "password", "passwort", "mypassword", "yourpassword", "secret", "letmein", "welcome", "changeme", "default",
            "iloveyou", "iloveu", "loveyou", "love", "lovely", "sunshine", "princess", "football", "baseball", "soccer",
            "monkey", "dragon", "superman", "batman", "spiderman", "master", "shadow", "hunter", "charlie", "freedom",
            "whatever", "starwars", "computer", "admin", "administrator", "login", "hello", "helloworld", "flower",
            "summer", "winter", "spring", "autumn", "family", "friends", "forever", "angel", "blessed", "cookie",
            "chocolate", "pokemon", "google", "facebook", "instagram", "samsung", "iphone", "apple", "android", "test",
            "testing", "backup", "trustnoone", "trustno", "qwerty", "qwertz", "azerty", "asdf", "asdfgh", "asdfghjkl",
            "zxcvbnm", "qazwsx", "zaqwsx", "abc", "abcd", "abcdef", "michael", "jennifer", "jessica", "jordan",
            "maia", "maiaapp", "mymaia", "baby", "mybaby", "babyboy", "babygirl", "ourbaby", "newbaby", "littleone",
            "mommy", "mummy", "mommytobe", "momtobe", "mumtobe", "mama", "mamma", "mother", "motherhood", "pregnancy",
            "pregnant", "bump", "babybump",
            "agapi", "agapimou", "sagapo", "sagapw", "kalimera", "kalinixta", "ellada", "hellas", "greece", "athens",
            "athina", "thessaloniki", "olympiakos", "panathinaikos", "paok", "aek", "kodikos", "moro", "mwro",
            "moromou", "mitera", "manoula", "panagia", "christos", "eleni", "maria", "giorgos", "dimitris", "kostas",
            "nikos", "giannis",
            "αγάπη", "αγάπημου", "σαγαπώ", "καλημέρα", "καληνύχτα", "ελλάδα", "αθήνα", "θεσσαλονίκη", "ολυμπιακός",
            "παναθηναϊκός", "παοκ", "αεκ", "κωδικός", "μωρό", "μωρόμου", "μωράκι", "μαμά", "μανούλα", "μητέρα",
            "εγκυμοσύνη", "παναγία", "χριστός", "ελένη", "μαρία", "γιώργος", "δημήτρης", "κώστας", "νίκος", "γιάννης",
        }.Select(Fold),
        StringComparer.Ordinal);

    /// <summary>What stops <paramref name="password"/> from protecting a backup, or <see cref="BackupPasswordIssue.None"/>.</summary>
    public static BackupPasswordIssue Check(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var text = password.Normalize(NormalizationForm.FormC);
        var elements = TextElements(text);
        if (elements.Count < MinLength)
        {
            return BackupPasswordIssue.TooShort;
        }

        // Compared without separators, and without the symbols around it: the first thing guessing programs add.
        var folded = Fold(text);
        var compact = string.Concat(folded.Where(c => !IsSeparator(c)));
        var core = compact.Trim(Symbols(compact));
        string[] wholes = core.Length > 0 && core != compact ? [compact, core] : [compact];
        if (IsRepeated(elements) || wholes.Any(whole => IsRepeated(TextElements(whole))))
        {
            return BackupPasswordIssue.Repeated;
        }

        if (wholes.Any(IsRun))
        {
            return BackupPasswordIssue.Sequence;
        }

        if (IsDigitsOnly(compact, out var digits) && digits < MinDigitsOnly)
        {
            return BackupPasswordIssue.DigitsOnly;
        }

        // A repeat or a run with a digit or a letter or two added: "passwordpassword1", "qwertyuiop12", "x1234567890".
        foreach (var part in NearlyAll(core))
        {
            if (IsRepeated(TextElements(part)))
            {
                return BackupPasswordIssue.Repeated;
            }

            if (IsRun(part))
            {
                return BackupPasswordIssue.Sequence;
            }
        }

        return IsCommon(folded) ? BackupPasswordIssue.Common : BackupPasswordIssue.None;
    }

    /// <summary>An acceptable password that is long enough to count as strong (a few words usually are).</summary>
    public static bool IsStrong(string password) =>
        Check(password) == BackupPasswordIssue.None && TextElements(password.Normalize(NormalizationForm.FormC)).Count >= StrongLength;

    /// <summary>Lower case without accents, final sigma as σ: how people's passwords compare.</summary>
    private static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var kept = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                kept.Append(c == 'ς' ? 'σ' : char.ToLowerInvariant(c));
            }
        }

        return kept.ToString().Normalize(NormalizationForm.FormC);
    }

    private static List<string> TextElements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }

    private static bool IsSeparator(char c) => char.IsWhiteSpace(c) || c is '.' or '-' or '/' or '_' or ',' or ':' or '\\';

    /// <summary>Three different characters or fewer, or one short piece over and over.</summary>
    private static bool IsRepeated(List<string> elements)
    {
        if (elements.Count == 0 || elements.Distinct(StringComparer.Ordinal).Count() <= 3)
        {
            return true;
        }

        for (var unit = 1; unit <= elements.Count / 2; unit++)
        {
            var repeats = true;
            for (var i = unit; i < elements.Count && repeats; i++)
            {
                repeats = string.Equals(elements[i], elements[i % unit], StringComparison.Ordinal);
            }

            if (repeats)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Letters or digits one after another ("abcdefghij", "9876543210"), or a run along a keyboard, also going round
    /// again ("1234567890123", "qwertyuiopqwerty").
    /// </summary>
    /// <remarks>
    /// Only asked of text that is not <see cref="IsRepeated"/>: four different characters or more, so never shorter
    /// than three.
    /// </remarks>
    private static bool IsRun(string text)
    {
        Debug.Assert(text.Length >= 3, "Only text that is not repeated is checked for a run.");
        var step = text[1] - text[0];
        if (Math.Abs(step) == 1 && Enumerable.Range(1, text.Length - 1).All(i => text[i] - text[i - 1] == step))
        {
            return true;
        }

        var reversed = new string(text.Reverse().ToArray());
        return KeyRuns.Any(run =>
        {
            var round = string.Concat(Enumerable.Repeat(run, (text.Length / run.Length) + 2));
            return round.Contains(text, StringComparison.Ordinal) || round.Contains(reversed, StringComparison.Ordinal);
        });
    }

    /// <summary>Only digits, maybe with symbols around or between them (a date, "+30 691…"), and how many digits.</summary>
    private static bool IsDigitsOnly(string compact, out int digits)
    {
        digits = compact.Count(char.IsDigit);
        return digits > 0 && compact.All(c => char.IsDigit(c) || !char.IsLetter(c));
    }

    /// <summary>
    /// The password with one to <see cref="MaxAdded"/> characters taken off its ends, while most of it is left: what is
    /// under a digit or a letter or two added before or after.
    /// </summary>
    private static IEnumerable<string> NearlyAll(string core)
    {
        for (var start = 0; start <= MaxAdded; start++)
        {
            for (var end = start == 0 ? 1 : 0; start + end <= MaxAdded; end++)
            {
                var length = core.Length - start - end;
                if (length >= MinLength - MaxAdded)
                {
                    yield return core.Substring(start, length);
                }
            }
        }
    }

    /// <summary>The characters in <paramref name="text"/> that are neither letters nor digits.</summary>
    private static char[] Symbols(string text) => text.Where(c => !char.IsLetterOrDigit(c)).Distinct().ToArray();

    /// <summary>A common password, maybe with digits or symbols around it or letters swapped for look-alikes.</summary>
    private static bool IsCommon(string folded)
    {
        var core = folded.Trim().TrimStart(NotLetters(folded)).TrimEnd(NotLetters(folded));
        var candidates = new[] { folded, core, Unswap(core) };
        return candidates
            .Select(c => string.Concat(c.Where(ch => !IsSeparator(ch))))
            .Any(c => c.Length > 0 && CommonWords.Contains(c));
    }

    private static char[] NotLetters(string text) => text.Where(c => !char.IsLetter(c)).Distinct().ToArray();

    /// <summary>Undoes the usual look-alike swaps: "p@ssw0rd" reads as "password".</summary>
    private static string Unswap(string text) => string.Concat(text.Select(c => c switch
    {
        '@' or '4' => 'a',
        '0' => 'o',
        '1' or '!' => 'i',
        '3' => 'e',
        '$' or '5' => 's',
        '7' => 't',
        _ => c,
    }));
}
