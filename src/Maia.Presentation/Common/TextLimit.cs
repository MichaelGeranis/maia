using Maia.Presentation.Localization;

namespace Maia.Presentation.Common;

/// <summary>
/// A calm line under a field that has a limit, once she nears it: how many characters are left, then that it is full.
/// A field stops taking letters at its limit, so without this the end of what she types would go without a word.
/// </summary>
public static class TextLimit
{
    /// <summary>The line shows once this many characters or fewer are left.</summary>
    public const int ShowWithin = 20;

    /// <summary>The line for <paramref name="text"/> in a field of <paramref name="max"/> characters, or null while there is room.</summary>
    public static string? Hint(string? text, int max)
    {
        var left = max - (text?.Length ?? 0);
        return left > ShowWithin ? null
            : left <= 0 ? Text.Format("Common_LimitReached", max)
            : left == 1 ? Text.Get("Common_OneCharacterLeft")
            : Text.Format("Common_CharactersLeft", left);
    }
}
