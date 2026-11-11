namespace Maia.App.Controls;

/// <summary>
/// Builds its <see cref="ContentTemplate"/> only while <see cref="IsActive"/> is true, and lets it go again when it
/// turns false. MAUI creates native views even for hidden children, so the plan's check cards use this for their
/// expanded editor (date and time pickers, link field, results, actions): only the open card pays for one, however
/// many rows the timeline has inflated or recycled.
/// </summary>
public sealed class LazyView : ContentView
{
    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(LazyView), false, propertyChanged: (b, _, _) => ((LazyView)b).Update());

    public static readonly BindableProperty ContentTemplateProperty =
        BindableProperty.Create(nameof(ContentTemplate), typeof(DataTemplate), typeof(LazyView), null, propertyChanged: (b, _, _) => ((LazyView)b).Update());

    public LazyView()
    {
        IsVisible = false;
    }

    /// <summary>Whether the content is wanted now. It inherits this view's binding context.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    private void Update()
    {
        var wasVisible = IsVisible;
        if (IsActive && ContentTemplate is { } template)
        {
            Content ??= (View)template.CreateContent();
            IsVisible = true;
        }
        else
        {
            IsVisible = false;
            Content = null;
        }

        if (wasVisible != IsVisible)
        {
            RemeasureGridRow();
        }
    }

    /// <summary>
    /// In a grid of tiles (Results), Android stretches the shorter tiles of a row to its tallest and then keeps a tile's
    /// size until that tile itself changes: a row would stay tall after a tile's removal question closes. Every tile on
    /// screen is asked to measure itself again, so the row fits what it shows.
    /// </summary>
    private void RemeasureGridRow()
    {
#if ANDROID
        Element? parent = Parent;
        while (parent is not null and not CollectionView)
        {
            parent = parent.Parent;
        }

        if (parent is CollectionView { ItemsLayout: GridItemsLayout } grid
            && grid.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recycler)
        {
            for (var i = 0; i < recycler.ChildCount; i++)
            {
                recycler.GetChildAt(i)?.ForceLayout();
            }

            recycler.RequestLayout();
        }
#endif
    }
}
