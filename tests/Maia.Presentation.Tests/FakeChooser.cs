using Maia.Presentation.Navigation;

namespace Maia.Presentation.Tests;

/// <summary>The phone's list of choices: records what each list offered, and taps the line the test picks (null is Cancel).</summary>
internal sealed class FakeChooser : IChooser
{
    public List<(string Title, IReadOnlyList<string> Choices)> Asked { get; } = [];

    /// <summary>Picks the line she taps from what the list offers; by default she cancels.</summary>
    public Func<IReadOnlyList<string>, int?> Pick { get; set; } = _ => null;

    /// <summary>She taps the line that reads <paramref name="text"/>.</summary>
    public void Taps(string text) => Pick = choices => choices.ToList().IndexOf(text) is var i and >= 0 ? i : throw new InvalidOperationException($"No line reads {text}.");

    public Task<int?> ChooseAsync(string title, IReadOnlyList<string> choices)
    {
        Asked.Add((title, choices));
        return Task.FromResult(Pick(choices));
    }
}
