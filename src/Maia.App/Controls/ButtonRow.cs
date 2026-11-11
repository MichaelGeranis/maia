using Maia.Presentation.Common;

namespace Maia.App.Controls;

/// <summary>
/// Buttons side by side in equal parts, or one under the other when one would not fit its part on one line (see
/// <see cref="SideBySide"/>). Measured again whenever the row's width changes, so a larger text size or Greek words
/// stack them, and the design's row comes back once they fit.
/// </summary>
public sealed class ButtonRow : Grid
{
    private bool _stacked;

    public ButtonRow() => SizeChanged += (_, _) => Fit();

    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);
        Lay(stacked: _stacked);
    }

    private void Fit()
    {
        if (Width <= 0)
        {
            return;
        }

        var widths = Children.OfType<View>()
            .Select(view => view.Measure(double.PositiveInfinity, double.PositiveInfinity).Width)
            .ToList();
        var stacked = SideBySide.Stacks(Width, ColumnSpacing, widths);
        if (stacked != _stacked)
        {
            Lay(stacked);
        }
    }

    private void Lay(bool stacked)
    {
        _stacked = stacked;
        var views = Children.OfType<View>().ToList();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();
        RowSpacing = ColumnSpacing;
        for (var i = 0; i < views.Count; i++)
        {
            if (stacked)
            {
                RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            else
            {
                ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            this.SetColumn(views[i], stacked ? 0 : i);
            this.SetRow(views[i], stacked ? i : 0);
        }
    }
}
