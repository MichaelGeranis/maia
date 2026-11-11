namespace Maia.Presentation.Navigation;

/// <summary>
/// Asks her to choose one thing from a list, with the phone's own list of choices and a Cancel (the entry to tag a
/// result to, say). Implemented by the app.
/// </summary>
public interface IChooser
{
    /// <param name="title">What she is choosing.</param>
    /// <param name="choices">The choices, each worded differently from the others.</param>
    /// <returns>The index of the one she chose, or null if she cancelled.</returns>
    Task<int?> ChooseAsync(string title, IReadOnlyList<string> choices);
}
