using Maia.App.Services;

namespace Maia.App.Controls;

/// <summary>
/// The design's motion (docs/design/README.md, "Motion"), as behaviours a page switches on in XAML:
/// <c>controls:Motions.Rise="True"</c> on a stack of cards, <c>Pop</c> on list items, <c>Press</c> on anything tappable.
/// Every one is short, runs once per trigger, and does nothing when the phone asks for reduced motion.
/// </summary>
public static class Motions
{
    /// <summary>The design's ease-out, cubic-bezier(.2,.7,.2,1).</summary>
    public static readonly Easing Soft = new(t => 1 - Math.Pow(1 - t, 3.2));

    /// <summary>
    /// Children rise 16 dp and fade in, 50 ms apart, as the screen first shows. Coming back to it (a tab switch, a
    /// pushed screen closing) shows it as it was, at once, rather than fading every card in again.
    /// </summary>
    public static readonly BindableProperty RiseProperty =
        BindableProperty.CreateAttached("Rise", typeof(bool), typeof(Motions), false, propertyChanged: OnRiseChanged);

    /// <summary>The element pops in from 0.9 when it first shows (new tasks, questions, results).</summary>
    public static readonly BindableProperty PopProperty =
        BindableProperty.CreateAttached("Pop", typeof(bool), typeof(Motions), false, propertyChanged: OnPopChanged);

    /// <summary>The element pops each time it becomes visible (the tick in a checkbox).</summary>
    public static readonly BindableProperty PopWhenShownProperty =
        BindableProperty.CreateAttached("PopWhenShown", typeof(bool), typeof(Motions), false, propertyChanged: OnPopWhenShownChanged);

    /// <summary>The element dips to 0.96 while pressed and springs back.</summary>
    public static readonly BindableProperty PressProperty =
        BindableProperty.CreateAttached("Press", typeof(bool), typeof(Motions), false, propertyChanged: OnPressChanged);

    /// <summary>
    /// Bound to "just added": the element is tinted SageTint and fades back to clear, once, as it shows (a task she has
    /// just added to Your week's card).
    /// </summary>
    public static readonly BindableProperty GlowProperty =
        BindableProperty.CreateAttached("Glow", typeof(bool), typeof(Motions), false, propertyChanged: OnGlowChanged);

    private static readonly BindableProperty GlowWiredProperty =
        BindableProperty.CreateAttached("GlowWired", typeof(bool), typeof(Motions), false);

    private static readonly BindableProperty GlowPlayedProperty =
        BindableProperty.CreateAttached("GlowPlayed", typeof(bool), typeof(Motions), false);

    public static bool GetRise(BindableObject view) => (bool)view.GetValue(RiseProperty);

    public static void SetRise(BindableObject view, bool value) => view.SetValue(RiseProperty, value);

    public static bool GetPop(BindableObject view) => (bool)view.GetValue(PopProperty);

    public static void SetPop(BindableObject view, bool value) => view.SetValue(PopProperty, value);

    public static bool GetPopWhenShown(BindableObject view) => (bool)view.GetValue(PopWhenShownProperty);

    public static void SetPopWhenShown(BindableObject view, bool value) => view.SetValue(PopWhenShownProperty, value);

    public static bool GetPress(BindableObject view) => (bool)view.GetValue(PressProperty);

    public static void SetPress(BindableObject view, bool value) => view.SetValue(PressProperty, value);

    public static bool GetGlow(BindableObject view) => (bool)view.GetValue(GlowProperty);

    public static void SetGlow(BindableObject view, bool value) => view.SetValue(GlowProperty, value);

    /// <summary>Plays the pop: 0.9 → 1.02 → 1 while fading in, 0.5 s.</summary>
    public static void PlayPop(VisualElement view, uint delay = 0)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (Motion.IsReduced)
        {
            return;
        }

        view.AbortAnimation("maia-pop");
        view.Opacity = 0;
        view.Scale = 0.9;
        Layer(view, true);
        var total = 500 + delay;
        var start = delay / (double)total;
        new Animation
        {
            { start, start + ((1 - start) * 0.7), new Animation(v => view.Scale = v, 0.9, 1.02, Soft) },
            { start + ((1 - start) * 0.7), 1, new Animation(v => view.Scale = v, 1.02, 1, Easing.SinOut) },
            { start, start + ((1 - start) * 0.6), new Animation(v => view.Opacity = v, 0, 1) },
        }.Commit(view, "maia-pop", length: total, finished: (_, _) =>
        {
            view.Scale = 1;
            view.Opacity = 1;
            Layer(view, false);
        });
    }

    private static void OnRiseChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Layout layout || newValue is not true)
        {
            return;
        }

        // Once per screen: Loaded fires again each time its page comes back (a tab switch, a pushed screen closing). A
        // layout hidden until its content is ready (Your week's cards, after the first load) rises as it first shows.
        var played = false;
        var loaded = false;
        void Play()
        {
            if (!played && loaded && layout.IsVisible)
            {
                played = true;
                Rise(layout);
            }
        }

        layout.Loaded += (_, _) =>
        {
            loaded = true;
            Play();
        };
        layout.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VisualElement.IsVisible))
            {
                Play();
            }
        };
    }

    private static void OnGlowChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement view)
        {
            return;
        }

        if (!(bool)view.GetValue(GlowWiredProperty))
        {
            // A row added with the glow already on plays it as it arrives on screen.
            view.SetValue(GlowWiredProperty, true);
            view.Loaded += (_, _) => GlowOnce(view);
        }

        if (newValue is true)
        {
            view.SetValue(GlowPlayedProperty, false);
            if (view.IsLoaded)
            {
                GlowOnce(view);
            }
        }
    }

    /// <summary>
    /// SageTint fading to clear over 1.6 s, once for each time the glow is turned on (not again when the page comes
    /// back). With reduced motion there is no glow: the row simply shows.
    /// </summary>
    private static void GlowOnce(VisualElement view)
    {
        if (!GetGlow(view) || (bool)view.GetValue(GlowPlayedProperty))
        {
            return;
        }

        view.SetValue(GlowPlayedProperty, true);
        if (Motion.IsReduced)
        {
            return;
        }

        var tint = Palette.Get("SageTint");
        view.AbortAnimation("maia-glow");
        new Animation(t => view.BackgroundColor = tint.WithAlpha((float)(1 - t)), 0, 1, Easing.CubicIn)
            .Commit(view, "maia-glow", length: 1600, finished: (_, _) => view.BackgroundColor = Colors.Transparent);
    }

    private static void Rise(Layout layout)
    {
        if (Motion.IsReduced)
        {
            return;
        }

        var index = 0;
        foreach (var child in layout.Children.OfType<VisualElement>().Where(c => c.IsVisible))
        {
            var delay = Math.Min(index++, 8) * 50;
            var total = (uint)(600 + delay);
            var start = delay / (double)total;
            child.AbortAnimation("maia-rise");
            child.Opacity = 0;
            child.TranslationY = 16;
            Layer(child, true);
            new Animation
            {
                { start, 1, new Animation(v => child.TranslationY = v, 16, 0, Soft) },
                { start, 1, new Animation(v => child.Opacity = v, 0, 1, Easing.CubicOut) },
            }.Commit(child, "maia-rise", length: total, finished: (_, _) =>
            {
                child.TranslationY = 0;
                child.Opacity = 1;
                Layer(child, false);
            });
        }
    }

    private static void OnPopChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is VisualElement view && newValue is true)
        {
            // Once per element: Loaded fires again each time its page comes back (a tab switch, a pushed screen
            // closing), and an item kept in place should not pop in again then.
            var played = false;
            view.Loaded += (_, _) =>
            {
                // Turned off before it showed (a row moved within its list, not a new one): no pop.
                if (!played && GetPop(view))
                {
                    played = true;
                    PlayPop(view, SiblingDelay(view));
                }
            };
        }
    }

    /// <summary>List items pop one after another (60 ms apart, at most eight steps).</summary>
    private static uint SiblingDelay(VisualElement view) =>
        view.Parent is Layout parent ? (uint)(Math.Min(parent.Children.IndexOf(view), 8) * 60) : 0;

    private static void OnPopWhenShownChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement view || newValue is not true)
        {
            return;
        }

        var loaded = false;
        object? shownFor = null;
        view.Loaded += (_, _) => loaded = true;
        view.BindingContextChanged += (_, _) => shownFor = view.BindingContext;
        view.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(VisualElement.IsVisible))
            {
                return;
            }

            // Only a change she made: not the first layout of a list that is already ticked, nor a list row
            // reused for another item.
            var sameItem = ReferenceEquals(shownFor, view.BindingContext);
            shownFor = view.BindingContext;
            if (loaded && sameItem && view.IsVisible)
            {
                PlayPop(view);
            }
        };
    }

    private static void OnPressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View view || newValue is not true)
        {
            return;
        }

        if (view is Button button)
        {
            button.Pressed += (_, _) => Dip(button);
            button.Released += (_, _) => Release(button);
            return;
        }

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, _) => Dip(view);
        pointer.PointerReleased += (_, _) => Release(view);
        pointer.PointerExited += (_, _) => Release(view);
        view.GestureRecognizers.Add(pointer);
    }

    private static void Dip(VisualElement view)
    {
        if (!Motion.IsReduced)
        {
            view.AbortAnimation("maia-press");
            new Animation(v => view.Scale = v, view.Scale, 0.96, Easing.CubicOut).Commit(view, "maia-press", length: 110);
        }
    }

    private static void Release(VisualElement view)
    {
        // The dip stops first: a quick tap can be let go before its first frame, and the dip would then finish at 0.96
        // and leave the control smaller for good.
        view.AbortAnimation("maia-press");
        if (view.Scale >= 1)
        {
            return;
        }

        new Animation(v => view.Scale = v, view.Scale, 1, Easing.SpringOut).Commit(view, "maia-press", length: 320);
    }

    /// <summary>
    /// While a card fades, Android draws it once into a layer and fades the layer, instead of drawing the card and
    /// everything in it off screen again for every frame of the fade. The layer goes as the fade ends.
    /// </summary>
    private static void Layer(VisualElement view, bool on)
    {
#if ANDROID
        if (view.Handler is IViewHandler handler && (handler.ContainerView ?? handler.PlatformView) is global::Android.Views.View platform)
        {
            platform.SetLayerType(on ? global::Android.Views.LayerType.Hardware : global::Android.Views.LayerType.None, null);
        }
#endif
    }

    internal static Page? FindPage(Element element)
    {
        var parent = element.Parent;
        while (parent is not null and not Page)
        {
            parent = parent.Parent;
        }

        return parent as Page;
    }
}
