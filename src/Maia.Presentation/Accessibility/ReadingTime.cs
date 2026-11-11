namespace Maia.Presentation.Accessibility;

/// <summary>
/// How long a message that goes away by itself (the toast) stays on screen: the design's few seconds for one short
/// sentence, and longer for a longer message, so she can read it to the end at a calm pace before it fades. A tap still
/// closes it at once, and a screen reader reads it out whole.
/// </summary>
public static class ReadingTime
{
    /// <summary>
    /// About 14 characters a second: slower than most people read (well under 180 words a minute in English, and Greek
    /// words, which run longer, get more time), so the last sentence is not gone while she is still reading.
    /// </summary>
    private static readonly TimeSpan PerCharacter = TimeSpan.FromMilliseconds(70);

    /// <summary>The design's few seconds, enough for one short sentence.</summary>
    public static TimeSpan Shortest { get; } = TimeSpan.FromSeconds(4);

    public static TimeSpan For(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var reading = PerCharacter * message.Length;
        return reading > Shortest ? reading : Shortest;
    }
}
