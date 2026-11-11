using System.Globalization;
using Maia.Presentation.Accessibility;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Tests;

/// <summary>How long a toast stays: the design's few seconds, and long enough to read to the end in either language.</summary>
public sealed class ReadingTimeTests
{
    /// <summary>A calm reading pace, slower than most people read.</summary>
    private const double WordsAMinute = 180;

    [Fact]
    public void A_short_confirmation_stays_the_designs_few_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), ReadingTime.Shortest);
        Assert.Equal(ReadingTime.Shortest, ReadingTime.For(Text.In("Toast_Deleted", Text.English)));
        Assert.Equal(ReadingTime.Shortest, ReadingTime.For(Text.In("Toast_Deleted", Text.Greek)));
    }

    [Fact]
    public void Every_toast_stays_until_it_can_be_read_to_the_end_at_a_calm_pace_in_both_languages()
    {
        var toasts = Text.KeysFor(CultureInfo.InvariantCulture).Where(key => key.StartsWith("Toast_", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(toasts);
        Assert.All(toasts.SelectMany(key => new[] { Text.In(key, Text.English), Text.In(key, Text.Greek) }), message =>
        {
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var reading = TimeSpan.FromMinutes(words / WordsAMinute);
            Assert.True(ReadingTime.For(message) >= reading, $"'{message}' goes before it can be read ({reading.TotalSeconds:0.0} s)");
        });
    }
}
