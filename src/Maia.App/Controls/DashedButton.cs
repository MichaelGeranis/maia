using System.Windows.Input;
using Maia.Presentation.Accessibility;
using Microsoft.Maui.Controls.Shapes;

namespace Maia.App.Controls;

/// <summary>The design's dashed button ("Link another contact", "Link from contacts"): a soft green panel with an icon.</summary>
public sealed class DashedButton : Border
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(DashedButton), string.Empty, propertyChanged: (b, o, n) => ((DashedButton)b).OnText((string)o, (string)n));

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(Geometry), typeof(DashedButton), propertyChanged: (b, _, n) => ((DashedButton)b)._icon.Data = (Geometry?)n);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(DashedButton), propertyChanged: (b, _, n) => ((DashedButton)b)._tap.SetCommand((ICommand?)n));

    private readonly Label _label;
    private readonly Icon _icon;
    private readonly CommandTap _tap;

    public DashedButton()
    {
        BackgroundColor = Palette.Get("Mist");
        Stroke = Palette.Get("Sage");
        StrokeThickness = 1;
        StrokeDashArray = [4, 3];
        StrokeShape = new RoundRectangle { CornerRadius = 14 };
        MinimumHeightRequest = 48;
        Padding = new Thickness(12, 0);

        _icon = new Icon { Size = 18, Color = Palette.Get("Pine") };
        _label = new Label { TextColor = Palette.Get("Pine"), VerticalTextAlignment = TextAlignment.Center };
        _label.SetDynamicResource(StyleProperty, "Body");
        _label.SetDynamicResource(Label.FontFamilyProperty, "FontTextBold");
        // A grid, not a stack, so with large text the words wrap instead of running off the button.
        var content = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 8,
            Padding = new Thickness(0, 8),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { _icon, _label },
        };
        Grid.SetColumn(_label, 1);
        Content = content;
        // Runs the command only while it can run: a double tap opens the contacts app once, as a Button would.
        _tap = new CommandTap(this);
        Accessible.SetRole(this, ControlRole.Button);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void OnText(string oldText, string text)
    {
        _label.Text = text;

        // A screen reader says its words, unless it was given a name of its own, before or after them: the small
        // "Link" on a doctor's tile on Care team is "Dentist: link from contacts".
        var description = SemanticProperties.GetDescription(this);
        if (string.IsNullOrEmpty(description) || description == oldText)
        {
            SemanticProperties.SetDescription(this, text);
        }
    }
}
