using System.Text.RegularExpressions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Plan;
using Maia.Presentation.Settings;
using Maia.Presentation.Setup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>
/// A failure while a screen loads, or inside an action, never ends Maia: it is reported by code and she reads a calm,
/// specific line. Today is 24+3.
/// </summary>
public sealed class CrashSafetyTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task A_screen_that_cannot_load_says_so_calmly_and_reports_only_its_code()
    {
        await _app.CreatePlanAsync();
        _app.Repository.FailNextLoad = new IOException("Couldn't read Glucose test");

        var result = await ScreenLoad.RunAsync(_app.CreateWeek().LoadAsync, "week.load", _app.Errors);

        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", result.Message);
        Assert.False(result.NeedsStartup);
        var report = Assert.Single(_app.Errors.Reports);
        Assert.Equal("week.load", report.Operation);
    }

    [Fact]
    public async Task A_plan_that_cannot_be_opened_hands_over_to_the_start_up_screen()
    {
        await _app.CreatePlanAsync();
        _app.Repository.Unreadable = PlanUnreadableException.Damaged(new InvalidDataException("both copies"));

        var result = await ScreenLoad.RunAsync(NewPlan().LoadAsync, "plan.load", _app.Errors);

        Assert.True(result.NeedsStartup);
        Assert.Equal("Maia can't open your journey right now.", result.Message);
    }

    [Fact]
    public async Task A_change_that_breaks_a_rule_says_what_happened_instead_of_something_went_wrong()
    {
        await _app.CreatePlanAsync();
        var plan = NewPlan();
        await plan.LoadAsync();

        // Deleted on another screen while this card was open.
        var glucose = Open(plan, TestData.Glucose);
        glucose.StartBookCommand.Execute(null);
        await _app.Plans.DeleteEntryAsync(TestData.Glucose);
        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Equal("This check isn't in your journey any more.", glucose.ErrorMessage);
        Assert.Equal("plan.book", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task A_full_care_team_role_says_how_many_it_holds()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync(Enumerable.Range(0, CareTeam.MaxContactsPerRole)
            .Select(i => new ContactToLink(CareRole.ObGyn, $"Contact {i}", $"+30 690 000 00{i:00}", null, $"c{i}")));
        var team = new Care.TeamViewModel(_app.Queries, _app.Plans, _app.Contacts, _app.Dialer, _app.Chooser, _app.Notifier, _app.Errors);
        await team.LoadAsync();
        _app.Contacts.Picks.Enqueue(new Care.PickedContact("Papadaki Clinic", "+30 210 000 9999", "Work", "c99"));

        await team.LinkCommand.ExecuteAsync(null);

        Assert.Equal("You can link up to 10 contacts here. Unlink one to add another.", team.Notice);
    }

    [Theory]
    [InlineData("care_team.role_full")]
    [InlineData("plan.tasks.full")]
    [InlineData("plan.questions.full")]
    [InlineData("task.title.too_long")]
    [InlineData("attachment.duplicate")]
    public void Known_codes_have_calm_words_of_their_own_in_both_languages(string code)
    {
        var english = ErrorMessages.For(new DomainException(code, "Check 'nipt' is already done."));
        string greek;
        using (new CultureScope("el-GR"))
        {
            greek = ErrorMessages.For(new DomainException(code, "Check 'nipt' is already done."));
        }

        Assert.NotEqual(Text.Get("Common_Error"), english);
        Assert.NotEqual(english, greek);
        Assert.DoesNotContain("{0}", english, StringComparison.Ordinal);
        Assert.DoesNotContain("nipt", english, StringComparison.Ordinal);
    }

    [Fact]
    public void Any_other_failure_gets_the_general_words() =>
        Assert.Equal("Something went wrong. Please try again.", ErrorMessages.For(new InvalidOperationException("Glucose test")));

    [Fact]
    public void Every_error_code_she_can_meet_has_words_of_its_own()
    {
        var src = Path.Combine(Repo.Root(), "src");
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "DomainException\\(\\s*\"([a-z_]+(?:\\.[a-z_]+)+)\"\\s*,"))
            {
                codes.Add(match.Groups[1].Value);
            }
        }

        foreach (Match match in Regex.Matches(File.ReadAllText(Path.Combine(src, "Maia.Application", "Common", "ApplicationExceptions.cs")), "\"([a-z_]+(?:\\.[a-z_]+)+)\""))
        {
            codes.Add(match.Groups[1].Value);
        }

        Assert.Contains("care_team.role_full", codes);
        Assert.Contains("plan.not_found", codes);
        var withoutWords = codes.Where(code => !ErrorMessages.Internal.Contains(code) && ErrorMessages.ForCode(code) is null).ToList();
        Assert.True(withoutWords.Count == 0, "Codes with no words of their own: " + string.Join(", ", withoutWords));
        Assert.All(codes.Where(code => !ErrorMessages.Internal.Contains(code)), code =>
        {
            var english = ErrorMessages.ForCode(code)!;
            using var greek = new CultureScope("el-GR");
            Assert.NotEqual(english, ErrorMessages.ForCode(code));
        });
    }

    [Fact]
    public async Task A_reload_that_fails_after_booking_keeps_maia_open_and_says_so()
    {
        await _app.CreatePlanAsync();
        var plan = NewPlan();
        await plan.LoadAsync();
        var glucose = Open(plan, TestData.Glucose);
        _app.Notifier.WhenShown = message =>
        {
            if (message.StartsWith("Glucose test booked", StringComparison.Ordinal))
            {
                _app.Repository.FailNextLoad = new IOException("storage");
            }
        };

        glucose.StartBookCommand.Execute(null);
        glucose.Timing.Date = new DateTime(2026, 10, 12);
        await glucose.SaveCommand.ExecuteAsync(null);

        Assert.Equal(CheckState.Booked, _app.Repository.Stored!.GetCheck(TestData.Glucose).State);
        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", _app.Notifier.Shown[^1]);
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "plan.load");
    }

    [Fact]
    public async Task A_task_whose_reload_fails_is_still_added_and_maia_stays_open()
    {
        await _app.CreatePlanAsync();
        var week = _app.CreateWeek();
        await week.LoadAsync();
        week.OpenTaskFormCommand.Execute(null);
        week.NewTaskTitle = "Call the clinic";
        week.NewTaskHasDate = true;
        _app.Notifier.WhenShown = message =>
        {
            if (message.StartsWith("Task added for", StringComparison.Ordinal))
            {
                _app.Repository.FailNextLoad = new IOException("storage");
            }
        };

        await week.AddTaskCommand.ExecuteAsync(null);

        Assert.Single(_app.Repository.Stored!.Tasks);
        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", _app.Notifier.Shown[^1]);
        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", Assert.Single(_app.Notifier.Notices));
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "week.load");
    }

    [Fact]
    public async Task A_task_whose_reload_fails_after_deleting_is_still_gone_and_maia_stays_open()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddTaskAsync("Call the clinic", new DateOnly(2026, 10, 1));
        var week = _app.CreateWeek();
        await week.LoadAsync();
        var task = Assert.Single(week.Tasks);
        _app.Notifier.WhenShown = message =>
        {
            if (message == "Task deleted.")
            {
                _app.Repository.FailNextLoad = new IOException("storage");
            }
        };

        task.AskDeleteCommand.Execute(null);
        await task.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_app.Repository.Stored!.Tasks);
        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", _app.Notifier.Shown[^1]);
        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", Assert.Single(_app.Notifier.Notices));
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "week.load");
    }

    [Fact]
    public async Task A_question_whose_reload_fails_after_deleting_is_still_gone_and_maia_stays_open()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddQuestionAsync("Is it still safe to cycle to work?");
        var questions = new Questions.QuestionsViewModel(_app.Queries, _app.Plans, _app.Navigator, _app.Notifier, _app.Errors);
        await questions.LoadAsync();
        var cycling = Assert.Single(questions.Questions);
        _app.Notifier.WhenShown = message =>
        {
            if (message == "Question deleted.")
            {
                _app.Repository.FailNextLoad = new IOException("storage");
            }
        };

        cycling.AskDeleteCommand.Execute(null);
        await cycling.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_app.Repository.Stored!.Questions);
        Assert.Equal("Maia couldn't show this just now. Try again in a moment.", _app.Notifier.Shown[^1]);
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "questions.load");
    }

    [Fact]
    public async Task Changing_reminder_choices_while_her_plan_cannot_be_read_keeps_maia_open_and_the_choices_still_save()
    {
        await _app.CreatePlanAsync();
        _app.ReminderSettings.IsOn = true;
        _app.NotificationAccess.Granted = true;
        var reminders = _app.CreateRemindersSetting();
        await reminders.LoadAsync();
        _app.Repository.FailLoads = new IOException("storage");

        reminders.SetRemindersCommand.Execute("Both");
        reminders.DiscreetTitles = true;
        await reminders.WhenSavedAsync();

        Assert.Equal(new Application.Abstractions.ReminderSettings { When = Application.Abstractions.ReminderPreference.Both, DiscreetTitles = true }, _app.ReminderSettings.Settings);
        Assert.Null(reminders.Notice);
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "reminders.set");
    }

    [Fact]
    public async Task A_file_picker_that_fails_on_restore_is_said_calmly_and_can_be_tried_again()
    {
        var restore = new RestoreViewModel(_app.Plans, _app.BackupFiles, _app.Navigator, _app.Notifier, _app.Errors, _app.BackupHistory, _app.Clock);
        _app.BackupFiles.FailPick = new IOException("cloud provider offline");

        await restore.ChooseFileCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong. Please try again.", restore.ErrorMessage);
        Assert.Equal("setup.restore_pick", Assert.Single(_app.Errors.Reports).Operation);

        _app.BackupFiles.NextPick = new BackupFile("Maia-backup.maia", () => Task.FromResult<Stream>(new MemoryStream()));
        await restore.ChooseFileCommand.ExecuteAsync(null);

        Assert.Null(restore.ErrorMessage);
        Assert.True(restore.HasFile);
    }

    [Fact]
    public async Task A_plan_made_in_setup_opens_with_a_plain_toast_even_if_later_loads_fail()
    {
        _app.Setup.DueDate = TestData.DueDate;
        var care = _app.CreateSetupCare();
        await care.LoadAsync();

        // Every load after the plan is saved fails: the toast never needed one.
        _app.Repository.BeforeLoad = () => _app.Repository.Stored is null ? Task.CompletedTask : Task.FromException(new IOException("storage"));
        await care.SkipCommand.ExecuteAsync(null);

        Assert.NotNull(_app.Repository.Stored);
        Assert.Equal(new[] { "main" }, _app.Navigator.Visited);
        Assert.Equal("Your journey is ready.", _app.Notifier.Shown.Single());
    }

    [Fact]
    public async Task The_plan_screen_loads_the_plan_once_per_refresh()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync([new ContactToLink(CareRole.ObGyn, "Papadaki Clinic", "+30 210 000 9999", "Work", "c1")]);
        var plan = NewPlan();
        var before = _app.Repository.LoadCount;

        await plan.LoadAsync();

        Assert.Equal(before + 1, _app.Repository.LoadCount);
    }

    [Fact]
    public async Task Refreshes_asked_for_while_the_plan_loads_run_once_more_after_it()
    {
        await _app.CreatePlanAsync();
        var plan = NewPlan();
        var slow = new SlowStep();
        _app.Repository.BeforeLoad = slow.WaitAsync;
        var before = _app.Repository.LoadCount;

        var first = plan.LoadAsync();
        await slow.Started;
        _app.Repository.BeforeLoad = null;
        var second = plan.LoadAsync();
        var third = plan.LoadAsync();
        slow.Finish();
        await Task.WhenAll(first, second, third);

        Assert.Equal(before + 2, _app.Repository.LoadCount);
        Assert.NotEmpty(plan.Rows);
    }

    private PlanViewModel NewPlan() =>
        new(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);

    private static CheckRow Row(PlanViewModel vm, CheckId id) => vm.Rows.OfType<CheckRow>().Single(r => r.Id == id);

    private static CheckRow Open(PlanViewModel vm, CheckId id)
    {
        var row = Row(vm, id);
        if (!row.IsExpanded)
        {
            row.ToggleCommand.Execute(null);
        }

        return row;
    }
}
