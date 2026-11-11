using System.Runtime.CompilerServices;
using Maia.App.Services;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Maia.App.Controls;

/// <summary>
/// A small, calm celebration: when she ticks something off (a task, a question, a check done), a handful of leaves in
/// the palette's greens, sky and sun lift out of the tick and drift down, fading, in under a second. It lives in
/// the page templates, above the content and below the toast, and never takes a touch.
/// </summary>
public sealed class CelebrationLayer : AbsoluteLayout
{
    private static readonly ConditionalWeakTable<Page, CelebrationLayer> Layers = [];
    private static readonly string[] Colours = ["Sage", "Sky", "Sea", "Sun", "SageTint", "Sun"];

    public CelebrationLayer()
    {
        InputTransparent = true;
        CascadeInputTransparent = true;
        Loaded += (_, _) =>
        {
            if (Motions.FindPage(this) is { } page)
            {
                Layers.AddOrUpdate(page, this);
            }
        };
    }

    /// <summary>Lifts leaves out of <paramref name="source"/>, on the page it is on.</summary>
    public static void Burst(VisualElement source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Motion.IsReduced || Motions.FindPage(source) is not { } page || !Layers.TryGetValue(page, out var layer))
        {
            return;
        }

        layer.BurstFrom(layer.CentreOf(source));
    }

    private void BurstFrom(Point centre)
    {
        const int Count = 9;
        for (var i = 0; i < Count; i++)
        {
            var size = Random.Shared.Next(7, 12);
            var leaf = new Path
            {
                Data = new RoundRectangleGeometry(new CornerRadius(0, size, size, 0), new Rect(0, 0, size, size)),
                Fill = Palette.Get(Colours[i % Colours.Length]),
                Opacity = 0,
            };
            AbsoluteLayout.SetLayoutBounds(leaf, new Rect(centre.X - (size / 2.0), centre.Y - (size / 2.0), size, size));
            Children.Add(leaf);

            // Fan out upwards (−160° … −20°), then fall a little as they fade, turning as they go.
            var angle = (-160 + (140.0 * i / (Count - 1)) + Random.Shared.Next(-8, 9)) * Math.PI / 180;
            var distance = Random.Shared.Next(34, 64);
            var dx = Math.Cos(angle) * distance;
            var dy = Math.Sin(angle) * distance;
            var spin = Random.Shared.Next(-220, 221);
            var length = (uint)Random.Shared.Next(760, 980);
            new Animation(t =>
            {
                var rise = Motions.Soft.Ease(Math.Min(t / 0.55, 1));
                leaf.TranslationX = dx * rise;
                leaf.TranslationY = (dy * rise) + (28 * t * t);
                leaf.Rotation = spin * t;
                leaf.Opacity = t < 0.15 ? t / 0.15 * 0.9 : 0.9 * (1 - ((t - 0.15) / 0.85));
            }).Commit(leaf, "maia-leaf", length: length, easing: Easing.Linear, finished: (_, _) => Children.Remove(leaf));
        }
    }

    /// <summary>The centre of <paramref name="source"/> in this layer's coordinates.</summary>
    private Point CentreOf(VisualElement source)
    {
#if ANDROID
        if (source.Handler?.PlatformView is Android.Views.View from && Handler?.PlatformView is Android.Views.View to)
        {
            var a = new int[2];
            var b = new int[2];
            from.GetLocationInWindow(a);
            to.GetLocationInWindow(b);
            var density = from.Resources?.DisplayMetrics?.Density ?? 1f;
            return new Point(((a[0] - b[0]) / density) + (source.Width / 2), ((a[1] - b[1]) / density) + (source.Height / 2));
        }
#endif
        return new Point(Width / 2, Height / 3);
    }
}

/// <summary>
/// <c>controls:Celebrate.When="{Binding IsDone}"</c>: bursts leaves from the element when the value turns true
/// because of something she did (not when a screen first loads already ticked).
/// </summary>
public static class Celebrate
{
    public static readonly BindableProperty WhenProperty =
        BindableProperty.CreateAttached("When", typeof(bool?), typeof(Celebrate), null, propertyChanged: OnWhenChanged);

    public static bool? GetWhen(BindableObject view) => (bool?)view.GetValue(WhenProperty);

    public static void SetWhen(BindableObject view, bool? value) => view.SetValue(WhenProperty, value);

    private static readonly ConditionalWeakTable<BindableObject, object> LastContext = [];

    private static void Remember(BindableObject bindable)
    {
        if (bindable.BindingContext is { } context)
        {
            LastContext.AddOrUpdate(bindable, context);
        }
    }

    private static void OnWhenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // A list row reused for another item also flips the value; only the same item turning true is her doing.
        var sameItem = LastContext.TryGetValue(bindable, out var last) && ReferenceEquals(last, bindable.BindingContext);
        Remember(bindable);
        if (oldValue is null)
        {
            // The first value (the property starts empty, so this always comes): from now on, follow the item.
            bindable.BindingContextChanged += (_, _) => Remember(bindable);
        }

        if (bindable is VisualElement { IsLoaded: true } view && sameItem && oldValue is false && newValue is true)
        {
            CelebrationLayer.Burst(view);
        }
    }
}
