using Maia.Domain.Plans;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Maia.App.Controls;

/// <summary>
/// A result's tile, per the prototype: a sea-blue gradient for photos and sage for PDF reports, each with its icon. With <see cref="ShowArt"/> (the Results screen), photos show a small scan illustration instead.
/// </summary>
public sealed class ResultTile : Border
{
    public static readonly BindableProperty KindProperty =
        BindableProperty.Create(nameof(Kind), typeof(AttachmentKind), typeof(ResultTile), AttachmentKind.Document, propertyChanged: (b, _, _) => ((ResultTile)b).Repaint());

    public static readonly BindableProperty IconSizeProperty =
        BindableProperty.Create(nameof(IconSize), typeof(double), typeof(ResultTile), 20d, propertyChanged: (b, _, _) => ((ResultTile)b).Repaint());

    public static readonly BindableProperty ShowArtProperty =
        BindableProperty.Create(nameof(ShowArt), typeof(bool), typeof(ResultTile), false, propertyChanged: (b, _, _) => ((ResultTile)b).Repaint());

    private (AttachmentKind Kind, double IconSize, bool ShowArt)? _painted;

    public ResultTile()
    {
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 12 };
    }

    public AttachmentKind Kind
    {
        get => (AttachmentKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public bool ShowArt
    {
        get => (bool)GetValue(ShowArtProperty);
        set => SetValue(ShowArtProperty, value);
    }

    /// <summary>Builds the tile once it is on screen, when its kind is known, rather than for every property XAML sets.</summary>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is not null)
        {
            Repaint();
        }
    }

    /// <summary>Builds the tile's content, only once it is shown and only when what it shows has changed.</summary>
    private void Repaint()
    {
        if (Handler is null || _painted == (Kind, IconSize, ShowArt))
        {
            return;
        }

        _painted = (Kind, IconSize, ShowArt);
        Paint();
    }

    private void Paint()
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        Geometry? Geometry(string key) => resources?.TryGetValue(key, out var value) == true ? value as Geometry : null;

        switch (Kind)
        {
            case AttachmentKind.Photo:
                Background = new LinearGradientBrush(
                    [new GradientStop(Palette.Get("Sea").WithAlpha(0.7f), 0f), new GradientStop(Palette.Get("Sea"), 1f)],
                    new Point(0, 0),
                    new Point(1, 1));
                Content = ShowArt ? ScanArt() : new Icon { Data = Geometry("IconPhoto"), Size = IconSize, Color = Palette.Get("White") };
                break;
            default:
                Background = new SolidColorBrush(Palette.Get("SageTint"));
                Content = new Icon { Data = Geometry("IconFile"), Size = IconSize, Color = Palette.Get("Pine") };
                break;
        }
    }

    /// <summary>The prototype's scan thumbnail: a dark fan with a pale oval in it.</summary>
    private static Grid ScanArt() => new()
    {
        Children =
        {
            Shape("M70 8 L22 70 H118 Z", Palette.Get("SkyDeep").WithAlpha(0.55f)),
            Shape("M54 44 A16 11 0 1 0 86 44 A16 11 0 1 0 54 44 Z", Palette.Get("Sky").WithAlpha(0.7f)),
            Shape("M57 40 A5 5 0 1 0 67 40 A5 5 0 1 0 57 40 Z", Palette.Get("Mist").WithAlpha(0.8f)),
        },
    };

    private static Path Shape(string data, Color fill) => new()
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
        Fill = fill,
        WidthRequest = 140,
        HeightRequest = 70,
        Aspect = Stretch.None,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.End,
    };
}
