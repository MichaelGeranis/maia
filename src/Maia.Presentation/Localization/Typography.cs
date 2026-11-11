namespace Maia.Presentation.Localization;

/// <summary>The fonts behind the four style keys: FontTitle, FontText, FontTextSemiBold and FontTextBold.</summary>
public sealed record FontSet(string Title, string Text, string TextSemiBold, string TextBold);

/// <summary>
/// Maia's one font, M PLUS Rounded 1c: soft rounded letters with Latin and Greek drawn alike, so English and Greek look
/// the same and nothing changes with the language. Titles use its Bold weight; the aliases are registered in MauiProgram.
/// </summary>
public static class Typography
{
    public static readonly FontSet Fonts = new("RoundedBold", "RoundedRegular", "RoundedMedium", "RoundedBold");
}
