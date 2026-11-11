using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.About;

/// <summary>The licence texts shipped inside Maia (from the repository's <c>licenses</c> folder). Implemented by the app.</summary>
public interface ILicenceTexts
{
    Task<string> ReadAsync(string fileName);
}

/// <summary>
/// A font, the icon set or a family of libraries in Maia: its name, licence and copyright, the NuGet packages it comes
/// in, and the full licence text, which opens in place.
/// </summary>
public sealed partial class LicenceItem : ObservableObject
{
    private readonly ILicenceTexts _texts;
    private readonly IErrorReporter _errors;

    internal LicenceItem(ILicenceTexts texts, IErrorReporter errors, string name, string licence, string copyright, string file, params string[] packages)
    {
        _texts = texts;
        _errors = errors;
        Name = name;
        Licence = licence;
        Copyright = copyright;
        File = file;
        Packages = packages;
    }

    public string Name { get; }

    /// <summary>The licence's own name, such as "MIT License". Names are not translated.</summary>
    public string Licence { get; }

    public string Copyright { get; }

    /// <summary>The licence text's file name among the texts shipped with Maia.</summary>
    public string File { get; }

    /// <summary>The NuGet packages this entry covers; empty for a font or the icons.</summary>
    public IReadOnlyList<string> Packages { get; }

    /// <summary>"Packages: Microsoft.Maui.Controls, …", or empty for a font or the icons.</summary>
    public string PackageList => Packages.Count == 0 ? string.Empty : Text.Format("Licences_Packages", string.Join(", ", Packages));

    public bool HasPackages => Packages.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    public partial bool IsOpen { get; private set; }

    /// <summary>The full licence text, once opened.</summary>
    [ObservableProperty]
    public partial string? FullText { get; private set; }

    /// <summary>"Show licence" or "Hide licence".</summary>
    public string ToggleText => Text.Get(IsOpen ? "Licences_Hide" : "Licences_Show");

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsOpen)
        {
            IsOpen = false;
            return;
        }

        if (FullText is null)
        {
            try
            {
                FullText = await _texts.ReadAsync(File);
            }
#pragma warning disable CA1031 // A missing text is said calmly; the reporter gets the type and a fixed operation.
            catch (Exception ex)
            {
                _errors.Report(ex, "licences.read");
                FullText = Text.Get("Licences_Unreadable");
            }
#pragma warning restore CA1031
        }

        IsOpen = true;
    }
}

/// <summary>
/// Licences (from About Maia): the fonts, the icons and every NuGet package Maia ships, with their licences, grouped the
/// way they come (the .NET MAUI packages, the Android libraries it binds, and so on). THIRD-PARTY-NOTICES.md lists the
/// same.
/// </summary>
public sealed partial class LicencesViewModel
{
    private readonly INavigator _navigator;

    public LicencesViewModel(ILicenceTexts texts, INavigator navigator, IErrorReporter errors)
    {
        _navigator = navigator;
        const string ofl = "SIL Open Font License 1.1";
        Fonts =
        [
            new(texts, errors, "M PLUS Rounded 1c", ofl, "Copyright 2016 The Rounded M+ Project Authors", "mplusrounded1c-OFL.txt"),
        ];

        // Lucide's icons are drawn from its SVG paths (Resources/Styles/Icons.xaml); those it took from Feather keep MIT.
        Icons =
        [
            new(texts, errors, "Lucide", "ISC License · MIT License (icons from Feather)", "Copyright (c) Lucide Contributors 2022 · Feather icons: Cole Bemis 2013-2022", "lucide-ISC.txt"),
        ];

        const string mit = "MIT License";
        const string dotnet = "Copyright (c) .NET Foundation and Contributors";
        const string bindings = "MIT License (.NET bindings) · Apache License 2.0";
        Libraries =
        [
            new(
                texts, errors, ".NET MAUI", mit, dotnet, "dotnet-MIT.txt",
                "Microsoft.Maui.Controls", "Microsoft.Maui.Controls.Core", "Microsoft.Maui.Controls.Xaml", "Microsoft.Maui.Core",
                "Microsoft.Maui.Essentials", "Microsoft.Maui.Graphics"),
            new(
                texts, errors, "Microsoft.Extensions", mit, dotnet, "dotnet-MIT.txt",
                "Microsoft.Extensions.Configuration", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Logging",
                "Microsoft.Extensions.Logging.Debug"),
            new(texts, errors, ".NET Community Toolkit", mit, "Copyright © .NET Foundation and Contributors", "communitytoolkit-MIT.txt", "CommunityToolkit.Mvvm"),
            new(texts, errors, "AndroidX and Material Components", bindings, "The Android Open Source Project · .NET bindings: .NET Foundation Contributors", "android-libraries-NOTICES.txt", AndroidX),
            new(
                texts, errors, "Kotlin and Google libraries for Android", bindings, "JetBrains, Google and others · .NET bindings: .NET Foundation Contributors", "android-libraries-NOTICES.txt",
                "Xamarin.Kotlin.StdLib", "Xamarin.KotlinX.AtomicFU", "Xamarin.KotlinX.AtomicFU.Jvm", "Xamarin.KotlinX.Coroutines.Android", "Xamarin.KotlinX.Coroutines.Core",
                "Xamarin.KotlinX.Coroutines.Core.Jvm", "Xamarin.KotlinX.Serialization.Core", "Xamarin.KotlinX.Serialization.Core.Jvm",
                "GoogleGson", "Xamarin.Google.Crypto.Tink.Android", "Xamarin.Google.Guava.ListenableFuture",
                "Xamarin.Google.ErrorProne.Annotations", "Xamarin.Google.Code.FindBugs.JSR305", "Xamarin.Jetbrains.Annotations",
                "Xamarin.JSpecify"),
            new(
                texts, errors, "Glide", "MIT License (.NET bindings) · BSD 2-Clause · Apache License 2.0", "Google and others · .NET bindings: .NET Foundation Contributors", "glide-NOTICES.txt",
                "Xamarin.Android.Glide", "Xamarin.Android.Glide.Annotations", "Xamarin.Android.Glide.DiskLruCache", "Xamarin.Android.Glide.GifDecoder"),
        ];
    }

    /// <summary>The AndroidX and Material packages .NET MAUI brings to Android.</summary>
    internal static readonly string[] AndroidX =
    [
        "Xamarin.AndroidX.Activity", "Xamarin.AndroidX.Activity.Ktx", "Xamarin.AndroidX.Annotation", "Xamarin.AndroidX.Annotation.Experimental",
        "Xamarin.AndroidX.Annotation.Jvm", "Xamarin.AndroidX.AppCompat", "Xamarin.AndroidX.AppCompat.AppCompatResources",
        "Xamarin.AndroidX.Arch.Core.Common", "Xamarin.AndroidX.Arch.Core.Runtime", "Xamarin.AndroidX.Biometric", "Xamarin.AndroidX.Browser",
        "Xamarin.AndroidX.CardView", "Xamarin.AndroidX.Collection", "Xamarin.AndroidX.Collection.Jvm", "Xamarin.AndroidX.Collection.Ktx",
        "Xamarin.AndroidX.Compose.Runtime.Annotation", "Xamarin.AndroidX.Compose.Runtime.Annotation.Android", "Xamarin.AndroidX.Concurrent.Futures",
        "Xamarin.AndroidX.ConstraintLayout", "Xamarin.AndroidX.ConstraintLayout.Core", "Xamarin.AndroidX.CoordinatorLayout", "Xamarin.AndroidX.Core",
        "Xamarin.AndroidX.Core.Core.Ktx", "Xamarin.AndroidX.Core.ViewTree", "Xamarin.AndroidX.Credentials", "Xamarin.AndroidX.CursorAdapter",
        "Xamarin.AndroidX.CustomView", "Xamarin.AndroidX.CustomView.PoolingContainer", "Xamarin.AndroidX.DrawerLayout", "Xamarin.AndroidX.DynamicAnimation",
        "Xamarin.AndroidX.Emoji2", "Xamarin.AndroidX.Emoji2.ViewsHelper", "Xamarin.AndroidX.ExifInterface", "Xamarin.AndroidX.Fragment",
        "Xamarin.AndroidX.Fragment.Ktx", "Xamarin.AndroidX.Graphics.Shapes", "Xamarin.AndroidX.Graphics.Shapes.Android", "Xamarin.AndroidX.Interpolator", "Xamarin.AndroidX.Lifecycle.Common", "Xamarin.AndroidX.Lifecycle.Common.Jvm", "Xamarin.AndroidX.Lifecycle.LiveData",
        "Xamarin.AndroidX.Lifecycle.LiveData.Core", "Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx", "Xamarin.AndroidX.Lifecycle.Process",
        "Xamarin.AndroidX.Lifecycle.Runtime", "Xamarin.AndroidX.Lifecycle.Runtime.Android", "Xamarin.AndroidX.Lifecycle.Runtime.Ktx",
        "Xamarin.AndroidX.Lifecycle.Runtime.Ktx.Android", "Xamarin.AndroidX.Lifecycle.ViewModel", "Xamarin.AndroidX.Lifecycle.ViewModel.Android",
        "Xamarin.AndroidX.Lifecycle.ViewModel.Ktx", "Xamarin.AndroidX.Lifecycle.ViewModelSavedState", "Xamarin.AndroidX.Lifecycle.ViewModelSavedState.Android",
        "Xamarin.AndroidX.Loader", "Xamarin.AndroidX.Navigation.Common", "Xamarin.AndroidX.Navigation.Common.Android", "Xamarin.AndroidX.Navigation.Fragment",
        "Xamarin.AndroidX.Navigation.Runtime", "Xamarin.AndroidX.Navigation.Runtime.Android", "Xamarin.AndroidX.Navigation.UI",
        "Xamarin.AndroidX.NavigationEvent", "Xamarin.AndroidX.NavigationEvent.Android",
        "Xamarin.AndroidX.ProfileInstaller.ProfileInstaller", "Xamarin.AndroidX.RecyclerView", "Xamarin.AndroidX.ResourceInspection.Annotation",
        "Xamarin.AndroidX.SavedState", "Xamarin.AndroidX.SavedState.SavedState.Android", "Xamarin.AndroidX.SavedState.SavedState.Ktx",
        "Xamarin.AndroidX.Security.SecurityCrypto", "Xamarin.AndroidX.SlidingPaneLayout", "Xamarin.AndroidX.Startup.StartupRuntime",
        "Xamarin.AndroidX.SwipeRefreshLayout", "Xamarin.AndroidX.Tracing.Tracing", "Xamarin.AndroidX.Tracing.Tracing.Android", "Xamarin.AndroidX.Transition",
        "Xamarin.AndroidX.VectorDrawable", "Xamarin.AndroidX.VectorDrawable.Animated", "Xamarin.AndroidX.VersionedParcelable", "Xamarin.AndroidX.ViewPager",
        "Xamarin.AndroidX.ViewPager2", "Xamarin.AndroidX.Window", "Xamarin.AndroidX.Window.WindowCore", "Xamarin.AndroidX.Window.WindowCore.Jvm",
        "Xamarin.Google.Android.Material",
    ];

    public IReadOnlyList<LicenceItem> Fonts { get; }

    /// <summary>The one icon set, Lucide.</summary>
    public IReadOnlyList<LicenceItem> Icons { get; }

    public IReadOnlyList<LicenceItem> Libraries { get; }

    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();
}
