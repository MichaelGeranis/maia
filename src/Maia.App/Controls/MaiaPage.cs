namespace Maia.App.Controls;

/// <summary>
/// Base for every Maia screen: the misty background, and for main screens the bottom bar. Set <see cref="Tab"/>
/// to one of <see cref="Tabs"/> to show the bar with that tab active; leave it empty for setup and pushed screens.
/// </summary>
public class MaiaPage : ContentPage
{
    public static readonly BindableProperty TabProperty =
        BindableProperty.Create(nameof(Tab), typeof(string), typeof(MaiaPage), string.Empty, propertyChanged: OnTabChanged);

    public MaiaPage()
    {
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);

        // Keyboard-aware (.NET 10 safe areas): the page pads itself above the keyboard, and the bottom bar hides while
        // she types (see BottomBar). The default pans the whole window instead, which on Android left it offset and
        // dimmed after the keyboard closed.
        SafeAreaEdges = SafeAreaEdges.All;
        ApplyTemplate(string.Empty);
    }

    /// <summary>The active bottom-bar tab, or empty for a screen without the bar.</summary>
    public string Tab
    {
        get => (string)GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    private static void OnTabChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((MaiaPage)bindable).ApplyTemplate((string)newValue);

    private void ApplyTemplate(string tab)
    {
        var key = string.IsNullOrEmpty(tab) ? "PlainPageTemplate" : "TabPageTemplate";
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var template) == true)
        {
            ControlTemplate = (ControlTemplate)template;
        }
    }
}

/// <summary>
/// Bottom-bar slot keys, in the bar's order. All but <see cref="Add"/> are also the Shell routes under <c>//main</c>.
/// </summary>
public static class Tabs
{
    public const string Week = "week";

    /// <summary>Journey (the timeline of her checks). Its route keeps its first name.</summary>
    public const string Plan = "plan";

    /// <summary>Add ("Add to your journey"), a tab like the others.</summary>
    public const string Add = "add";

    public const string Results = "results";

    /// <summary>Settings, a tab like the others: About Maia and the screens it opens are pushed from it.</summary>
    public const string Settings = "settings";
}
