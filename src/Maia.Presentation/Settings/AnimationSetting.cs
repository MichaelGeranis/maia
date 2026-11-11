using CommunityToolkit.Mvvm.ComponentModel;

namespace Maia.Presentation.Settings;

/// <summary>Where her "Animations" choice is kept on this phone.</summary>
public interface IAnimationPreference
{
    /// <summary>On unless she turned it off. Changing it takes effect at once, on every screen.</summary>
    bool AnimationsOn { get; set; }
}

/// <summary>
/// "Animations" in Settings, on by default. Off, nothing on Maia's screens moves, exactly as when the phone asks for
/// reduced motion; the phone's own "Remove animations" still stills them whatever this says.
/// </summary>
public sealed partial class AnimationSetting : ObservableObject
{
    private readonly IAnimationPreference _preference;

    public AnimationSetting(IAnimationPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        _preference = preference;
        IsOn = preference.AnimationsOn;
    }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    partial void OnIsOnChanged(bool value) => _preference.AnimationsOn = value;
}
