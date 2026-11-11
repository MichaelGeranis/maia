namespace Maia.App.Controls;

/// <summary>
/// Maia's logo: the sprout of the app icon (<c>Resources/AppIcon/appiconfg.svg</c>), a stem and two filled leaves, in
/// its colouring for a light background: the stem and right leaf Pine, the left leaf Sage (docs/design/README.md, Icons
/// and the logo). One drawing on one view, in its 24-unit box like the icons, scaled to <see cref="Size"/>.
/// </summary>
public sealed class Logo : GraphicsView
{
    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(Logo), 24d, propertyChanged: (b, _, n) => ((Logo)b).Resize((double)n));

    public Logo()
    {
        Drawable = new SproutDrawable();
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        Resize(Size);
    }

    /// <summary>Rendered size in dp; the design box is 24.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private void Resize(double size)
    {
        WidthRequest = size;
        HeightRequest = size;
        Invalidate();
    }

    private sealed class SproutDrawable : IDrawable
    {
        // The app icon's 456 box cropped to 114,110 228×228, divided by 9.5 to the 24 box.
        private static readonly PathF LeftLeaf = PathBuilder.Build("M12 13.474C6.526 13.263 4 9.474 4.211 5.053C9.053 5.263 12.211 8.421 12 13.474Z");
        private static readonly PathF RightLeaf = PathBuilder.Build("M12 10.947C12.421 5.684 15.579 2.526 20.632 2.316C20.842 7.158 17.895 10.947 12 10.947Z");
        private const float StemTop = 10.526f;
        private const float StemBottom = 23.158f;
        private const float StemWidth = 1.474f;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var scale = Math.Min(dirtyRect.Width, dirtyRect.Height) / 24f;
            canvas.SaveState();
            canvas.Scale(scale, scale);

            canvas.StrokeColor = Palette.Get("Pine");
            canvas.StrokeSize = StemWidth;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(12, StemTop, 12, StemBottom);

            canvas.FillColor = Palette.Get("Sage");
            canvas.FillPath(LeftLeaf);
            canvas.FillColor = Palette.Get("Pine");
            canvas.FillPath(RightLeaf);

            canvas.RestoreState();
        }
    }
}
