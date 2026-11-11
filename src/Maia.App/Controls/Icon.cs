using System.Numerics;
using Microsoft.Maui.Controls.Shapes;

namespace Maia.App.Controls;

/// <summary>
/// A line icon from <c>Icons.xaml</c>, drawn in its 24×24 design box and scaled to <see cref="Size"/>, so every
/// icon keeps the prototype's proportions and padding (a plain Path would stretch to its own bounds).
/// <para>
/// The icon is itself the shape: one platform view, which draws and lets touches through to whatever holds it. (It
/// was a content view around a path, made input-transparent: four platform views an icon, which made long lists such as
/// Journey slow to scroll.)
/// </para>
/// </summary>
public sealed class Icon : Shape
{
    public static readonly BindableProperty DataProperty =
        BindableProperty.Create(nameof(Data), typeof(Geometry), typeof(Icon), propertyChanged: (b, _, _) => ((Icon)b).Redraw());

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(Icon), 20d, propertyChanged: (b, _, n) => ((Icon)b).Resize((double)n));

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(Icon), null, propertyChanged: (b, _, n) => ((Icon)b).Stroke = new SolidColorBrush(n as Color ?? Colors.Transparent));

    /// <summary>The 24-unit design box.</summary>
    private const double DesignBox = 24;

    public Icon()
    {
        Aspect = Stretch.None;
        StrokeLineCap = PenLineCap.Round;
        StrokeLineJoin = PenLineJoin.Round;
        StrokeThickness = 1.8;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        Resize(Size);
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Rendered size in dp; the design box is 24.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public Color? Color
    {
        get => (Color?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>The geometry, scaled from its 24 box to <see cref="Size"/> (the line keeps its own width).</summary>
    public override PathF GetPath()
    {
        var path = new PathF();
        Data?.AppendPath(path);
        path.Transform(Matrix3x2.CreateScale((float)(Size / DesignBox)));
        return path;
    }

    /// <summary>The layout box matches the drawing exactly.</summary>
    private void Resize(double size)
    {
        WidthRequest = size;
        HeightRequest = size;
        Redraw();
    }

    private void Redraw() => Handler?.UpdateValue(nameof(IShapeView.Shape));
}
