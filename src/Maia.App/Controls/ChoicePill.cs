using System.Windows.Input;
using Maia.Presentation.Accessibility;
using Microsoft.Maui.Controls.Shapes;

namespace Maia.App.Controls;

/// <summary>
/// The design's choice pill (reminders, result filters, suggestion chips): pine and white when selected, white with
/// a thin border otherwise, 44 dp high at least. A screen reader hears its text, then whether it is selected, as one
/// choice of a set.
/// </summary>
public sealed class ChoicePill : Border
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ChoicePill), string.Empty, propertyChanged: (b, _, n) => ((ChoicePill)b).OnText((string)n));

    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(ChoicePill), false, propertyChanged: (b, _, _) => ((ChoicePill)b).Paint());

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ChoicePill), propertyChanged: (b, _, n) => ((ChoicePill)b)._tap.SetCommand((ICommand?)n));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ChoicePill), propertyChanged: (b, _, n) => ((ChoicePill)b)._tap.SetParameter(n));

    private readonly Label _label;
    private readonly CommandTap _tap;

    public ChoicePill()
    {
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = 20 };
        Padding = new Thickness(14, 0);
        MinimumHeightRequest = 44;
        Margin = new Thickness(0, 0, 6, 6);

        // In a wrapping row a pill moves to the next line rather than shrink: FlexLayout would otherwise squeeze the last
        // pill of a nearly full line and cut its words ("Tas" for "Task").
        FlexLayout.SetShrink(this, 0);
        _label = new Label { HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center };
        // Body words in Bold, as on every button.
        _label.SetDynamicResource(StyleProperty, "Body");
        _label.SetDynamicResource(Label.FontFamilyProperty, "FontTextBold");
        Content = _label;

        // Runs the command only while it can run (a busy screen, or still running from the last tap), as a Button does.
        _tap = new CommandTap(this);
        Accessible.SetRole(this, ControlRole.Radio);
        Paint();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private void Paint()
    {
        BackgroundColor = Palette.Get(IsSelected ? "Pine" : "White");
        Stroke = Palette.Get(IsSelected ? "Pine" : "Line");
        _label.TextColor = Palette.Get(IsSelected ? "White" : "Pine");
        Accessible.SetIsOn(this, IsSelected);
    }

    /// <summary>The words are also its spoken name, whichever of text and selection XAML sets first.</summary>
    private void OnText(string text)
    {
        _label.Text = text;
        SemanticProperties.SetDescription(this, text);
    }
}
