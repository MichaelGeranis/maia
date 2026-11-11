using Maia.Application.Common;
using Maia.Domain.Common;
using Maia.Presentation.Common;
using Maia.Presentation.Settings;
using Maia.Presentation.Startup;

namespace Maia.Presentation.Tests;

/// <summary>
/// The last lines of defence on a screen: a reload or a hand-over that fails is said calmly or only reported, and every
/// failure gets words chosen by its code alone.
/// </summary>
public sealed class ScreenFailureTests : IDisposable
{
    private const string CouldNotShow = "Maia couldn't show this just now. Try again in a moment.";

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Opened_straight_on_your_week_a_reload_after_renaming_that_fails_is_said_calmly()
    {
        await _app.CreatePlanAsync();
        using var greek = new CultureScope("el-GR");
        var startup = _app.CreateStartupFlow();
        startup.OpensOnWeek(returning: false);
        var loader = new ScreenLoader(startup, _app.Navigator, _app.Notifier, _app.Errors);
        var week = _app.CreateWeek();
        var loads = 0;

        var loaded = await loader.LoadAsync(
            () =>
            {
                loads++;
                return loads == 1 ? week.LoadAsync() : throw new IOException("storage not ready");
            },
            "week.load");
        await startup.Tidying;

        Assert.True(loaded);
        Assert.Equal(2, loads);
        Assert.Equal(Localization.Text.Get("Common_LoadError"), Assert.Single(_app.Notifier.Notices));
        Assert.Equal("week.load", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Empty(_app.Navigator.Visited);
    }

    [Fact]
    public async Task A_hand_over_to_start_up_that_fails_is_only_reported_by_its_code()
    {
        await _app.CreatePlanAsync();
        _app.Repository.Unreadable = PlanUnreadableException.Damaged(new InvalidDataException("both copies"));
        _app.Navigator.WhenVisiting = _ => throw new InvalidOperationException("no shell yet");
        var loader = new ScreenLoader(_app.CreateStartupFlow(), _app.Navigator, _app.Notifier, _app.Errors);

        var loaded = await loader.LoadAsync(_app.CreateWeek().LoadAsync, "week.load");

        Assert.False(loaded);
        Assert.Equal(new[] { "week.load", "week.load" }, _app.Errors.Reports.Select(r => r.Operation));
        Assert.IsType<InvalidOperationException>(_app.Errors.Reports[1].Exception);
        Assert.Empty(_app.Notifier.Shown);
    }

    [Fact]
    public async Task Other_work_from_a_page_event_that_works_reports_nothing()
    {
        var loader = new ScreenLoader(_app.CreateStartupFlow(), _app.Navigator, _app.Notifier, _app.Errors);
        var ran = false;

        await loader.RunAsync(
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            "week.keyboard");

        Assert.True(ran);
        Assert.Empty(_app.Errors.Reports);
    }

    [Fact]
    public async Task Start_up_that_cannot_keep_the_home_time_zone_reports_it_and_carries_on()
    {
        await _app.CreatePlanAsync();
        _app.Setup.DueDate = new DateOnly(2027, 1, 17);
        await _app.Setup.SaveAsync(Setup.SetupStep.DueDate);
        _app.Repository.FailNextLoad = new IOException("storage not ready");
        var upkeep = new PlanUpkeep(_app.Plans, _app.Setup, _app.Notifier, _app.Errors);

        var reworded = await upkeep.RunAsync();

        Assert.False(reworded);
        Assert.Equal("startup.time_zone", Assert.Single(_app.Errors.Reports).Operation);
        Assert.Empty(_app.Drafts.Drafts);
    }

    [Theory]
    [MemberData(nameof(BackupFailures))]
    public void A_backup_that_cannot_be_opened_says_why_wherever_it_shows(string code, string key)
    {
        var words = Localization.Text.Get(key);
        var error = code switch
        {
            BackupUnreadableException.WrongPasswordCode => BackupUnreadableException.WrongPassword(new InvalidDataException("tag")),
            BackupUnreadableException.NotABackupCode => BackupUnreadableException.NotABackup(),
            BackupUnreadableException.NewerAppCode => BackupUnreadableException.FromNewerApp(),
            _ => BackupUnreadableException.Damaged(),
        };

        Assert.Equal(words, BackupMessages.For(error));
        Assert.Equal(words, ErrorMessages.For(error));
    }

    public static TheoryData<string, string> BackupFailures() => new()
    {
        { BackupUnreadableException.WrongPasswordCode, "Backup_WrongPassword" },
        { BackupUnreadableException.NotABackupCode, "Backup_NotABackup" },
        { BackupUnreadableException.NewerAppCode, "Backup_NewerApp" },
        { BackupUnreadableException.DamagedCode, "Backup_Damaged" },
    };

    [Fact]
    public void A_backup_failure_with_no_code_of_its_own_says_the_backup_is_damaged() =>
        Assert.Equal(Localization.Text.Get("Backup_Damaged"), BackupMessages.For(new BackupUnreadableException("unknown")));

    [Fact]
    public void Only_a_code_spelt_exactly_gets_words_of_its_own()
    {
        // Codes that differ from one with words by a single letter, by length, or in case get none: only the code picks
        // the text, never something close to it.
        var near = WithWords.SelectMany(NearMisses).Concat(["", "x", "care_team", "CHECK.NOT_FOUND"]).Except(WithWords).ToList();

        Assert.All(WithWords, code => Assert.NotNull(ErrorMessages.ForCode(code)));
        Assert.All(near, code => Assert.Null(ErrorMessages.ForCode(code)));
        Assert.All(ErrorMessages.Internal, code => Assert.Null(ErrorMessages.ForCode(code)));
        Assert.Equal("Something went wrong. Please try again.", ErrorMessages.For(new DomainException("window.invalid", "Week 50")));
    }

    private static IEnumerable<string> NearMisses(string code) =>
        Enumerable.Range(0, code.Length).Select(i => code[..i] + "#" + code[(i + 1)..]).Append(code + "#");

    /// <summary>Every code with words of its own, as <see cref="ErrorMessages.ForCode"/> lists them.</summary>
    private static readonly string[] WithWords =
    [
        "care_team.role_full", "care_team.contact_not_found", "appointment.contact_unknown", "contact.field.too_long",
        "check.is_milestone", "check.not_found", "plan.checks.full", "plan.tasks.full", "plan.questions.full",
        "task.title.too_long", "entry.title.too_long", "task.time_without_date", "task.not_found", "question.text.too_long",
        "question.not_found", "appointment.location.too_long", "attachment.duplicate", "attachment.not_found",
        "attachment.not_a_file", "attachment.type.unsupported", "attachment.size.too_large", "attachment.size.empty",
        "dating.out_of_range", "template.not_reviewed", "plan.not_found", "plan.already_exists",
        PlanUnreadableException.NewerAppCode, PlanUnreadableException.KeyUnavailableCode, PlanUnreadableException.DamagedCode,
        BackupUnreadableException.WrongPasswordCode, BackupUnreadableException.NotABackupCode,
        BackupUnreadableException.NewerAppCode, BackupUnreadableException.DamagedCode,
    ];
}
