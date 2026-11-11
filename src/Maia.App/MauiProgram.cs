using Maia.App.Services;
using Maia.App.Views;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Infrastructure;
using Maia.Infrastructure.Attachments;
using Maia.Infrastructure.Persistence;
using Maia.Infrastructure.Security;
using Maia.Infrastructure.Templates;
using Maia.Presentation.Calendar;
using Maia.Presentation.Calm;
using Maia.Presentation.Care;
using Maia.Presentation.About;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Plan;
using Maia.Presentation.Privacy;
using Maia.Presentation.Questions;
using Maia.Presentation.Results;
using Maia.Presentation.Settings;
using Maia.Presentation.Setup;
using Maia.Presentation.Startup;
using Maia.Presentation.Add;
using Maia.Presentation.Week;
using Microsoft.Extensions.Logging;

namespace Maia.App;

/// <summary>Composition root: the only place that knows every concrete type.</summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // One rounded family for English and Greek alike (subset to Latin, Greek and Cyrillic); see Typography.
                fonts.AddFont("MPLUSRounded1c-Regular.ttf", "RoundedRegular");
                fonts.AddFont("MPLUSRounded1c-Medium.ttf", "RoundedMedium");
                fonts.AddFont("MPLUSRounded1c-Bold.ttf", "RoundedBold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
                // A line icon is a shape of its own (one platform view each), drawn like any other shape.
                handlers.AddHandler<Controls.Icon, Microsoft.Maui.Handlers.ShapeViewHandler>();

                // Cards, chips and dots in long lists draw as plain rounded rectangles, with no layer or clip (see LightBorder).
#if ANDROID
                handlers.AddHandler<Controls.LightBorder, Controls.LightBorderHandler>();

                // Enter in a field does the field's return and keeps the focus where it is (see FieldHandler).
                handlers.AddHandler<Entry, Platforms.Android.FieldHandler>();

                // The calendar a date field opens speaks Maia's Greek, not only the phone's language (see DateFieldHandler).
                handlers.AddHandler<DatePicker, Platforms.Android.DateFieldHandler>();
#else
                handlers.AddHandler<Controls.LightBorder, Microsoft.Maui.Handlers.BorderHandler>();
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var dataDirectory = FileSystem.AppDataDirectory;
        AddCore(builder.Services, dataDirectory);
        AddScreens(builder.Services, dataDirectory);
        RemoveNativeUnderlines();
        KeepAccessibleRoles();
        ShowTheYearOnDateFields();
        BreakLongWordsWhereTheyMay();
        var app = builder.Build();
        CrashReporting.Register(app.Services.GetRequiredService<IErrorReporter>());
        WarmUpDataKey(app.Services, dataDirectory);
        return app;
    }

    private static string PlanDirectory(string dataDirectory) => Path.Combine(dataDirectory, "plan");

    private static string ResultsDirectory(string dataDirectory) => Path.Combine(dataDirectory, "results");

    /// <summary>Whether any file encrypted with the data key is on the phone (her plan or a result).</summary>
    private static bool HasEncryptedFiles(string dataDirectory) =>
        EncryptedPlanRepository.HasStoredPlan(PlanDirectory(dataDirectory)) || EncryptedAttachmentStore.HasStoredFiles(ResultsDirectory(dataDirectory));

    /// <summary>
    /// The keystore's first read is often the slowest step of a cold start, so it starts now, in the background,
    /// alongside building the first screen, rather than when her plan is first opened. Only when her files are there to
    /// open: a new key is never made here. A failure is left for the plan's own load to meet and report.
    /// </summary>
    private static void WarmUpDataKey(IServiceProvider services, string dataDirectory)
    {
        if (!HasEncryptedFiles(dataDirectory))
        {
            return;
        }

        var keys = services.GetRequiredService<DataKeyProvider>();
        _ = Task.Run(async () =>
        {
            try
            {
                await keys.GetKeyAsync(CancellationToken.None);
            }
#pragma warning disable CA1031 // The plan's load meets the same failure and reports it with its own code.
            catch (Exception)
            {
            }
#pragma warning restore CA1031
        });
    }

    /// <summary>
    /// Fields sit in the design's rounded frames (the Field styles), so Android's own underline goes, and a picker keeps
    /// to one line inside its frame.
    /// </summary>
    private static void RemoveNativeUnderlines()
    {
#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => handler.PlatformView.Background = null);
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => handler.PlatformView.Background = null);
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => handler.PlatformView.Background = null);
        Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => handler.PlatformView.Background = null);
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => handler.PlatformView.Background = null);

        // A check's name that doesn't fit the "File under" pill ends in "…" instead of being cut mid-word at its edge.
        // One line at any text size: the pill grows with the text, and the full name is in the list it opens.
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("OneLine", (handler, _) =>
        {
            handler.PlatformView.SetSingleLine(true);
            handler.PlatformView.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        });
#endif
    }

    /// <summary>
    /// A word too long for its line (Greek at a large text size: "Υπερηχογράφημα", "χρειάζεται") breaks where a word
    /// may, with a hyphen, in the language Maia shows, instead of at whatever letter reached the edge. Android stopped
    /// doing this by itself in Android 13, and took the phone's language, not Maia's, before.
    /// </summary>
    private static void BreakLongWordsWhereTheyMay()
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return;
        }

        static void Hyphenate(Android.Widget.TextView view)
        {
            view.TextLocale = Java.Util.Locale.ForLanguageTag(Text.Language);
            view.BreakStrategy = Android.Text.BreakStrategy.HighQuality;
            view.HyphenationFrequency = Android.Text.HyphenationFrequency.Normal;
        }

        Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping("Hyphenate", (handler, _) => Hyphenate(handler.PlatformView));
        Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("Hyphenate", (handler, _) => Hyphenate(handler.PlatformView));
#endif
    }

    /// <summary>
    /// A drawn control's role and state (Controls/Accessible) go back on after MAUI maps its description, which it does
    /// when the control first shows and whenever the description changes.
    /// </summary>
    private static void KeepAccessibleRoles() =>
        Microsoft.Maui.Handlers.ViewHandler.ViewMapper.AppendToMapping(nameof(IView.Semantics), (_, view) =>
        {
            if (view is VisualElement element)
            {
                Controls.Accessible.Apply(element);
            }
        });

    /// <summary>
    /// A date field reads "Sat 3 Oct" as everywhere else, with the year once its day is far from today: "Fri 3 Oct"
    /// alone would read as this year's (see <see cref="Presentation.Formatting.DisplayFormat.DateFieldFormat"/>).
    /// </summary>
    private static void ShowTheYearOnDateFields() =>
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping(nameof(IDatePicker.Date), (_, view) =>
        {
            if (view is DatePicker { Date: DateTime date } picker)
            {
                var today = IPlatformApplication.Current?.Services.GetService<IClock>()?.Today ?? DateOnly.FromDateTime(DateTime.Today);
                picker.Format = Presentation.Formatting.DisplayFormat.DateFieldFormat(DateOnly.FromDateTime(date), today);
            }
        });

    private static void AddCore(IServiceCollection services, string dataDirectory)
    {
        var planDirectory = PlanDirectory(dataDirectory);
        var resultsDirectory = ResultsDirectory(dataDirectory);

        services.AddSingleton<IClock>(new SystemClock());

        // One key for the plan and every result file. A new key is made only while none of her files exists.
        services.AddSingleton(_ => new DataKeyProvider(new SecureStorageSecretStore(), () => HasEncryptedFiles(dataDirectory)));
        services.AddSingleton<IKeyProvider>(sp => sp.GetRequiredService<DataKeyProvider>());
        services.AddSingleton<IDataKey>(sp => sp.GetRequiredService<DataKeyProvider>());
        services.AddSingleton<AesGcmCipher>();
        services.AddSingleton<IPlanRepository>(sp =>
            new EncryptedPlanRepository(planDirectory, sp.GetRequiredService<AesGcmCipher>(), sp.GetRequiredService<IErrorReporter>()));
        services.AddSingleton<IPlanBackupFormat, EncryptedBackupFormat>();
        services.AddSingleton<IAttachmentStore>(sp =>
            new EncryptedAttachmentStore(resultsDirectory, sp.GetRequiredService<AesGcmCipher>()));
        services.AddSingleton<IPlanTemplateCatalog>(new EmbeddedPlanTemplateCatalog(new TemplateCatalogOptions
        {
            // Store builds never show clinical content that has not been reviewed (ADR 0003). Debug builds, and a
            // personal Release APK built with -p:MaiaAllowDraftTemplates=true (never distributed; MaiaStoreGate in
            // the project file refuses it for an AAB, CI, iOS and Windows), may use Draft templates.
#if DEBUG || MAIA_ALLOW_DRAFT_TEMPLATES
            AllowDraftTemplates = true,
#else
            AllowDraftTemplates = false,
#endif
        }));

        // Setup choices until her plan is made, so Android closing Maia in the background loses none of them.
        services.AddSingleton<IDraftStore>(sp => new EncryptedDraftStore(Path.Combine(dataDirectory, "drafts"), sp.GetRequiredService<AesGcmCipher>()));
        // The problem log: codes and types only, kept in every build, shared only from Settings when she chooses.
        services.AddSingleton(new DiagnosticsFile(Path.Combine(dataDirectory, "diagnostics")));
        services.AddSingleton<IDiagnostics>(sp => sp.GetRequiredService<DiagnosticsFile>());
        services.AddSingleton<IErrorReporter, LoggerErrorReporter>();

        // Reminders on this phone, set again after each change (PlanService), on start (App) and after a restart.
        services.AddSingleton<IReminderSettingsStore>(new ReminderPreferences(new MauiPreferenceValues()));
#if ANDROID
        services.AddSingleton<IReminderScheduler, Platforms.Android.AndroidReminderScheduler>();
#else
        services.AddSingleton<IReminderScheduler, NoReminderScheduler>();
#endif
        services.AddSingleton<PhoneReminders>();
        services.AddSingleton<PlanService>();
        services.AddSingleton<PlanQueries>();
    }

    private static void AddScreens(IServiceCollection services, string dataDirectory)
    {
        // One navigator, so the bottom bar and every screen share its gate.
        services.AddSingleton<ShellNavigator>();
        services.AddSingleton<INavigator>(sp => sp.GetRequiredService<ShellNavigator>());
        services.AddSingleton(sp => new SetupSession(sp.GetRequiredService<IDraftStore>(), sp.GetRequiredService<IErrorReporter>()));
        services.AddSingleton<PlanFocus>();

        // The group Your tasks opens at, as Your week asks for it.
        services.AddSingleton<TasksFocus>();
        services.AddSingleton<ShareIntake>();
        services.AddSingleton<PlanUpkeep>();
        services.AddSingleton(sp => new StartupFlow(
            () => EncryptedPlanRepository.HasStoredPlan(PlanDirectory(dataDirectory)),
            sp.GetRequiredService<ShareIntake>(),
            sp.GetRequiredService<PlanLanguageSync>(),
            sp.GetRequiredService<PlanUpkeep>(),
            sp.GetRequiredService<PlanService>(),
            sp.GetRequiredService<IErrorReporter>()));
        // Page events load screens through it (see ScreenWork): a failure is said calmly and never ends Maia.
        services.AddSingleton<ScreenLoader>();
        services.AddSingleton<IContactPicker, MauiContactPicker>();
        services.AddSingleton<IDialer, MauiPhoneDialer>();
        services.AddSingleton<IChooser, ChoiceSheet>();
        services.AddSingleton<IScreenAwake, DeviceScreenAwake>();
        services.AddSingleton<IAnnouncer, ScreenReaderAnnouncer>();

        // Breathe's gentle vibration (on by default) and soft tones (off by default), and her choice of each.
        services.AddSingleton<IBreatheSettings, PreferencesBreatheSettings>();
#if ANDROID
        services.AddSingleton<IBreathVibration, Platforms.Android.AndroidBreathVibration>();
        services.AddSingleton<IBreathSound, Platforms.Android.AndroidBreathSound>();
#else
        services.AddSingleton<IBreathVibration, NoBreathVibration>();
        services.AddSingleton<IBreathSound, NoBreathSound>();
#endif
        services.AddSingleton<IResultPicker, MauiFilePicker>();
#if ANDROID
        services.AddSingleton<IPhotoProcessor, Platforms.Android.AndroidPhotoProcessor>();
#else
        services.AddSingleton<IPhotoProcessor, PlainPhotoProcessor>();
#endif
        services.AddSingleton<IResultViewer, MauiResultViewer>();
#if ANDROID
        services.AddSingleton<IResultRenderer, Platforms.Android.AndroidResultRenderer>();
#else
        services.AddSingleton<IResultRenderer, PlainResultRenderer>();
#endif
        services.AddSingleton<ResultViewing>();
        services.AddSingleton<ResultThumbnails>();
        services.AddSingleton<ILanguageSwitcher, AppLanguage>();
        services.AddSingleton<IBackupFiles, MauiBackupFiles>();
        services.AddSingleton<IBackupHistory, PreferencesBackupHistory>();
        services.AddSingleton<INotificationAccess, MauiNotificationAccess>();
        services.AddTransient<PhoneRemindersSetting>();
        services.AddSingleton<IAnimationPreference, PreferencesAnimationPreference>();
        services.AddTransient<AnimationSetting>();
        services.AddTransient<ResultFiling>();
        services.AddTransient<PlanLanguageSync>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<INotifier>(sp => sp.GetRequiredService<ToastService>());

        services.AddTransient<StartupViewModel>();
        services.AddTransient<StartupPage>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<SetupPage>();
        services.AddTransient<SetupPlanViewModel>();
        services.AddTransient<SetupPlanPage>();
        services.AddTransient<SetupCareViewModel>();
        services.AddTransient<SetupCarePage>();
        services.AddTransient<WeekViewModel>();
        services.AddTransient<WeekPage>();
        // Journey's "Tasks" switch, kept on this phone (on until she turns it off).
        services.AddSingleton<IJourneySettings, PreferencesJourneySettings>();
        services.AddTransient<PlanViewModel>();
        services.AddTransient<PlanPage>();
        services.AddTransient<ResultsViewModel>();
        services.AddTransient<ResultsPage>();
        services.AddTransient<TeamViewModel>();
        services.AddTransient<AddEntryViewModel>();
        services.AddTransient<AddEntryPage>();
        services.AddTransient<QuestionsViewModel>();
        services.AddTransient<QuestionsPage>();
        services.AddTransient<TasksViewModel>();
        services.AddTransient<TasksPage>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<CalendarPage>();
        services.AddTransient<BreatheViewModel>();
        services.AddTransient<BreathePage>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<RestoreViewModel>();
        services.AddTransient<RestorePage>();
        services.AddTransient<ResultViewerViewModel>();
        services.AddTransient<ResultViewerPage>();
        services.AddTransient<DueDateSettings>();
        services.AddTransient<AppLockSettings>();
        services.AddSingleton<IAppVersion, MauiAppVersion>();
        services.AddSingleton<ILicenceTexts, BundledLicenceTexts>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<AboutPage>();
        services.AddTransient<PrivacyViewModel>();
        services.AddTransient<PrivacyPage>();
        services.AddTransient<TermsViewModel>();
        services.AddTransient<TermsPage>();
        services.AddTransient<LicencesViewModel>();
        services.AddTransient<LicencesPage>();

        // "Lock Maia with your screen lock" (off by default): the lock, its screen, and what shows it.
        services.AddSingleton<IAppLockPreference, PreferencesAppLockPreference>();
#if ANDROID
        services.AddSingleton<IScreenLock, Platforms.Android.AndroidScreenLock>();
#else
        services.AddSingleton<IScreenLock, NoScreenLock>();
#endif
        services.AddSingleton(sp => new AppLock(sp.GetRequiredService<IAppLockPreference>(), sp.GetRequiredService<IScreenLock>(), new BootClock()));
        services.AddSingleton<AppLockPresenter>();
        services.AddTransient<LockViewModel>();
        services.AddTransient<LockView>();
    }
}
