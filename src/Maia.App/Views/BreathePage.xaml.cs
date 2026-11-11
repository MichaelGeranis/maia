using System.ComponentModel;
using Maia.App.Controls;
using Maia.App.Services;
using Maia.Presentation.Calm;

namespace Maia.App.Views;

public partial class BreathePage : MaiaPage
{
    /// <summary>The sphere at rest and after breathing out; breathing in grows it to full size.</summary>
    public const double RestScale = 0.73;

    private readonly BreatheViewModel _viewModel;
    private Window? _window;
    private bool _showing;

    public BreathePage(BreatheViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // The dashed ring turns slowly, once a minute, on the ambient loops' shared clock: still with reduced motion or
        // Battery Saver, and only while this screen shows.
        AmbientLoop.Attach(OuterRing, seconds => OuterRing.Rotation = seconds * 6 % 360);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _showing = true;
        _viewModel.PropertyChanged += OnChanged;
        Watch(Window);
    }

    /// <summary>Breathing stops when she leaves the screen.</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _showing = false;
        Watch(null);
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Stop();
        Sphere.CancelAnimations();
        Sphere.Scale = RestScale;
    }

    /// <summary>
    /// Shell shows this page before it is in the window, so the window is watched once it is there too; otherwise going
    /// to the background would go unnoticed.
    /// </summary>
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Window) && _showing)
        {
            Watch(Window);
        }
    }

    private void Watch(Window? window)
    {
        if (ReferenceEquals(window, _window))
        {
            return;
        }

        if (_window is not null)
        {
            _window.Stopped -= OnStopped;
        }

        _window = window;
        if (_window is not null)
        {
            _window.Stopped += OnStopped;
        }
    }

    /// <summary>
    /// And it pauses when Maia goes to the background (home, another app, the screen turned off): no vibration or tone
    /// carries on in her pocket, and the time left waits for her to resume. A finished exercise keeps its "Well done".
    /// </summary>
    private void OnStopped(object? sender, EventArgs e) => _viewModel.Pause();

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The time left changes every second and has nothing to animate.
        if (e.PropertyName is nameof(BreatheViewModel.PhaseText) or nameof(BreatheViewModel.IsInhaling) or nameof(BreatheViewModel.IsBreathing))
        {
            ScreenWork.Run(() => AnimateAsync(e), "breathe.animate");
        }
    }

    private async Task AnimateAsync(PropertyChangedEventArgs e)
    {
        if (Motion.IsReduced)
        {
            return;
        }

        if (e.PropertyName == nameof(BreatheViewModel.PhaseText))
        {
            Phase.Opacity = 0;
            await Phase.FadeToAsync(1, 600);
        }
        else
        {
            Sphere.CancelAnimations();
            // A breath resumed part way grows or settles in what is left of it.
            var (scale, length) = _viewModel.IsInhaling
                ? (1d, _viewModel.PhaseLeft)
                : (RestScale, _viewModel.IsBreathing ? _viewModel.PhaseLeft : TimeSpan.FromSeconds(1));
            await Sphere.ScaleToAsync(scale, (uint)length.TotalMilliseconds, Easing.SinInOut);
        }
    }
}
