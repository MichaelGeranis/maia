using System.Globalization;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Application.Reminders;
using Maia.Domain.Templates;
using Maia.Presentation.Add;
using Maia.Presentation.Calendar;
using Maia.Presentation.Care;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;
using Maia.Presentation.Privacy;
using Maia.Presentation.Results;
using Maia.Presentation.Settings;
using Maia.Presentation.Setup;
using Maia.Presentation.Startup;
using Maia.Presentation.Week;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Sets the UI culture for one test and restores it afterwards.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture;
    private readonly CultureInfo _uiCulture;

    public CultureScope(string name)
    {
        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}

internal sealed class RecordingNavigator : INavigator
{
    public List<string> Visited { get; } = [];

    /// <summary>Runs as each navigation happens, for a test that looks at the moment she leaves.</summary>
    public Action<string>? WhenVisiting { get; set; }

    public Task GoToStartupAsync() => Record("startup");

    public Task GoToSetupAsync() => Record("setup");

    public Task GoToSetupPlanAsync() => Record("setup-plan");

    public Task GoToSetupCareAsync() => Record("setup-care");

    public Task GoToMainAsync() => Record("main");

    public Task GoToPlanAsync() => Record("plan");

    public Task GoToResultsAsync() => Record("results");

    public Task GoToResultViewerAsync() => Record("viewer");

    public Task GoToSettingsAsync() => Record("settings");

    public Task GoToRestoreAsync() => Record("restore");

    public Task GoToAddAsync() => Record("add");

    public Task GoToQuestionsAsync() => Record("questions");

    public Task GoToTasksAsync() => Record("tasks");

    public Task GoToBreatheAsync() => Record("breathe");

    public Task GoToCalendarAsync() => Record("calendar");

    public Task GoToAboutAsync() => Record("about");

    public Task GoToPrivacyAsync() => Record("privacy");

    public Task GoToTermsAsync() => Record("terms");

    public Task GoToLicencesAsync() => Record("licences");

    public Task GoBackAsync() => Record("back");

    private Task Record(string route)
    {
        WhenVisiting?.Invoke(route);
        Visited.Add(route);
        return Task.CompletedTask;
    }
}

/// <summary>Keeps every toast, newest last: confirmations and calm notices alike in <see cref="Shown"/>, the notices also in <see cref="Notices"/>.</summary>
internal sealed class RecordingNotifier : INotifier
{
    public List<string> Shown { get; } = [];

    /// <summary>The toasts shown as calm notices (Sand, no tick): something did not happen.</summary>
    public List<string> Notices { get; } = [];

    /// <summary>Runs as each toast shows, for a test that changes the phone right after one.</summary>
    public Action<string>? WhenShown { get; set; }

    public void Show(string message)
    {
        Shown.Add(message);
        WhenShown?.Invoke(message);
    }

    public void ShowNotice(string message)
    {
        Notices.Add(message);
        Show(message);
    }
}

internal sealed class StubTemplateCatalog(PlanTemplate template) : IPlanTemplateCatalog
{
    public IReadOnlyList<string> SupportedCountries => [template.CountryCode];

    public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken) => Task.FromResult(template);
}

/// <summary>Wires the real application services over in-memory fakes.</summary>
internal sealed class TestApp : IDisposable
{
    private ShareIntake? _shares;
    private AppLock? _appLock;
    private ResultViewing? _viewing;
    private SetupSession? _setup;

    public TestApp(PlanTemplate? template = null)
    {
        Templates = new StubTemplateCatalog(template ?? TestData.Template());

        Reminders = new PhoneReminders(ReminderScheduler, ReminderSettings, Repository, Clock, Errors);
        Plans = new PlanService(Repository, Templates, Attachments, DataKey, Backups, Clock, Errors, Reminders);
        Queries = new PlanQueries(Repository, Clock);
    }

    public RecordingDataKey DataKey { get; } = new();

    public PhoneReminders Reminders { get; }

    public FakeReminderScheduler ReminderScheduler { get; } = new();

    public InMemoryReminderSettingsStore ReminderSettings { get; } = new();

    public FakeNotificationAccess NotificationAccess { get; } = new();

    public PhoneRemindersSetting CreateRemindersSetting() => new(Reminders, NotificationAccess, Errors);

    public RecordingNotifier Notifier { get; } = new();

    public PlanFocus Focus { get; } = new();

    /// <summary>The group Your tasks opens at, as Your week asks for it.</summary>
    public TasksFocus TasksFocus { get; } = new();

    public FakeContactPicker Contacts { get; } = new();

    public RecordingDialer Dialer { get; } = new();

    public FakeFilePicker Files { get; } = new();

    public RecordingViewer Viewer { get; } = new();

    public FakePhotoProcessor Photos { get; } = new();

    public FakeResultRenderer Renderer { get; } = new();

    public ResultViewing Viewing => _viewing ??= new(Renderer, Navigator);

    public ResultFiling Filing => new(Plans, Files, Photos, Viewer, Viewing, Notifier, Clock, Errors);

    public ShareIntake Shares => _shares ??= new(Queries, Navigator, Notifier);

    /// <summary>The phone's list of choices: which entry to tag a result to.</summary>
    public FakeChooser Chooser { get; } = new();

    public ResultsViewModel CreateResults() => new(Queries, Filing, Shares, Navigator, Errors, new ResultThumbnails(Plans, Photos, Renderer, Errors));

    public InMemoryDraftStore Drafts { get; } = new();

    /// <summary>Her setup choices, kept in <see cref="Drafts"/> as on the phone.</summary>
    public SetupSession Setup => _setup ??= new(Drafts, Errors);

    public FakeClock Clock { get; } = new(TestData.Today);

    public InMemoryPlanRepository Repository { get; } = new();

    public RecordingErrorReporter Errors { get; } = new();

    public InMemoryAttachmentStore Attachments { get; } = new();

    public InMemoryBackupFormat Backups { get; } = new();

    public FakeBackupFiles BackupFiles { get; } = new();

    public FakeBackupHistory BackupHistory { get; } = new();

    public FakeDiagnostics Diagnostics { get; } = new("Maia 1.0 (12)");

    public RecordingNavigator Navigator { get; } = new();

    public IPlanTemplateCatalog Templates { get; }

    public PlanService Plans { get; }

    public PlanQueries Queries { get; }

    public FakeScreenLock ScreenLock { get; } = new();

    public InMemoryAppLockPreference LockPreference { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public AppLock AppLock => _appLock ??= new(LockPreference, ScreenLock, Time);

    public SettingsViewModel CreateSettings(RecordingLanguageSwitcher? languages = null) => new(
        Plans, languages ?? new RecordingLanguageSwitcher(), Viewer, Setup, BackupFiles, Clock, Navigator, Notifier, Errors,
        BackupHistory, CreateRemindersSetting(), new DueDateSettings(Queries, Plans, Clock, Notifier, Errors),
        new AppLockSettings(AppLock, Notifier, Errors), new DiagnosticsViewModel(Diagnostics, Notifier, Errors), new AnimationSetting(Animations),
        new Care.TeamViewModel(Queries, Plans, Contacts, Dialer, Chooser, Notifier, Errors));

    /// <summary>Her "Animations" choice, kept in memory.</summary>
    public FakeAnimationPreference Animations { get; } = new();

    /// <summary>Setup step 3: her OB-GYN and "Remind me".</summary>
    public SetupCareViewModel CreateSetupCare() => new(Setup, Plans, Contacts, Reminders, NotificationAccess, Navigator, Notifier, Errors);

    public WeekViewModel CreateWeek() => new(Queries, Plans, Clock, Navigator, Notifier, Errors, BackupHistory, Focus);

    /// <summary>Your tasks, opened from Your week.</summary>
    public CalendarViewModel CreateCalendar() => new(Queries, Clock, Navigator);

    public TasksViewModel CreateTasks() => new(Queries, Plans, Clock, Navigator, Notifier, Errors, TasksFocus);

    /// <summary>"Add to your journey", as the Add slot and Journey's Add open it.</summary>
    public AddEntryViewModel CreateAdd() => new(Queries, Plans, Clock, Navigator, Notifier, Errors);

    /// <summary>How Maia starts, over this phone: a plan is "stored" once the repository holds one.</summary>
    public StartupFlow CreateStartupFlow() => new(
        () => Repository.Stored is not null, Shares, new PlanLanguageSync(Plans, Errors), new PlanUpkeep(Plans, Setup, Notifier, Errors), Plans, Errors);

    /// <summary>
    /// A plan she has had since early on, as most plans on phones are: every check of the template, the ones whose
    /// weeks have passed too. (Setup today leaves those out: see <see cref="CreatePlanThroughSetupAsync"/>.)
    /// </summary>
    public async Task CreatePlanAsync()
    {
        var template = await Templates.GetAsync("GR", CancellationToken.None);
        var dating = Domain.Pregnancy.PregnancyDating.ForToday(TestData.DueDate, Clock.Today);
        await Repository.SaveAsync(Domain.Plans.PregnancyPlan.Create(Domain.Common.PlanId.New(), dating, template, "en", Clock.UtcNow, timeZoneId: HomeTimeZone.IdOf(Clock.TimeZone)), CancellationToken.None);
    }

    /// <summary>A plan made today, as setup makes it.</summary>
    public Task CreatePlanThroughSetupAsync() =>
        Plans.CreatePlanAsync(new CreatePlanRequest(TestData.DueDate, "GR", "en"));

    public void Dispose() => Plans.Dispose();
}

/// <summary>Hands back the contacts the test queued, as if picked in the phone's picker; null means cancelled.</summary>
internal sealed class FakeContactPicker : IContactPicker
{
    public Queue<PickedContact?> Picks { get; } = new();

    /// <summary>A contact picked just as Android closed Maia, handed over once.</summary>
    public PickedContact? Interrupted { get; set; }

    public Task<PickedContact?> PickAsync() => Task.FromResult(Picks.Count > 0 ? Picks.Dequeue() : null);

    public Task<PickedContact?> TakeInterruptedPickAsync()
    {
        var picked = Interrupted;
        Interrupted = null;
        return Task.FromResult(picked);
    }
}

internal sealed class RecordingDialer : IDialer
{
    public List<string> Dialled { get; } = [];

    public void Dial(string number) => Dialled.Add(number);
}

/// <summary>
/// Hands back the files the test queued, as if picked; nothing queued means she cancelled. Photos queued in
/// <see cref="Photos"/> come from the camera; a null photo means she closed the camera without taking one.
/// </summary>
internal sealed class FakeFilePicker : IResultPicker
{
    public Queue<IReadOnlyList<PickedFile>> Picks { get; } = new();

    public Queue<PickedFile?> Photos { get; } = new();

    public bool CanTakePhoto { get; set; } = true;

    /// <summary>The phone has no camera app, or its camera is switched off.</summary>
    public bool CameraUnavailable { get; set; }

    public Task<IReadOnlyList<PickedFile>> PickAsync() => Task.FromResult(Picks.Count > 0 ? Picks.Dequeue() : []);

    public Task<PickedFile?> TakePhotoAsync() =>
        CameraUnavailable ? throw new CameraUnavailableException() : Task.FromResult(Photos.Count > 0 ? Photos.Dequeue() : null);
}

/// <summary>Records what would be opened in the phone's viewer, or handed to its share sheet.</summary>
internal sealed class RecordingViewer : IResultViewer
{
    public List<string> Opened { get; } = [];

    public Task OpenFileAsync(string name, string mediaType, byte[] content)
    {
        Opened.Add($"{name} ({mediaType}, {content.Length} bytes)");
        return Task.CompletedTask;
    }

    /// <summary>Each share sheet opened: what each file was called and how big it was once read.</summary>
    public List<IReadOnlyList<string>> Shared { get; } = [];

    /// <summary>Makes the next share fail, as a phone without a share sheet would.</summary>
    public Exception? FailNextShare { get; set; }

    public async Task ShareFilesAsync(IReadOnlyList<ResultToShare> results)
    {
        if (FailNextShare is { } failure)
        {
            FailNextShare = null;
            throw failure;
        }

        var files = new List<string>(results.Count);
        foreach (var result in results)
        {
            files.Add($"{result.Name} ({result.MediaType}, {(await result.ReadAsync()).Length} bytes)");
        }

        Shared.Add(files);
    }

    public int Cleared { get; private set; }

    public void ClearCopies() => Cleared++;
}

internal sealed class RecordingLanguageSwitcher : ILanguageSwitcher
{
    public List<string> Switched { get; } = [];

    public LanguageReturn? ReturnedTo { get; private set; }

    public Task SwitchAsync(string language, LanguageReturn returnTo = LanguageReturn.Settings)
    {
        Switched.Add(language);
        ReturnedTo = returnTo;
        return Task.CompletedTask;
    }
}

/// <summary>The phone's permission for notifications, which the test grants or refuses.</summary>
internal sealed class FakeNotificationAccess : INotificationAccess
{
    public bool IsSupported { get; set; } = true;

    public bool Granted { get; set; }

    /// <summary>What she answers when asked.</summary>
    public bool WillGrant { get; set; } = true;

    public int Requests { get; private set; }

    public int SettingsOpened { get; private set; }

    public Task<bool> IsGrantedAsync() => Task.FromResult(Granted);

    public Task<bool> RequestAsync()
    {
        Requests++;
        Granted = Granted || WillGrant;
        return Task.FromResult(Granted);
    }

    public void OpenAppSettings() => SettingsOpened++;
}

/// <summary>When she last made a backup and put the reminder away, as the app keeps them in its preferences.</summary>
internal sealed class FakeBackupHistory : IBackupHistory
{
    public DateTimeOffset? LastBackup { get; set; }

    public DateTimeOffset? ReminderDismissed { get; set; }

    /// <summary>Makes noting a backup throw this, as when the phone's preferences can't be written.</summary>
    public Exception? FailBackupMade { get; set; }

    public void BackupMade(DateTimeOffset at) => LastBackup = FailBackupMade is { } failure ? throw failure : at;

    public void DismissReminder(DateTimeOffset at) => ReminderDismissed = at;

    public void Forget()
    {
        LastBackup = null;
        ReminderDismissed = null;
    }
}

/// <summary>The problem log, as the lines the test adds, and what was handed to the share sheet.</summary>
internal sealed class FakeDiagnostics(string header) : IDiagnostics
{
    public List<string> Entries { get; } = [];

    public List<DiagnosticsReport> Shared { get; } = [];

    public bool FailShare { get; set; }

    public bool FailRead { get; set; }

    public Task<DiagnosticsReport> ReadAsync() => FailRead
        ? Task.FromException<DiagnosticsReport>(new IOException("log unreadable"))
        : Task.FromResult(new DiagnosticsReport(header, Entries.ToList()));

    public Task ShareAsync(DiagnosticsReport report)
    {
        if (FailShare)
        {
            return Task.FromException(new IOException("no app to share with"));
        }

        Shared.Add(report);
        return Task.CompletedTask;
    }
}

internal sealed class FakeBackupFiles : IBackupFiles
{
    public List<(string Name, byte[] Content)> Shared { get; } = [];

    public BackupFile? NextPick { get; set; }

    /// <summary>Makes the next pick throw this, as a cloud file provider can when it is offline.</summary>
    public Exception? FailPick { get; set; }

    /// <summary>When set, writing the backup waits for it, as encrypting a large plan takes a few seconds.</summary>
    public TaskCompletionSource? Hold { get; set; }

    public async Task ShareAsync(string fileName, Func<Stream, Task> write)
    {
        if (Hold is { } hold)
        {
            await hold.Task;
        }

        using var file = new MemoryStream();
        await write(file);
        Shared.Add((fileName, file.ToArray()));
    }

    public Task<BackupFile?> PickAsync()
    {
        if (FailPick is { } failure)
        {
            FailPick = null;
            return Task.FromException<BackupFile?>(failure);
        }

        return Task.FromResult(NextPick);
    }
}

/// <summary>
/// The phone's photo work, as the test sets it up: a photo it knows the size of can be measured (others can't be
/// read), a new JPEG is a small stand-in, and wiping the location is recorded.
/// </summary>
internal sealed class FakePhotoProcessor : IPhotoProcessor
{
    public Dictionary<string, PhotoSize> Sizes { get; } = [];

    public List<string> Measured { get; } = [];

    public List<(string Name, int LongEdge, int Quality)> Reencoded { get; } = [];

    public int LocationsWiped { get; private set; }

    public Task<PhotoSize?> MeasureAsync(PickedFile photo)
    {
        Measured.Add(photo.Name);
        return Task.FromResult<PhotoSize?>(Sizes.TryGetValue(photo.Name, out var size) ? size : null);
    }

    public Task<byte[]> ToJpegAsync(PickedFile photo, int longEdge, int quality)
    {
        Reencoded.Add((photo.Name, longEdge, quality));
        return Task.FromResult(new byte[300 * 1024]);
    }

    public byte[] WithoutLocation(string mediaType, byte[] photo)
    {
        LocationsWiped++;
        return photo;
    }
}

/// <summary>A stream that can only be read forward, like a file from another app, and counts what was read.</summary>
internal sealed class ForwardOnlyStream(long length) : Stream
{
    private long _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = (int)Math.Min(count, length - _position);
        _position += read;
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>The phone's drawing of result files: which photos it can show, and the PDF it opens next.</summary>
internal sealed class FakeResultRenderer : IResultRenderer
{
    /// <summary>Android 9 and later draw HEIC photos; older phones don't.</summary>
    public bool DrawsHeic { get; set; } = true;

    /// <summary>The page shapes of the next PDF opened; null when the phone can't draw it (password or damage).</summary>
    public IReadOnlyList<double>? NextPdf { get; set; } = [1.414, 1.414];

    public FakePdf? Opened { get; private set; }

    /// <summary>When set, opening a PDF takes as long as the test chooses.</summary>
    public SlowStep? SlowOpen { get; set; }

    /// <summary>When set, each page of the next PDF is drawn but handed back only when the test chooses.</summary>
    public SlowStep? SlowDrawing { get; set; }

    public bool CanShowImage(string mediaType) => mediaType is not ("image/heic" or "image/heif") || DrawsHeic;

    public async Task<IPdfPages?> OpenPdfAsync(byte[] pdf)
    {
        if (SlowOpen is { } slow)
        {
            await slow.WaitAsync();
        }

        return NextPdf is { } ratios ? Opened = new FakePdf(ratios, SlowDrawing) : null;
    }
}

/// <summary>An open PDF that records what was drawn; each page is a one-byte stand-in for its picture.</summary>
internal sealed class FakePdf(IReadOnlyList<double> ratios, SlowStep? slow = null) : IPdfPages
{
    public IReadOnlyList<double> Ratios => ratios;

    public List<(int Index, int Width)> Rendered { get; } = [];

    public bool Disposed { get; private set; }

    public async Task<byte[]> RenderAsync(int index, int width)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        Rendered.Add((index, width));
        if (slow is not null)
        {
            // Like the phone's picture compression, which finishes after the PDF has let go of the page.
            await slow.WaitAsync();
        }

        return [(byte)(index + 1)];
    }

    public void Dispose() => Disposed = true;
}

/// <summary>A step the phone takes its time over: the test sees it start, then lets it finish when it chooses.</summary>
internal sealed class SlowStep
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public void Finish() => _finished.TrySetResult();

    public Task WaitAsync()
    {
        _started.TrySetResult();
        return _finished.Task;
    }
}

/// <summary>The phone's screen lock, as the test sets it up: whether one is set, and what she does at its prompt.</summary>
internal sealed class FakeScreenLock : IScreenLock
{
    public bool IsSet { get; set; } = true;

    /// <summary>Whether she confirms her screen lock at the next prompts.</summary>
    public bool WillConfirm { get; set; } = true;

    /// <summary>When set, the prompt stays up until the test finishes this step.</summary>
    public SlowStep? Slow { get; set; }

    /// <summary>The title of every prompt shown, oldest first.</summary>
    public List<string> Prompts { get; } = [];

    public int SettingsOpened { get; private set; }

    public async Task<bool> ConfirmAsync(string title, string subtitle)
    {
        Prompts.Add(title);
        if (Slow is { } slow)
        {
            await slow.WaitAsync();
        }

        return WillConfirm;
    }

    public void OpenSettings() => SettingsOpened++;
}

internal sealed class InMemoryAppLockPreference : IAppLockPreference
{
    public bool IsOn { get; set; }
}

/// <summary>A monotonic clock the test moves by hand.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}

/// <summary>Her "Animations" choice, as the phone's preferences keep it: on until she turns it off.</summary>
public sealed class FakeAnimationPreference : IAnimationPreference
{
    public bool AnimationsOn { get; set; } = true;
}
