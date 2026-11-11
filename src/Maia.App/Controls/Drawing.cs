namespace Maia.App.Controls;

/// <summary>
/// Two ways to make a view cheaper to draw while a list scrolls under or over it, with no change to how it looks. They
/// act on Android; elsewhere they do nothing.
/// </summary>
public static class Drawing
{
    /// <summary>
    /// The view is painted once into a layer of its own, and again only when something in it changes; each frame in
    /// between puts that finished picture back. For things that sit still while a list scrolls past them: the page
    /// background (its drifts hold still while a list moves, see AmbientLoop) and the bottom bar.
    /// </summary>
    public static void KeepInLayer(VisualElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (view.Handler is IViewHandler handler && (handler.ContainerView ?? handler.PlatformView) is Android.Views.View platform)
            {
                platform.SetLayerType(Android.Views.LayerType.Hardware, null);
            }
#endif
        };
    }

    /// <summary>
    /// A shape drawn in one fill or one line looks the same faded draw by draw, so a fading one (a falling leaf, the
    /// "you are here" ring) need not be painted offscreen first, as Android otherwise does for every frame it is faded.
    /// </summary>
    public static void FadeInPlace(VisualElement view)
    {
        ArgumentNullException.ThrowIfNull(view);

        // Its fade is set on the view that wraps it when it has one (an input-transparent shape has), which may only
        // come once it is on screen.
        view.HandlerChanged += (_, _) => DrawFadedInPlace(view);
        view.Loaded += (_, _) => DrawFadedInPlace(view);
    }

    private static void DrawFadedInPlace(VisualElement view)
    {
#if ANDROID
        if (view.Handler is IViewHandler handler)
        {
            (handler.PlatformView as Android.Views.View)?.ForceHasOverlappingRendering(false);
            (handler.ContainerView as Android.Views.View)?.ForceHasOverlappingRendering(false);
        }
#endif
    }
}
