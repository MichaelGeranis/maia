namespace Maia.Presentation.Calm;

/// <summary>
/// The Timer's rhythm: a first part and a second, in whole seconds, done a number of times. She sets each within the
/// limits, for calm breathing or the exercises her doctor suggested; Maia suggests nothing about them beyond the default.
/// </summary>
public sealed record Rhythm(int FirstSeconds, int SecondSeconds, int Rounds)
{
    public const int MinSeconds = 1;
    public const int MaxSeconds = 30;
    public const int MinRounds = 1;
    public const int MaxRounds = 50;

    /// <summary>4 seconds, then 6, twelve times: two calm minutes, until she sets her own.</summary>
    public static readonly Rhythm Default = new(4, 6, 12);

    public TimeSpan First => TimeSpan.FromSeconds(FirstSeconds);

    public TimeSpan Second => TimeSpan.FromSeconds(SecondSeconds);

    /// <summary>The whole session: both parts, every round.</summary>
    public TimeSpan Length => (First + Second) * Rounds;

    /// <summary>The same rhythm kept within the limits, for values read back from the phone.</summary>
    public Rhythm Within() => new(
        Math.Clamp(FirstSeconds, MinSeconds, MaxSeconds),
        Math.Clamp(SecondSeconds, MinSeconds, MaxSeconds),
        Math.Clamp(Rounds, MinRounds, MaxRounds));
}
