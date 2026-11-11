namespace Maia.Presentation.Localization;

/// <summary>
/// Switches the language the app shows (<see cref="Text.English"/> or <see cref="Text.Greek"/>). Implemented by the
/// app: it remembers the choice for next time and redraws every screen in the new language.
/// </summary>
public interface ILanguageSwitcher
{
    /// <param name="language"><see cref="Text.English"/> or <see cref="Text.Greek"/>.</param>
    /// <param name="returnTo">Where the redrawn app opens: Settings, or setup for a choice made before there is a plan.</param>
    Task SwitchAsync(string language, LanguageReturn returnTo = LanguageReturn.Settings);
}

/// <summary>Where the app opens again once it is redrawn in another language.</summary>
public enum LanguageReturn
{
    /// <summary>Settings, where she switched.</summary>
    Settings,

    /// <summary>Setup, at the step she reached, with her choices kept (there is no plan yet).</summary>
    Setup,
}
