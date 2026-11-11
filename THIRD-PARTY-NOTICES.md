# Third-party notices

Maia ships the fonts, icons and NuGet packages below. The app shows the same list, with each licence text, under
Settings, About Maia, Licences (`LicencesViewModel`); the licence texts it shows are the files in
[`licenses/`](licenses/), bundled into the app. A test checks that every package the app references is listed here
and on that screen. Update both when a package is added, removed or upgraded.

## Fonts

Bundled in `src/Maia.App/Resources/Fonts/`, each under the SIL Open Font License 1.1
(full text and copyright lines in [`licenses/fonts/`](licenses/fonts/)).

| Font | Files | Source |
| --- | --- | --- |
| M PLUS Rounded 1c (Regular, Medium, Bold) | `MPLUSRounded1c-*.ttf` | https://github.com/coz-m/MPLUS_FONTS (via https://fonts.google.com/specimen/M+PLUS+Rounded+1c) |

One family for English and Greek. The files are subset to the Latin, Greek and Cyrillic characters (the Japanese
glyphs are left out), which the OFL allows; the font keeps its name, as the licence reserves none.

## Icons

Drawn in the app from the icons' SVG paths, in `src/Maia.App/Resources/Styles/Icons.xaml`, under the ISC License; the
icons Lucide took from Feather stay under the MIT License (full text and copyright lines in
[`licenses/lucide-ISC.txt`](licenses/lucide-ISC.txt)).

| Icons | Version | Source |
| --- | --- | --- |
| Lucide | lucide-static 0.460.0 | https://lucide.dev (https://github.com/lucide-icons/lucide) |

## NuGet packages in the Android app

Resolved for `net11.0-android` (`dotnet list src/Maia.App package --include-transitive`). Licences are the
packages' own SPDX expressions. The `Xamarin.*` and `GoogleGson` packages are Microsoft's .NET bindings (MIT) of
Android, Kotlin and Google libraries, which keep their own licences (Apache 2.0; Glide also BSD 2-Clause).

From .NET 11, `Microsoft.Extensions.Configuration.Abstractions`, `DependencyInjection.Abstractions`,
`Diagnostics.Abstractions`, `FileProviders.Abstractions`, `Hosting.Abstractions`, `Logging.Abstractions`, `Options`
and `Primitives` are part of .NET itself rather than separate packages; they ship in the app with the .NET runtime,
under the same MIT licence ([`dotnet-MIT.txt`](licenses/dotnet-MIT.txt)).

| Package | Version | Licence | Licence text |
| --- | --- | --- | --- |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | [`communitytoolkit-MIT.txt`](licenses/communitytoolkit-MIT.txt) |
| Microsoft.Extensions.Logging.Debug | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Maui.Controls | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| GoogleGson | 2.14.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Microsoft.Extensions.Configuration | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Extensions.DependencyInjection | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Extensions.Logging | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Maui.Controls.Core | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Maui.Controls.Xaml | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Maui.Core | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Maui.Essentials | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Microsoft.Maui.Graphics | 11.0.0 | MIT | [`dotnet-MIT.txt`](licenses/dotnet-MIT.txt) |
| Xamarin.Android.Glide | 5.0.7.1 | MIT AND BSD-2-Clause AND Apache-2.0 | [`glide-NOTICES.txt`](licenses/glide-NOTICES.txt) |
| Xamarin.Android.Glide.Annotations | 5.0.7.1 | MIT AND BSD-2-Clause AND Apache-2.0 | [`glide-NOTICES.txt`](licenses/glide-NOTICES.txt) |
| Xamarin.Android.Glide.DiskLruCache | 5.0.7.1 | MIT AND BSD-2-Clause AND Apache-2.0 | [`glide-NOTICES.txt`](licenses/glide-NOTICES.txt) |
| Xamarin.Android.Glide.GifDecoder | 5.0.7.1 | MIT AND BSD-2-Clause AND Apache-2.0 | [`glide-NOTICES.txt`](licenses/glide-NOTICES.txt) |
| Xamarin.AndroidX.Activity | 1.13.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Activity.Ktx | 1.13.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Annotation | 1.10.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Annotation.Experimental | 1.6.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Annotation.Jvm | 1.10.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.AppCompat | 1.7.1.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.AppCompat.AppCompatResources | 1.7.1.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Arch.Core.Common | 2.2.0.21 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Arch.Core.Runtime | 2.2.0.21 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Biometric | 1.1.0.33 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Browser | 1.10.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.CardView | 1.0.0.39 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Collection | 1.6.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Collection.Jvm | 1.6.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Collection.Ktx | 1.6.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Compose.Runtime.Annotation | 1.11.3.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Compose.Runtime.Annotation.Android | 1.11.3.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Concurrent.Futures | 1.3.0.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.ConstraintLayout | 2.2.1.6 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.ConstraintLayout.Core | 1.1.1.6 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.CoordinatorLayout | 1.3.0.6 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Core | 1.19.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Core.Core.Ktx | 1.19.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Core.ViewTree | 1.0.0.6 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Credentials | 1.6.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.CursorAdapter | 1.0.0.37 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.CustomView | 1.2.0.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.CustomView.PoolingContainer | 1.1.0.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.DrawerLayout | 1.2.0.21 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.DynamicAnimation | 1.1.0.6 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Emoji2 | 1.6.0.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Emoji2.ViewsHelper | 1.6.0.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.ExifInterface | 1.4.2.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Fragment | 1.8.9.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Fragment.Ktx | 1.8.9.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Graphics.Shapes | 1.1.0.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Graphics.Shapes.Android | 1.1.0.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Interpolator | 1.0.0.37 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.Common | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.Common.Jvm | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.LiveData | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.LiveData.Core | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.Process | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.Runtime | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.Runtime.Android | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.Runtime.Ktx | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.Runtime.Ktx.Android | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.ViewModel | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.ViewModel.Android | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.ViewModel.Ktx | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.ViewModelSavedState | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Lifecycle.ViewModelSavedState.Android | 2.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Loader | 1.1.0.37 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Navigation.Common | 2.9.8.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Navigation.Common.Android | 2.9.8.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Navigation.Fragment | 2.9.8.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Navigation.Runtime | 2.9.8.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Navigation.Runtime.Android | 2.9.8.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Navigation.UI | 2.9.8.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.NavigationEvent | 1.1.2.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.NavigationEvent.Android | 1.1.2.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.ProfileInstaller.ProfileInstaller | 1.4.1.8 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.RecyclerView | 1.4.0.6 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.ResourceInspection.Annotation | 1.0.1.25 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.SavedState | 1.5.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.SavedState.SavedState.Android | 1.5.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.SavedState.SavedState.Ktx | 1.5.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Security.SecurityCrypto | 1.1.0.4-alpha07 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.SlidingPaneLayout | 1.2.0.25 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Startup.StartupRuntime | 1.2.0.8 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.SwipeRefreshLayout | 1.2.0.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Tracing.Tracing | 1.3.0.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Tracing.Tracing.Android | 1.3.0.4 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Transition | 1.7.0.2 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.VectorDrawable | 1.2.0.11 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.VectorDrawable.Animated | 1.2.0.11 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.VersionedParcelable | 1.2.1.6 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.ViewPager | 1.1.0.7 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.ViewPager2 | 1.1.0.11 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Window | 1.5.1.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Window.WindowCore | 1.5.1.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.AndroidX.Window.WindowCore.Jvm | 1.5.1.3 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.Google.Android.Material | 1.14.0.5 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.Google.Code.FindBugs.JSR305 | 3.0.2.24 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.Google.Crypto.Tink.Android | 1.22.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.Google.ErrorProne.Annotations | 2.50.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.Google.Guava.ListenableFuture | 1.0.0.32 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.Jetbrains.Annotations | 26.1.0.2 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.JSpecify | 1.0.0.7 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.Kotlin.StdLib | 2.4.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.KotlinX.AtomicFU | 0.33.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.KotlinX.AtomicFU.Jvm | 0.33.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.KotlinX.Coroutines.Android | 1.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.KotlinX.Coroutines.Core | 1.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.KotlinX.Coroutines.Core.Jvm | 1.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.KotlinX.Serialization.Core | 1.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |
| Xamarin.KotlinX.Serialization.Core.Jvm | 1.11.0.1 | MIT AND Apache-2.0 | [`android-libraries-NOTICES.txt`](licenses/android-libraries-NOTICES.txt) |

Build-time only, not shipped in the app: `Microsoft.Maui.Controls.Build.Tasks` and `Microsoft.Maui.Resizetizer`
(MIT), and `Microsoft.NET.ILLink.Tasks` (MIT, part of the .NET SDK).

Test projects also use `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` and `coverlet.collector`
(MIT and Apache 2.0); they are never part of the app.
