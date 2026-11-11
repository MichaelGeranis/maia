#if ANDROID
using Android.Graphics.Drawables;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
#endif

namespace Maia.App.Controls;

/// <summary>
/// A Border for the cards, chips and dots of a long scrolling list (Journey's rows): it looks the same, a rounded
/// rectangle with its fill and line, but costs little to draw. Its content must stay inside its padding, as it is not
/// clipped to the shape, and it draws only a <see cref="Microsoft.Maui.Controls.Shapes.RoundRectangle"/> (or no)
/// stroke shape, in solid colours.
/// <para>
/// On Android a Border draws its shape as a general path, clips its content to that path and keeps a hardware layer
/// of its own. In a list, every card and chip coming into view was painted into an offscreen texture, and every frame of
/// a scroll redrew dozens of paths on the phone's render thread: Journey stuttered. This one is drawn as the platform's
/// own rounded rectangle, which the GPU draws in one step, with no layer and no clip.
/// </para>
/// </summary>
public sealed class LightBorder : Border
{
}

#if ANDROID
/// <summary>The Border handler, painting a platform rounded rectangle instead (see <see cref="LightBorder"/>).</summary>
public sealed class LightBorderHandler : BorderHandler
{
    private static readonly IPropertyMapper<IBorderView, IBorderHandler> LightMapper = new PropertyMapper<IBorderView, IBorderHandler>(Mapper)
    {
        [nameof(IView.Background)] = MapPaint,
        [nameof(IBorderStroke.Shape)] = MapPaint,
        [nameof(IBorderStroke.Stroke)] = MapPaint,
        [nameof(IBorderStroke.StrokeThickness)] = MapPaint,

        // A light border has no dashes, caps or joins to draw: its line is a plain rounded rectangle.
        [nameof(IBorderStroke.StrokeLineCap)] = Ignore,
        [nameof(IBorderStroke.StrokeLineJoin)] = Ignore,
        [nameof(IBorderStroke.StrokeDashPattern)] = Ignore,
        [nameof(IBorderStroke.StrokeDashOffset)] = Ignore,
        [nameof(IBorderStroke.StrokeMiterLimit)] = Ignore,
    };

    public LightBorderHandler()
        : base(LightMapper)
    {
    }

    protected override ContentViewGroup CreatePlatformView() =>
        new UnclippedViewGroup(Context) { CrossPlatformLayout = VirtualView };

    /// <summary>Fill, line and corners as one platform rounded rectangle, kept on the view and updated in place.</summary>
    private static void MapPaint(IBorderHandler handler, IBorderView border)
    {
        if (handler.PlatformView is not { } view)
        {
            return;
        }

        var density = view.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var shape = view.Background as GradientDrawable ?? new GradientDrawable();
        shape.SetShape(ShapeType.Rectangle);
        shape.SetCornerRadius((float)(CornerOf(border.Shape) * density));
        shape.SetColor(ColorOf(border.Background));
        shape.SetStroke((int)Math.Round(border.StrokeThickness * density), ColorOf(border.Stroke));
        if (!ReferenceEquals(view.Background, shape))
        {
            view.Background = shape;
        }
    }

    private static void Ignore(IBorderHandler handler, IBorderView border)
    {
    }

    private static double CornerOf(IShape? shape) => shape is RoundRectangle round ? round.CornerRadius.TopLeft : 0;

    private static Android.Graphics.Color ColorOf(Paint? paint) =>
        paint is SolidPaint { Color: { } color } ? color.ToPlatform() : Android.Graphics.Color.Transparent;

    /// <summary>Draws its content without a clip; the rounded rectangle behind it is its background.</summary>
    private sealed class UnclippedViewGroup : ContentViewGroup
    {
        public UnclippedViewGroup(Android.Content.Context context)
            : base(context)
        {
        }

        protected override Android.Graphics.Path? GetClipPath(int width, int height) => null;
    }
}
#endif
