using Maia.App.Services;
using Maia.Presentation.Navigation;
using Microsoft.Maui.Controls.Shapes;

namespace Maia.App.Controls;

/// <summary>The main screens' bottom bar. <see cref="Current"/> is the active slot (a <see cref="Tabs"/> key).</summary>
public partial class BottomBar : ContentView
{
    /// <summary>The bar's height: the bottom margin <c>TabPageTemplate</c> gives a screen's content.</summary>
    public const double BarHeight = 78;

    public static readonly BindableProperty CurrentProperty =
        BindableProperty.Create(nameof(Current), typeof(string), typeof(BottomBar), string.Empty, propertyChanged: (b, _, _) => ((BottomBar)b).Highlight());

    private readonly Slot[] _slots;

    public BottomBar()
    {
        InitializeComponent();
        _slots =
        [
            new(Tabs.Week, WeekTab, WeekIcon, WeekLabel, WeekDot),
            new(Tabs.Plan, JourneyTab, JourneyIcon, JourneyLabel, JourneyDot),
            new(Tabs.Add, AddTab, AddIcon, AddLabel, AddDot),
            new(Tabs.Results, ResultsTab, ResultsIcon, ResultsLabel, ResultsDot),
            new(Tabs.Settings, SettingsTab, SettingsIcon, SettingsLabel, SettingsDot),
        ];
        Highlight();

        // The bar sits still over lists that scroll under it: it is drawn once into a layer, not again every frame.
        Drawing.KeepInLayer(this);

        // With a long name ("Αποτελέσματα") or large text, the names shrink together just enough to fit (TabLabelFit).
        SizeChanged += (_, _) => FitLabels();

        // Out of the way while she types: pages pad themselves above the keyboard, and the bar would ride up on it.
        Loaded += (_, _) =>
        {
            if (Motions.FindPage(this) is { } page)
            {
                page.Appearing -= OnPageAppearing;
                page.Appearing += OnPageAppearing;
            }

            Bounce();
            SoftKeyboard.Watch(this);
            SoftKeyboard.VisibilityChanged += OnKeyboardChanged;
            FollowKeyboard();
        };
        Unloaded += (_, _) => SoftKeyboard.VisibilityChanged -= OnKeyboardChanged;
    }

    public string Current
    {
        get => (string)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    private void OnPageAppearing(object? sender, EventArgs e) => Bounce();

    /// <summary>
    /// The five names in one size: each is measured at the design's size with her phone's text setting, and all shrink
    /// together if two neighbours would touch or one would run past its slot (see <see cref="TabLabelFit"/>). The
    /// longest is measured once more without her text setting, so the smallest size holds for what she actually sees.
    /// </summary>
    private void FitLabels()
    {
        var padding = Slots.Padding;
        var slot = (Width - padding.HorizontalThickness) / _slots.Length;
        if (slot <= 0)
        {
            return;
        }

        var widths = new double[_slots.Length];
        var longest = 0;
        for (var i = 0; i < _slots.Length; i++)
        {
            var label = _slots[i].Label;
            label.FontSize = TabLabelFit.DesignSize;
            widths[i] = WidthOf(label);
            longest = widths[i] > widths[longest] ? i : longest;
        }

        var textScale = TextScale(_slots[longest].Label, widths[longest]);
        var size = TabLabelFit.Size(slot, Math.Min(padding.Left, padding.Right), -WeekLabel.Margin.Left, widths, textScale);
        foreach (var each in _slots)
        {
            each.Label.FontSize = size;
        }
    }

    /// <summary>
    /// Her phone's text size as this label shows it: how much wider the name is with it than without. Measured, not read
    /// from the phone's settings, so it is exactly what the label does on any platform.
    /// </summary>
    private static double TextScale(Label label, double scaled)
    {
        label.FontAutoScalingEnabled = false;
        var plain = WidthOf(label);
        label.FontAutoScalingEnabled = true;
        return plain > 0 && scaled > 0 ? scaled / plain : 1;
    }

    private static double WidthOf(Label label) =>
        label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width - label.Margin.HorizontalThickness;

    /// <summary>The active slot's icon springs up and its dot grows in, as the screen arrives.</summary>
    private void Bounce()
    {
        if (Array.Find(_slots, s => s.Key == Current) is not { } active || Motion.IsReduced)
        {
            return;
        }

        var (icon, dot) = (active.Icon, active.Dot);
        icon.AbortAnimation("maia-tab");
        new Animation
        {
            { 0, 1, new Animation(v => icon.Scale = v, 0.78, 1, Easing.SpringOut) },
            { 0, 0.45, new Animation(v => icon.TranslationY = v, 0, -4, Easing.CubicOut) },
            { 0.45, 1, new Animation(v => icon.TranslationY = v, -4, 0, Easing.BounceOut) },
            { 0.2, 0.8, new Animation(v => dot.Scale = v, 0, 1, Easing.SpringOut) },
        }.Commit(icon, "maia-tab", length: 650);
    }

    private void OnKeyboardChanged(object? sender, EventArgs e) => FollowKeyboard();

    /// <summary>
    /// Hidden while she types, and the screen's content, which otherwise ends above the bar (the page template's
    /// bottom margin), then reaches down to the keyboard: no empty strip is left where the bar was.
    /// </summary>
    private void FollowKeyboard()
    {
        IsVisible = !SoftKeyboard.IsVisible;
        if (Parent is Layout page && page.Children.OfType<ContentPresenter>().FirstOrDefault() is { } content)
        {
            content.Margin = new Thickness(0, 0, 0, IsVisible ? BarHeight : 0);
        }
    }

    private void Highlight()
    {
        foreach (var slot in _slots)
        {
            Mark(slot, slot.Key == Current);
        }
    }

    /// <summary>The active slot in Primary with its dot; a screen reader hears it as the selected tab.</summary>
    private static void Mark(Slot slot, bool active)
    {
        Accessible.SetIsOn(slot.Tab, active);
        var color = Palette.Get(active ? "Pine" : "Muted");
        slot.Icon.Color = color;
        slot.Label.TextColor = color;
        slot.Dot.Fill = active ? new SolidColorBrush(Palette.Get("Sage")) : new SolidColorBrush(Colors.Transparent);
    }

    private void OnWeekTapped(object? sender, TappedEventArgs e) => GoToTab(Tabs.Week);

    private void OnJourneyTapped(object? sender, TappedEventArgs e) => GoToTab(Tabs.Plan);

    private void OnAddTapped(object? sender, TappedEventArgs e) => GoToTab(Tabs.Add);

    private void OnResultsTapped(object? sender, TappedEventArgs e) => GoToTab(Tabs.Results);

    private void OnSettingsTapped(object? sender, TappedEventArgs e) => GoToTab(Tabs.Settings);

    /// <summary>
    /// Tabs switch instantly; a tab reached from a pushed screen (About Maia, Questions) goes back to its root. Through
    /// the navigation gate, so a double tap switches once.
    /// </summary>
    private static void GoToTab(string tab) => ShellNavigator.Current?.TapAsync($"//main/{tab}", animate: false);

    /// <summary>One slot of the bar: its key, the tappable stack, and the parts that show it is active.</summary>
    private sealed record Slot(string Key, VisualElement Tab, Icon Icon, Label Label, Ellipse Dot);
}
