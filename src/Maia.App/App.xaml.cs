using Maia.Application.Reminders;
using Maia.Presentation.Startup;

namespace Maia.App;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly PhoneReminders _reminders;
    private readonly Services.AppLockPresenter _lock;
    private readonly StartupFlow _startup;

    public App(PhoneReminders reminders, Services.AppLockPresenter appLock, StartupFlow startup)
    {
        _reminders = reminders;
        _lock = appLock;
        _startup = startup;

        // Her language choice (Settings) comes first: every text and font is picked from it.
        Services.AppLanguage.ApplySaved();
        InitializeComponent();

        // The v1 design is light only; a dark palette comes later, so the OS setting is not followed yet.
        UserAppTheme = AppTheme.Light;

        // Plaintext copies are never kept: files decrypted for another app, MAUI's copies of shared or picked files,
        // an unfiled camera photo and shared backups all go. Copies MAUI makes for sharing stay in the internal cache.
        Services.CacheFolders.KeepSharedCopiesInternal();
        Services.CacheFolders.ClearAll();

        // Styles pick fonts through these keys (DynamicResource), so Greek gets fonts with Greek glyphs.
        Services.AppLanguage.ApplyFonts(Resources);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // With her plan on this phone and nothing else to do first, Maia opens straight on Your week (see StartupFlow).
        // Starting locked, it shows the start-up screen behind the lock screen, which opens her plan once she unlocks.
        var openOnWeek = _startup.OpensOnWeek(returning: Views.StartupPage.ThenOpen is not null, locked: _lock.StartsLocked);
        var window = new Window(new AppShell(openOnWeek));

        // Reminders on this phone are set again in full on every start (after a restart only their plain words were).
        _ = _reminders.RearmAsync();

        // Back from another app's viewer (Android OnResume): the file she opened there is removed from Maia's cache.
        window.Activated += (_, _) => Services.CacheFolders.ClearAfterViewing();
#if ANDROID
        // Back in Maia, so unlocked: a plain "Maia reminder" left in the shade goes; the full reminder stays.
        window.Activated += (_, _) => Platforms.Android.ReminderAlarms.ClearPlain(global::Android.App.Application.Context);
#endif

        // "Lock Maia" (Settings): locked from the start and after a minute away, with her plan hidden meanwhile.
        _lock.Attach(window);
        return window;
    }
}
