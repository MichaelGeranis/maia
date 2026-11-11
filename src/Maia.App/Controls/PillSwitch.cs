using Maia.App.Services;
using Maia.Presentation.Accessibility;
using Microsoft.Maui.Controls.Shapes;

namespace Maia.App.Controls;

/// <summary>
/// The design's switch: a 50×30 track (Sage on, a grey-green off) with a white knob that slides, in a 60×44 touch area.
/// The native Android switch has a white thumb that disappears on Maia's white cards. A screen reader hears it as a
/// switch named by <see cref="Label"/>, then whether it is on or off, in the language Maia shows.
/// </summary>
public sealed class PillSwitch : ContentView
{
    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(PillSwitch), false, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((PillSwitch)b).Paint(animate: true));

    /// <summary>What the switch turns on, as its row says it ("Lock Maia", a check's name): its spoken name.</summary>
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(PillSwitch), string.Empty, propertyChanged: (b, _, n) => SemanticProperties.SetDescription(b, (string)n));

    private readonly Border _track;

    /// <summary>
    /// What slides: a plain holder around the white knob. The knob's shadow makes Android wrap the knob's own view in a
    /// second one, sometimes only after a position was already set on the first; moved itself, the knob could end up
    /// shifted twice, past the end of the track. The holder never gets a wrapper, so its position always lands once.
    /// </summary>
    private readonly ContentView _knob;

    public PillSwitch()
    {
        _knob = new ContentView
        {
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(3, 0, 0, 0),
            Content = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Palette.Get("White"),
                Shadow = new Shadow { Brush = Palette.Get("Ink"), Opacity = 0.18f, Radius = 6, Offset = new Point(0, 2) },
            },
        };
        _track = new Border
        {
            WidthRequest = 50,
            HeightRequest = 30,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new Grid { Children = { _knob } },
        };

        Content = _track;
        WidthRequest = 60;
        HeightRequest = 44;
        GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => IsToggled = !IsToggled) });
        Accessible.SetRole(this, ControlRole.Switch);
        Paint(animate: false);
    }

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Only a switch already on screen slides. One that is on as it first shows (its binding arrives before it is
    /// drawn) gets its knob placed at once, so it doesn't slide in each time the screen opens.
    /// </summary>
    private void Paint(bool animate)
    {
        _track.BackgroundColor = Palette.Get(IsToggled ? "Sage" : "Muted");
        var x = IsToggled ? 20 : 0;
        if (animate && IsLoaded && _knob.Handler is not null && !Motion.IsReduced)
        {
            _knob.TranslateToAsync(x, 0, 250, Easing.CubicOut);
        }
        else
        {
            _knob.CancelAnimations();
            _knob.TranslationX = x;
        }

        // The state only: the spoken name stays the label.
        Accessible.SetIsOn(this, IsToggled);
    }
}
