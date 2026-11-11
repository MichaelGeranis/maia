using Maia.App.Services;

namespace Maia.App.Controls;

/// <summary>
/// The journey curve: "you are here" walks in once (2 s), then glows softly (2.4 s loop). The curve is only redrawn
/// while the node walks; the glow is a separate ring that is scaled and faded, which costs next to nothing.
/// </summary>
public sealed class JourneyView : Grid
{
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(JourneyView), 0d, propertyChanged: (b, _, n) => ((JourneyView)b).OnProgress((double)n));

    private const double WalkSeconds = 2.3;
    private readonly JourneyDrawable _drawable = new();
    private readonly GraphicsView _canvas;
    private readonly Microsoft.Maui.Controls.Shapes.Ellipse _glow;

    public JourneyView()
    {
        HeightRequest = 70;
        _canvas = new GraphicsView { Drawable = _drawable };
        _glow = new Microsoft.Maui.Controls.Shapes.Ellipse
        {
            Fill = Palette.Get("Sea"),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true,
            Opacity = 0,
        };

        // The glow sits under the curve, so the node's white border draws over it.
        Children.Add(_glow);
        Children.Add(_canvas);
        SizeChanged += (_, _) => PlaceGlow();

        // With reduced motion the loop never runs and the curve stays as drawn: walked in, no glow.
        AmbientLoop.Attach(this, Tick);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    private void OnProgress(double progress)
    {
        _drawable.Progress = progress;
        _canvas.Invalidate();
        PlaceGlow();
    }

    /// <summary>A 40-unit circle (in the design box) centred on "you are here".</summary>
    private void PlaceGlow()
    {
        if (Width <= 0)
        {
            return;
        }

        var rect = new RectF(0, 0, (float)Width, (float)Height);
        var size = 40 * JourneyDrawable.ScaleFor(rect);
        var here = _drawable.HereIn(rect);
        _glow.WidthRequest = size;
        _glow.HeightRequest = size;
        _glow.TranslationX = here.X - (size / 2);
        _glow.TranslationY = here.Y - (size / 2);
    }

    private void Tick(double seconds)
    {
        if (seconds < WalkSeconds + 0.1)
        {
            var walk = Math.Clamp((seconds - 0.3) / 2, 0, 1);
            _drawable.Walk = 1 - Math.Pow(1 - walk, 3);
            _canvas.Invalidate();
            PlaceGlow();
        }

        // Radius 10 → 20 (scale 0.5 → 1) while fading from 45 %.
        var glow = seconds % 2.4 / 2.4;
        _glow.Scale = 0.5 + (glow * 0.5);
        _glow.Opacity = 0.45 * (1 - glow);
    }
}

/// <summary>
/// The next-up card's waves: green behind, sea blue in front, sliding in opposite directions (9 s and 14 s loops).
/// Each layer is drawn once, one period wider than the card, and only moved: the phone slides a finished picture,
/// which costs next to nothing, instead of redrawing curves twenty times a second.
/// </summary>
public sealed class WaveView : Grid
{
    private readonly GraphicsView _back = Layer(new WaveDrawable(Palette.Get("Sage").WithAlpha(0.8f), middle: 22, crest: 6, phase: 0));
    private readonly GraphicsView _front = Layer(new WaveDrawable(Palette.Get("Sea").WithAlpha(0.75f), middle: 30, crest: 16, phase: WaveDrawable.Period / 2));

    public WaveView()
    {
        HeightRequest = 48;
        InputTransparent = true;
        IsClippedToBounds = true;
        Children.Add(_back);
        Children.Add(_front);
        SizeChanged += (_, _) =>
        {
            _back.WidthRequest = Width + WaveDrawable.Period;
            _front.WidthRequest = Width + WaveDrawable.Period;
        };
        AmbientLoop.Attach(this, Tick);
    }

    private static GraphicsView Layer(WaveDrawable drawable) => new()
    {
        Drawable = drawable,
        HorizontalOptions = LayoutOptions.Start,
        InputTransparent = true,
    };

    private void Tick(double seconds)
    {
        // The back wave drifts left, the front one right; a whole period later each looks the same again.
        _back.TranslationX = -(seconds / 9 % 1 * WaveDrawable.Period);
        _front.TranslationX = -WaveDrawable.Period + (seconds / 14 % 1 * WaveDrawable.Period);
    }
}

/// <summary>
/// A dandelion seed that drifts with Maia's breathing rhythm: it rises for 4 s and settles for 6 s, swaying a little as it
/// goes. Still with reduced motion.
/// </summary>
public sealed class DriftingSeed : ContentView
{
    public DriftingSeed()
    {
        Content = new Icon
        {
            Data = (Microsoft.Maui.Controls.Shapes.Geometry)Microsoft.Maui.Controls.Application.Current!.Resources["IconDandelionSeed"],
            Size = 24,
            Color = Palette.Get("Sea"),
        };
        InputTransparent = true;
        AmbientLoop.Attach(this, Tick);
    }

    private void Tick(double seconds)
    {
        var phase = seconds % 10;
        var lift = phase < 4 ? SinInOut(phase / 4) : 1 - SinInOut((phase - 4) / 6);
        TranslationY = 2 - (4 * lift);
        Rotation = 6 * Math.Sin(seconds * Math.PI / 5);
    }

    private static double SinInOut(double t) => (1 - Math.Cos(Math.PI * t)) / 2;
}

/// <summary>"You are here" on the plan: a soft Tertiary ring that swells and fades from the dot (2.4 s loop).</summary>
public sealed class PulseRing : ContentView
{
    private readonly Microsoft.Maui.Controls.Shapes.Ellipse _ring;

    public PulseRing()
    {
        _ring = new Microsoft.Maui.Controls.Shapes.Ellipse { Stroke = Palette.Get("Sea"), StrokeThickness = 2, Opacity = 0 };
        Content = _ring;
        InputTransparent = true;
        Drawing.FadeInPlace(_ring);
        AmbientLoop.Attach(this, Tick);
    }

    private void Tick(double seconds)
    {
        var t = Motions.Soft.Ease(seconds % 2.4 / 2.4);
        _ring.Scale = 0.6 + (t * 0.9);
        _ring.Opacity = 0.55 * (1 - t);
    }
}
