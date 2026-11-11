namespace Maia.Presentation.Common;

/// <summary>
/// Buttons that share a row in equal parts ("Mark done" and "Not needed") stand one under the other once any of them
/// would not fit its part on one line: at a large text size in Greek, "χρειάζεται" would otherwise break mid-word.
/// </summary>
public static class SideBySide
{
    /// <summary>Whether the buttons stand one under the other.</summary>
    /// <param name="available">The row's width.</param>
    /// <param name="spacing">The room between two buttons side by side.</param>
    /// <param name="oneLineWidths">Each button's width with its words on one line, padding included.</param>
    public static bool Stacks(double available, double spacing, IReadOnlyList<double> oneLineWidths)
    {
        ArgumentNullException.ThrowIfNull(oneLineWidths);
        if (oneLineWidths.Count < 2 || available <= 0)
        {
            return false;
        }

        var part = (available - (spacing * (oneLineWidths.Count - 1))) / oneLineWidths.Count;
        return oneLineWidths.Any(width => width > part);
    }
}
