namespace Maia.App.Controls;

/// <summary>
/// The journey card on Your week, drawn from the prototype's curve (<c>M8 50 C60 10, 100 64, 160 36 S262 8, 310 30</c>
/// in a 318×70 box): a dotted track, the travelled part in solid Sage up to "you are here", open rings at the
/// trimester boundaries (35 % and 70 % of the path) and a Sun dot for the due date. Positions are measured along
/// the path's length, as CSS offset-path does.
/// </summary>
public sealed class JourneyDrawable : IDrawable
{
    private const float BoxWidth = 318;
    private const float BoxHeight = 70;
    private static readonly PointF[] Curve = Sample();
    private static readonly float[] Lengths = Accumulate(Curve);

    /// <summary>Share of the pregnancy elapsed, 0–1.</summary>
    public double Progress { get; set; }

    /// <summary>How far the "you are here" node has walked in, 0–1 (animates on first show).</summary>
    public double Walk { get; set; } = 1;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var scale = ScaleFor(dirtyRect);
        canvas.SaveState();
        canvas.Translate(dirtyRect.Center.X - (BoxWidth * scale / 2), dirtyRect.Center.Y - (BoxHeight * scale / 2));
        canvas.Scale(scale, scale);

        var here = Here;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.StrokeSize = 6;

        // Track: round dots every 12 units.
        canvas.StrokeColor = Palette.Get("Line");
        canvas.StrokeDashPattern = [0.01f, 2];
        canvas.DrawPath(PathTo(1));
        canvas.StrokeDashPattern = null;

        // Travelled.
        canvas.StrokeColor = Palette.Get("Sage");
        canvas.DrawPath(PathTo(here));

        foreach (var boundary in new[] { 0.35f, 0.70f })
        {
            var p = At(boundary);
            canvas.FillColor = Palette.Get("White");
            canvas.FillCircle(p, 6);
            canvas.StrokeColor = Palette.Get("Sage");
            canvas.StrokeSize = 3;
            canvas.DrawCircle(p, 4.5f);
        }

        var due = At(1);
        canvas.FillColor = Palette.Get("Sun");
        canvas.FillCircle(due, 6.4f);
        canvas.StrokeColor = Palette.Get("Sun");
        canvas.StrokeSize = 1.1f;
        canvas.DrawCircle(due, 6.4f);

        // You are here: the node with its white border (its soft glow is a separate layer, see JourneyView).
        var now = At(here);
        canvas.FillColor = Palette.Get("White");
        canvas.FillCircle(now, 10);
        canvas.FillColor = Palette.Get("Sea");
        canvas.FillCircle(now, 6);

        canvas.RestoreState();
    }

    /// <summary>Where "you are here" sits, as a share of the path.</summary>
    public float Here => (float)(Math.Clamp(Progress, 0, 1) * Math.Clamp(Walk, 0, 1));

    /// <summary>The design box is scaled to fit the view, keeping its proportions.</summary>
    public static float ScaleFor(RectF rect) => Math.Min(rect.Width / BoxWidth, rect.Height / BoxHeight);

    /// <summary>"You are here" in the view's own coordinates.</summary>
    public PointF HereIn(RectF rect)
    {
        var scale = ScaleFor(rect);
        var p = At(Here);
        return new PointF(rect.Center.X - (BoxWidth * scale / 2) + (p.X * scale), rect.Center.Y - (BoxHeight * scale / 2) + (p.Y * scale));
    }

    /// <summary>The point at a share of the path's length.</summary>
    private static PointF At(float share)
    {
        var target = Math.Clamp(share, 0, 1) * Lengths[^1];
        var i = Array.FindIndex(Lengths, l => l >= target);
        if (i <= 0)
        {
            return Curve[0];
        }

        var t = (target - Lengths[i - 1]) / Math.Max(0.0001f, Lengths[i] - Lengths[i - 1]);
        return new PointF(Curve[i - 1].X + ((Curve[i].X - Curve[i - 1].X) * t), Curve[i - 1].Y + ((Curve[i].Y - Curve[i - 1].Y) * t));
    }

    private static PathF PathTo(float share)
    {
        var path = new PathF();
        path.MoveTo(Curve[0]);
        var target = share * Lengths[^1];
        for (var i = 1; i < Curve.Length && Lengths[i] <= target; i++)
        {
            path.LineTo(Curve[i]);
        }

        path.LineTo(At(share));
        return path;
    }

    /// <summary>The two cubic segments; the S command reflects the first segment's last control point.</summary>
    private static PointF[] Sample()
    {
        var points = new List<PointF>();
        Cubic(new(8, 50), new(60, 10), new(100, 64), new(160, 36), points);
        Cubic(new(160, 36), new(220, 8), new(262, 8), new(310, 30), points);
        return [.. points];
    }

    private static void Cubic(PointF p0, PointF p1, PointF p2, PointF p3, List<PointF> points)
    {
        for (var step = points.Count == 0 ? 0 : 1; step <= 60; step++)
        {
            var t = step / 60f;
            var u = 1 - t;
            points.Add(new PointF(
                (u * u * u * p0.X) + (3 * u * u * t * p1.X) + (3 * u * t * t * p2.X) + (t * t * t * p3.X),
                (u * u * u * p0.Y) + (3 * u * u * t * p1.Y) + (3 * u * t * t * p2.Y) + (t * t * t * p3.Y)));
        }
    }

    private static float[] Accumulate(PointF[] points)
    {
        var lengths = new float[points.Length];
        for (var i = 1; i < points.Length; i++)
        {
            lengths[i] = lengths[i - 1] + points[i].Distance(points[i - 1]);
        }

        return lengths;
    }
}
