using System.Windows.Input;
using Maia.App.Services;
using Maia.Presentation.Accessibility;
using Maia.Presentation.Localization;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Maia.App.Controls;

/// <summary>Setup progress: a pine pill for the current step, small dots for the others.</summary>
public sealed class StepDots : HorizontalStackLayout
{
    public static readonly BindableProperty StepProperty =
        BindableProperty.Create(nameof(Step), typeof(int), typeof(StepDots), 1, propertyChanged: (b, _, _) => ((StepDots)b).Build());

    public StepDots()
    {
        Spacing = 6;
        VerticalOptions = LayoutOptions.Center;
        Build();
    }

    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int Steps { get; set; } = 3;

    private void Build()
    {
        Children.Clear();
        for (var i = 1; i <= Steps; i++)
        {
            var active = i == Step;
            Children.Add(new Border
            {
                WidthRequest = active ? 28 : 6,
                HeightRequest = 6,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 3 },
                BackgroundColor = Palette.Get(active ? "Pine" : "MutedHalf"),
            });
        }
    }
}

/// <summary>The design's "‹ Back" pill.</summary>
public sealed class BackPill : Border
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(BackPill), propertyChanged: (b, _, n) => ((BackPill)b)._tap.SetCommand((ICommand?)n));

    private readonly CommandTap _tap;

    public BackPill()
    {
        BackgroundColor = Palette.Get("CardGlass");
        Stroke = Palette.Get("Line");
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = 22 };
        Padding = new Thickness(6, 0, 14, 0);
        MinimumHeightRequest = 44;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;

        var label = new Label { Text = Text.Get("Common_Back"), TextColor = Palette.Get("Pine"), VerticalTextAlignment = TextAlignment.Center };
        label.SetDynamicResource(StyleProperty, "Body");
        label.SetDynamicResource(Label.FontFamilyProperty, "FontTextBold");
        Content = new HorizontalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Icon { Data = (Geometry)Microsoft.Maui.Controls.Application.Current!.Resources["IconBack"], Size = 18, Color = Palette.Get("Pine") },
                label,
            },
        };
        // Runs the command only while it can run: a double tap goes back one screen, not two.
        _tap = new CommandTap(this);
        SemanticProperties.SetDescription(this, label.Text);
        Accessible.SetRole(this, ControlRole.Button);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}

/// <summary>
/// Setup's welcome card (design screen 01): a sky, a sun rising once, two clouds drifting, layered hills that sway
/// slowly and a sprout. Shapes and timings from the prototype; still under reduced motion.
/// </summary>
public sealed class SetupIllustration : Border
{
    private readonly Grid _sun;
    private readonly Border _cloudA;
    private readonly Border _cloudB;
    private readonly Path _backHill;
    private readonly Path _middleHill;
    private readonly Grid _sprout;

    public SetupIllustration()
    {
        HeightRequest = 210;
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 30 };
        Background = new LinearGradientBrush(
            [
                new GradientStop(Palette.Get("Sky"), 0f),
                new GradientStop(Palette.Get("Mist"), 0.7f),
                new GradientStop(Palette.Get("Mist"), 1f),
            ],
            new Point(0, 0),
            new Point(0, 1));

        _sun = new Grid
        {
            WidthRequest = 110,
            HeightRequest = 110,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(192, 24, 0, 0),
            Children =
            {
                new Ellipse
                {
                    Fill = new RadialGradientBrush(
                        [new GradientStop(Palette.Get("Sun").WithAlpha(0.55f), 0.3f), new GradientStop(Palette.Get("Sun").WithAlpha(0f), 1f)],
                        new Point(0.5, 0.5),
                        0.5),
                },
                new Ellipse
                {
                    WidthRequest = 70,
                    HeightRequest = 70,
                    Fill = new RadialGradientBrush(
                        [new GradientStop(Palette.Get("Sand"), 0f), new GradientStop(Palette.Get("Sun"), 1f)],
                        new Point(0.4, 0.4),
                        0.6),
                },
            },
        };

        _cloudA = Cloud(64, 20, 34, 0.85f);
        _cloudB = Cloud(44, 14, 70, 0.7f);
        _backHill = Hill("M0 70 C 60 30, 120 30, 190 62 S 320 20, 400 50 V120 H0 Z", "SageTint", 400, 120);
        _middleHill = Hill("M0 50 C 80 20, 150 60, 230 40 S 350 30, 400 55 V90 H0 Z", "Sage", 400, 90);
        var ground = Hill("M0 34 C 90 18, 170 44, 260 30 S 360 24, 390 32 V50 H0 Z", "Pine", 390, 50);
        ground.Margin = new Thickness(0);

        _sprout = new Grid
        {
            WidthRequest = 26,
            HeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(54, 0, 0, 22),
            AnchorY = 1,
            Children =
            {
                Shape("M13 44 V10", null, "Pine", 2),
                Shape("M13 22 C 4 20, 2 12, 4 6 C 10 8, 14 14, 13 22 Z", "Sage", null, 0),
                Shape("M13 16 C 22 14, 24 6, 22 0 C 16 2, 12 8, 13 16 Z", "Sage", null, 0),
            },
        };

        Content = new Grid
        {
            IsClippedToBounds = true,
            Children = { _sun, _cloudA, _cloudB, _backHill, _middleHill, ground, _sprout },
        };

        // Still with reduced motion: the sun is up and the clouds rest where the loop would start them.
        _cloudA.TranslationX = 40;
        _cloudB.TranslationX = 260;
        AmbientLoop.Attach(this, Tick);
    }

    private static Border Cloud(double width, double height, double top, float alpha) => new()
    {
        WidthRequest = width,
        HeightRequest = height,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = height / 2 },
        BackgroundColor = Palette.Get("White").WithAlpha(alpha),
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Start,
        Margin = new Thickness(0, top, 0, 0),
    };

    private static Path Hill(string data, string color, double width, double height) => new()
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
        Fill = Palette.Get(color),
        WidthRequest = width,
        HeightRequest = height,
        Aspect = Stretch.None,
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.End,
        Margin = new Thickness(-10, 0, 0, 0),
    };

    private static Path Shape(string data, string? fill, string? stroke, double thickness) => new()
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
        Fill = new SolidColorBrush(fill is null ? Colors.Transparent : Palette.Get(fill)),
        Stroke = new SolidColorBrush(stroke is null ? Colors.Transparent : Palette.Get(stroke)),
        StrokeThickness = thickness,
        StrokeLineCap = PenLineCap.Round,
        Aspect = Stretch.None,
    };

    private void Tick(double s)
    {
        // Sun rises 70 dp once (2.4 s after 0.2 s), fading in from 0.3.
        var rise = Math.Clamp((s - 0.2) / 2.4, 0, 1);
        rise = 1 - Math.Pow(1 - rise, 3);
        _sun.TranslationY = 70 * (1 - rise);
        _sun.Opacity = 0.3 + (0.7 * rise);

        // Clouds drift across from −80 to 420 on 40 s and 55 s loops.
        _cloudA.TranslationX = -80 + (s / 40 % 1 * 500);
        _cloudB.TranslationX = -80 + ((s + 22) / 55 % 1 * 500);

        // Hills sway 18 dp, ease-in-out, on 18 s and 12 s loops; the sprout sways ±6° every 4 s.
        _backHill.TranslationX = -18 * (1 - Math.Cos(s / 18 * 2 * Math.PI)) / 2;
        _middleHill.TranslationX = -18 * (1 - Math.Cos(-s / 12 * 2 * Math.PI)) / 2;
        _sprout.Rotation = 6 * Math.Sin(s / 4 * 2 * Math.PI);
    }
}
