using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.App.Services;

/// <summary>
/// The phone's own action sheet: a title, one line per choice and Cancel. It asks whom to call when she has more than
/// one OB-GYN contact, and which entry to tag a result to.
/// </summary>
public sealed class ChoiceSheet : IChooser
{
    public Task<int?> ChooseAsync(string title, IReadOnlyList<string> choices) => ShowAsync(title, choices);

    /// <returns>The index of the line she tapped, or null if she cancelled or no screen is showing.</returns>
    internal static Task<int?> ShowAsync(string title, IReadOnlyList<string> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Shell.Current?.CurrentPage;
            if (page is null)
            {
                return (int?)null;
            }

            var chosen = await page.DisplayActionSheetAsync(title, Text.Get("Common_Cancel"), null, [.. choices]);
            var index = chosen is null ? -1 : choices.ToList().IndexOf(chosen);
            return index >= 0 ? index : (int?)null;
        });
    }
}
