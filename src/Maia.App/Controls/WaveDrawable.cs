namespace Maia.App.Controls;

/// <summary>
/// One wave along the bottom of the next-up card: the prototype's quadratic curve with a 350 dp period, drawn once
/// across the whole view. <see cref="WaveView"/> stacks two and slides them, so the waves move without redrawing.
/// </summary>
public sealed class WaveDrawable(Color color, float middle, float crest, float phase) : IDrawable
{
    public const float Period = 350;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var scale = dirtyRect.Height / 48f;
        var y = dirtyRect.Top + (middle * scale);
        var reach = (middle - crest) * scale;
        var start = dirtyRect.Left - phase;

        // M0 m Q87.5 c 175 m T350 m …: alternating crests and troughs.
        var path = new PathF();
        path.MoveTo(start, y);
        var up = true;
        for (var x = start; x < dirtyRect.Right; x += Period / 2)
        {
            path.QuadTo(x + (Period / 4), up ? y - reach : y + reach, x + (Period / 2), y);
            up = !up;
        }

        path.LineTo(dirtyRect.Right + Period, dirtyRect.Bottom);
        path.LineTo(start, dirtyRect.Bottom);
        path.Close();
        canvas.FillColor = color;
        canvas.FillPath(path);
    }
}
