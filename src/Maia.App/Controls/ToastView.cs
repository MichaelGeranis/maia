using Maia.App.Services;
using Maia.Presentation.Accessibility;
using Microsoft.Maui.Controls.Shapes;

namespace Maia.App.Controls;

/// <summary>
/// The design's toast: a pine card with a tick and one short sentence when something she did has happened; a calm Sand
/// card with no tick when it has not (her list is full, a change could not be saved). It floats near the top of the
/// screen, pops in, and fades after a few seconds (or on tap), so it is seen wherever the page is scrolled. A longer
/// message stays until it can be read to the end (<see cref="ReadingTime"/>).
/// </summary>
public sealed class ToastView : ContentView
{
    private const string AnimationName = "maia-toast";
    private readonly Label _text;
    private readonly Icon _tick;
    private readonly Border _card;
    private readonly ToastService? _toasts;
    private IDispatcherTimer? _timer;

    public ToastView()
    {
        _toasts = IPlatformApplication.Current?.Services.GetService<ToastService>();
        _text = new Label
        {
            TextColor = Palette.Get("White"),
            VerticalTextAlignment = TextAlignment.Center,
        };
        _text.SetDynamicResource(StyleProperty, "Body");
        _text.SetDynamicResource(Label.FontFamilyProperty, "FontTextSemiBold");

        _tick = new Icon { Data = (Geometry)Microsoft.Maui.Controls.Application.Current!.Resources["IconCheck"], Size = 24, Color = Palette.Get("SageTint") };
        _card = new Border
        {
            BackgroundColor = Palette.Get("Pine"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(16, 13),
            Shadow = new Shadow { Brush = Palette.Get("Pine"), Opacity = 0.25f, Radius = 28, Offset = new Point(0, 12) },
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
                ColumnSpacing = 12,
                Children = { _tick, _text },
            },
        };
        Grid.SetColumn(_text, 1);

        Content = _card;
        Margin = new Thickness(20, 44, 20, 0);
        VerticalOptions = LayoutOptions.Start;
        IsVisible = false;
        GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(Hide) });

        Loaded += (_, _) =>
        {
            if (_toasts is null)
            {
                return;
            }

            _toasts.Shown += OnShown;
            if (FindPage() is { } page)
            {
                page.Appearing -= OnPageAppearing;
                page.Appearing += OnPageAppearing;
            }

            ShowPending();
        };
        Unloaded += (_, _) =>
        {
            if (_toasts is not null)
            {
                _toasts.Shown -= OnShown;
            }
        };
    }

    private void OnPageAppearing(object? sender, EventArgs e) => Dispatcher.Dispatch(ShowPending);

    /// <summary>Takes a toast raised while this screen was still arriving.</summary>
    private void ShowPending()
    {
        if (IsOnScreen() && _toasts?.TakePending() is { } toast)
        {
            Display(toast);
        }
    }

    private void OnShown(object? sender, Toast toast)
    {
        // Only the visible screen shows it; otherwise it waits for the next screen to appear.
        if (IsOnScreen())
        {
            _toasts?.MarkShown();
            Display(toast);
        }
    }

    private void Display(Toast toast)
    {
        var message = toast.Message;

        // A notice is not a success: Sand with SandDeep words and no tick, never red.
        _card.BackgroundColor = Palette.Get(toast.IsNotice ? "Sand" : "Pine");
        _card.Stroke = Palette.Get("Sun");
        _card.StrokeThickness = toast.IsNotice ? 1 : 0;
        _text.TextColor = Palette.Get(toast.IsNotice ? "SandDeep" : "White");
        _tick.IsVisible = !toast.IsNotice;
        Grid.SetColumn(_text, toast.IsNotice ? 0 : 1);
        Grid.SetColumnSpan(_text, toast.IsNotice ? 2 : 1);

        _text.Text = message;
        Accessible.SayAfterFocus(Dispatcher, message, () => IsVisible && _text.Text == message);
        this.AbortAnimation(AnimationName);
        IsVisible = true;
        if (Motion.IsReduced)
        {
            Opacity = 1;
            Scale = 1;
        }
        else
        {
            // "pop": 0.9 → 1.02 → 1, fading in over 0.5 s.
            new Animation(t =>
            {
                Opacity = Math.Min(1, t * 1.6);
                Scale = t < 0.7 ? 0.9 + (t / 0.7 * 0.12) : 1.02 - ((t - 0.7) / 0.3 * 0.02);
            }).Commit(this, AnimationName, length: 500, easing: Easing.Linear);
        }

        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = ReadingTime.For(message);
        _timer.IsRepeating = false;
        _timer.Tick -= OnTimer;
        _timer.Tick += OnTimer;
        _timer.Stop();
        _timer.Start();
    }

    private void OnTimer(object? sender, EventArgs e) => Hide();

    private void Hide()
    {
        _timer?.Stop();
        if (Motion.IsReduced)
        {
            IsVisible = false;
            return;
        }

        new Animation(t => Opacity = 1 - t).Commit(this, AnimationName, length: 300, easing: Easing.Linear, finished: (_, _) => IsVisible = false);
    }

    private bool IsOnScreen() => FindPage() is { } page && Shell.Current?.CurrentPage == page;

    private Page? FindPage()
    {
        Element? element = this;
        while (element is not null and not Page)
        {
            element = element.Parent;
        }

        return element as Page;
    }
}
