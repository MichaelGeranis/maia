using System.Windows.Input;
using Maia.Presentation.Accessibility;
using Microsoft.Maui.Controls.Shapes;

namespace Maia.App.Controls;

/// <summary>
/// A round 48 dp icon button in the <c>RoundButton</c> style, with a Pine icon: the Timer's − and +. Runs its command
/// only while it can run, and dims meanwhile (a time at its limit, or while the timer runs). A screen reader hears its
/// description as a button.
/// </summary>
public sealed class RoundIconButton : Border
{
    public static readonly BindableProperty DataProperty =
        BindableProperty.Create(nameof(Data), typeof(Geometry), typeof(RoundIconButton), propertyChanged: (b, _, n) => ((RoundIconButton)b)._icon.Data = (Geometry?)n);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(RoundIconButton), propertyChanged: (b, _, n) => ((RoundIconButton)b)._tap.SetCommand((ICommand?)n));

    private readonly Icon _icon;
    private readonly CommandTap _tap;

    public RoundIconButton()
    {
        SetDynamicResource(StyleProperty, "RoundButton");
        _icon = new Icon { Size = 18, Color = Palette.Get("Pine"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        Content = _icon;
        _tap = new CommandTap(this);
        Motions.SetPress(this, true);
        Accessible.SetRole(this, ControlRole.Button);
    }

    /// <summary>The icon's drawing, a key from Icons.xaml.</summary>
    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
