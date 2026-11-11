namespace Maia.Presentation.Navigation;

/// <summary>
/// The size of the bottom bar's five names. They share one size, so the bar reads as one row: the design's size, unless
/// two neighbouring names would come closer than <see cref="Gap"/> or a name would run past what its slot can show.
/// Then all five shrink just enough to fit, but they don't look smaller than <see cref="MinimumSize"/>, whatever her
/// phone's text size: at that size two names may come closer, down to <see cref="MinimumGap"/>. On a phone too narrow
/// even for that, the names shrink further rather than touch or be cut off. In English the names keep the design's
/// size on any phone; the longer Greek ones ("Αποτελέσματα") come out at about 9 to 10, and down to about 8 on the
/// narrowest phones (320 dp).
/// </summary>
public static class TabLabelFit
{
    /// <summary>The names' size in the design, before her phone's text size is applied.</summary>
    public const double DesignSize = 12;

    /// <summary>
    /// The smallest a name looks while the names fit, with her phone's text size applied. Once the names have shrunk to
    /// this, a larger text setting doesn't make them bigger: there is no room for it.
    /// </summary>
    public const double MinimumSize = 9;

    /// <summary>
    /// The largest a name looks, with her phone's text size applied: the names grow with her text size up to 130%, and
    /// past that stay this size, so at the largest text sizes the five still stand apart rather than run together.
    /// </summary>
    public const double MaximumSize = DesignSize * 1.3;

    /// <summary>The room kept between two neighbouring names, in dp.</summary>
    public const double Gap = 4;

    /// <summary>The least room between two names, in dp, once they are as small as <see cref="MinimumSize"/>: any closer and they run together.</summary>
    public const double MinimumGap = 2;

    /// <summary>
    /// The names' size for slots <paramref name="slotWidth"/> wide, as a label's FontSize: her phone's text size is
    /// applied on top of it.
    /// </summary>
    /// <param name="slotWidth">One slot's width: the bar's width less its side padding, shared by the slots.</param>
    /// <param name="edge">The bar's side padding, which the first and last names may use.</param>
    /// <param name="overhang">How far a name may reach past its own slot on each side (the label's negative margin).</param>
    /// <param name="widths">Each name's width at <see cref="DesignSize"/> with her phone's text size, in bar order.</param>
    /// <param name="textScale">
    /// Her phone's text size: how much wider a name shows with it than without (1 by default, 1.3 at 130%).
    /// </param>
    public static double Size(double slotWidth, double edge, double overhang, IReadOnlyList<double> widths, double textScale)
    {
        ArgumentNullException.ThrowIfNull(widths);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textScale);
        if (slotWidth <= Gap || widths.Count == 0)
        {
            return DesignSize;
        }

        var roomy = Math.Min(DesignSize * Scale(slotWidth, edge, overhang, widths, Gap), MaximumSize / textScale);
        var floor = MinimumSize / textScale;
        if (roomy >= floor)
        {
            return roomy;
        }

        // Too long for the full gap at the floor: the names come closer, but no closer than they must, and on a phone
        // too narrow for the floor they shrink below it rather than touch.
        return Math.Min(floor, DesignSize * Scale(slotWidth, edge, overhang, widths, MinimumGap));
    }

    /// <summary>How far the names shrink from <see cref="DesignSize"/> so neighbours keep <paramref name="gap"/> apart and each shows whole.</summary>
    private static double Scale(double slotWidth, double edge, double overhang, IReadOnlyList<double> widths, double gap)
    {
        var scale = 1.0;
        for (var i = 0; i < widths.Count; i++)
        {
            // Never wider than what its own slot shows; the first and last may also use the bar's padding, but keep a
            // whole gap from the screen's edge.
            var reach = i == 0 || i == widths.Count - 1 ? Math.Min(overhang, edge - Gap) : overhang;
            scale = Shrink(scale, slotWidth + (2 * Math.Max(reach, 0)), widths[i]);

            // Two neighbours' halves and the gap between them fit in one slot's width (centre to centre).
            if (i + 1 < widths.Count)
            {
                scale = Shrink(scale, slotWidth - gap, (widths[i] + widths[i + 1]) / 2);
            }
        }

        return scale;
    }

    private static double Shrink(double scale, double room, double wanted) =>
        wanted > 0 && wanted * scale > room ? room / wanted : scale;
}
