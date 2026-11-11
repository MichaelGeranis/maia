using Maia.App.Services;

namespace Maia.App.Controls;

/// <summary>
/// Holds a photo or a page to read closely: pinch to zoom (up to 5×), drag to move around while zoomed, double-tap
/// to zoom in on a spot or back out. At 1× the content fits the view. Double-tap jumps without animating when the
/// phone asks for reduced motion.
/// </summary>
public sealed class ZoomView : ContentView
{
    private const double MaxScale = 5;
    private const double DoubleTapScale = 2.5;

    private double _scale = 1;
    private double _x;
    private double _y;
    private double _panStartX;
    private double _panStartY;
    private bool _pinching;

    public ZoomView()
    {
        IsClippedToBounds = true;
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinch;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPan;
        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnDoubleTap;
        GestureRecognizers.Add(pinch);
        GestureRecognizers.Add(pan);
        GestureRecognizers.Add(doubleTap);
    }

    /// <summary>Back to the whole picture, as when new content is shown.</summary>
    public void Reset()
    {
        _scale = 1;
        _x = 0;
        _y = 0;
        Apply();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Content) && Content is not null)
        {
            Content.AnchorX = 0;
            Content.AnchorY = 0;
            Reset();
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        Clamp();
        Apply();
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _pinching = true;
                break;
            case GestureStatus.Running:
                ZoomAround(_scale * e.Scale, e.ScaleOrigin.X * Width, e.ScaleOrigin.Y * Height);
                break;
            default:
                _pinching = false;
                break;
        }
    }

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        if (_pinching || _scale <= 1)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartX = _x;
                _panStartY = _y;
                break;
            case GestureStatus.Running:
                _x = _panStartX + e.TotalX;
                _y = _panStartY + e.TotalY;
                Clamp();
                Apply();
                break;
            default:
                break;
        }
    }

    private void OnDoubleTap(object? sender, TappedEventArgs e)
    {
        var (fromScale, fromX, fromY) = (_scale, _x, _y);
        if (_scale > 1)
        {
            Reset();
        }
        else
        {
            var point = e.GetPosition(this) ?? new Point(Width / 2, Height / 2);
            ZoomAround(DoubleTapScale, point.X, point.Y);
        }

        if (Motion.IsReduced || Content is not { } content)
        {
            return;
        }

        // Ease from where it was to where it now is.
        var (toScale, toX, toY) = (_scale, _x, _y);
        content.AbortAnimation("maia-zoom");
        new Animation(t =>
        {
            content.Scale = fromScale + ((toScale - fromScale) * t);
            content.TranslationX = fromX + ((toX - fromX) * t);
            content.TranslationY = fromY + ((toY - fromY) * t);
        }).Commit(content, "maia-zoom", length: 220, easing: Motions.Soft);
    }

    /// <summary>Zooms to <paramref name="scale"/> keeping the point under (<paramref name="x"/>, <paramref name="y"/>) where it is.</summary>
    private void ZoomAround(double scale, double x, double y)
    {
        var next = Math.Clamp(scale, 1, MaxScale);
        _x = x - ((x - _x) * next / _scale);
        _y = y - ((y - _y) * next / _scale);
        _scale = next;
        Clamp();
        Apply();
    }

    /// <summary>Keeps the zoomed content covering the view: no empty edges while zoomed in.</summary>
    private void Clamp()
    {
        if (Content is not { } content || content.Width <= 0)
        {
            return;
        }

        _x = Math.Clamp(_x, Math.Min(0, Width - (content.Width * _scale)), 0);
        _y = Math.Clamp(_y, Math.Min(0, Height - (content.Height * _scale)), 0);
        if (_scale <= 1)
        {
            _x = 0;
            _y = 0;
        }
    }

    private void Apply()
    {
        if (Content is { } content)
        {
            content.AbortAnimation("maia-zoom");
            content.Scale = _scale;
            content.TranslationX = _x;
            content.TranslationY = _y;
        }
    }
}

/// <summary>A PDF page: as wide as its place, as tall as its shape, so the list keeps its height while pages draw.</summary>
public sealed class PageImage : Image
{
    public static readonly BindableProperty RatioProperty =
        BindableProperty.Create(nameof(Ratio), typeof(double), typeof(PageImage), Math.Sqrt(2), propertyChanged: (b, _, _) => ((PageImage)b).InvalidateMeasure());

    public PageImage()
    {
        Aspect = Aspect.AspectFit;
    }

    /// <summary>Height divided by width.</summary>
    public double Ratio
    {
        get => (double)GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var width = double.IsFinite(widthConstraint) ? widthConstraint : 320;
        return new Size(width, width * Ratio);
    }
}
