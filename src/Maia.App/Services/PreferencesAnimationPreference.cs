using Maia.Presentation.Settings;

namespace Maia.App.Services;

/// <summary>Her "Animations" choice, kept in app preferences through <see cref="Motion"/>, which every animation asks.</summary>
public sealed class PreferencesAnimationPreference : IAnimationPreference
{
    public bool AnimationsOn
    {
        get => Motion.AnimationsOn;
        set => Motion.AnimationsOn = value;
    }
}
