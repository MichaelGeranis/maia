using Maia.App.Services;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Maia.App.Controls;

/// <summary>
/// The misty page background (design: "Background"): a soft vertical gradient, two large blurred blobs (sky blue
/// top-left, sage lower right) drifting on 18 s and 21 s loops, and three leaves falling slowly. Still when the
/// phone asks for reduced motion, and paused while its page is hidden.
/// </summary>
public sealed class MaiaBackground : Grid
{
    private readonly Ellipse _skyBlob;
    private readonly Ellipse _sageBlob;
    private readonly (Path Leaf, double Seconds, double Offset, double Left)[] _leaves;

    public MaiaBackground()
    {
        InputTransparent = true;
        IsClippedToBounds = true;
        Background = new LinearGradientBrush(
            [
                new GradientStop(Palette.Get("Mist"), 0f),
                new GradientStop(Palette.Get("Mist"), 0.45f),
                new GradientStop(Palette.Get("Mist"), 1f),
            ],
            new Point(0, 0),
            new Point(0, 1));

        // CSS blur(60px) on a flat circle ≈ a radial gradient that fades out well before the edge.
        _skyBlob = Blob(Palette.Get("Sky"), 340, new Thickness(-150, -120, 0, 0), LayoutOptions.Start);
        _sageBlob = Blob(Palette.Get("SageTint"), 320, new Thickness(0, 360, -170, 0), LayoutOptions.End);
        _leaves =
        [
            (Leaf(Palette.Get("Sage"), 14), 22, 0, 60),
            (Leaf(Palette.Get("Sky"), 11), 26, 8, 250),
            (Leaf(Palette.Get("MutedHalf"), 9), 30, 15, 170),
        ];

        Children.Add(_skyBlob);
        Children.Add(_sageBlob);
        foreach (var (leaf, _, _, _) in _leaves)
        {
            Children.Add(leaf);
        }

        // 20 frames a second is plenty for 18 s drifts and slow leaves; nothing runs while the page is hidden.
        AmbientLoop.Attach(this, Tick);

        // Painted once into a layer of its own and again only when a blob or a leaf moves: while a list scrolls over it
        // (the drifts hold still meanwhile), each frame puts the finished picture back instead of painting three
        // screen-sized gradients under the cards again. The leaves fade without being painted offscreen first.
        Drawing.KeepInLayer(this);
        foreach (var (leaf, _, _, _) in _leaves)
        {
            Drawing.FadeInPlace(leaf);
        }
    }

    private static Ellipse Blob(Color color, double size, Thickness margin, LayoutOptions horizontal) => new()
    {
        WidthRequest = size + 120,
        HeightRequest = size + 120,
        Margin = margin,
        HorizontalOptions = horizontal,
        VerticalOptions = LayoutOptions.Start,
        Fill = new RadialGradientBrush(
            [
                new GradientStop(color.WithAlpha(0.9f), 0f),
                new GradientStop(color.WithAlpha(0.7f), 0.45f),
                new GradientStop(color.WithAlpha(0f), 1f),
            ],
            new Point(0.5, 0.5),
            0.5),
    };

    /// <summary>A leaf: a square with two opposite corners fully rounded.</summary>
    private static Path Leaf(Color color, double size) => new()
    {
        Data = new RoundRectangleGeometry(new CornerRadius(0, size, size, 0), new Rect(0, 0, size, size)),
        Fill = color,
        WidthRequest = size,
        HeightRequest = size,
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Start,
        Opacity = 0,
    };

    private void Tick(double seconds)
    {
        Drift(_skyBlob, seconds / 18, 36, 30);
        Drift(_sageBlob, seconds / 21, -30, -38);
        foreach (var (leaf, period, offset, left) in _leaves)
        {
            Fall(leaf, ((seconds + offset) / period) % 1, left);
        }
    }

    /// <summary>Ease-in-out there and back: at half the loop the blob is at (dx, dy).</summary>
    private static void Drift(VisualElement blob, double loops, double dx, double dy)
    {
        var t = (1 - Math.Cos(loops * 2 * Math.PI)) / 2;
        blob.TranslationX = t * dx;
        blob.TranslationY = t * dy;
    }

    /// <summary>The prototype's "fall" keyframes: drift right then back while falling 960 dp and turning.</summary>
    private static void Fall(VisualElement leaf, double t, double left)
    {
        var x = t < 0.5 ? t / 0.5 * 40 : 40 - ((t - 0.5) / 0.5 * 50);
        leaf.TranslationX = left + x;
        leaf.TranslationY = -60 + (t * 960);
        leaf.Rotation = t * 320;
        leaf.Opacity = t switch
        {
            < 0.1 => t / 0.1 * 0.55,
            > 0.9 => (1 - t) / 0.1 * 0.5,
            _ => 0.55 - ((t - 0.1) / 0.8 * 0.05),
        };
    }
}
