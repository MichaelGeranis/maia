using CommunityToolkit.Mvvm.Input;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.About;

/// <summary>This build of Maia, as the phone reports it. Implemented by the app.</summary>
public interface IAppVersion
{
    /// <summary>The version she sees in the store, such as "0.1".</summary>
    string Version { get; }

    /// <summary>The build number, such as "1".</summary>
    string Build { get; }
}

/// <summary>
/// About Maia (from Settings): what Maia is (it organises, her doctor decides), the version, and the way to the privacy
/// notice, the terms of use and the licences.
/// </summary>
public sealed partial class AboutViewModel
{
    private readonly INavigator _navigator;

    public AboutViewModel(IAppVersion version, INavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(version);
        _navigator = navigator;
        Version = Text.Format("About_Version", version.Version, version.Build);
    }

    /// <summary>"Version 0.1 (1)".</summary>
    public string Version { get; }

    [RelayCommand]
    private Task OpenPrivacyAsync() => _navigator.GoToPrivacyAsync();

    [RelayCommand]
    private Task OpenTermsAsync() => _navigator.GoToTermsAsync();

    [RelayCommand]
    private Task OpenLicencesAsync() => _navigator.GoToLicencesAsync();

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();
}
