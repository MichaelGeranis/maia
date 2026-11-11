using Maia.Domain.Pregnancy;
using Maia.Presentation.Plan;
using Maia.Presentation.Questions;
using Maia.Presentation.Setup;
using Maia.Testing;

namespace Maia.Presentation.Tests;

/// <summary>Choices that change nothing: a language Maia doesn't have, a reminder time that doesn't exist, a mode chosen twice.</summary>
public sealed class ChoiceEdgeTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Theory]
    [InlineData("en-US", "fr")]
    [InlineData("el-GR", "fr")]
    [InlineData("el-GR", "el")]
    public async Task A_language_maia_does_not_have_or_already_shows_changes_nothing(string culture, string language)
    {
        using var _ = new CultureScope(culture);
        await _app.CreatePlanAsync();
        var languages = new RecordingLanguageSwitcher();
        var settings = _app.CreateSettings(languages);
        var setup = new SetupViewModel(_app.Clock, new SetupSession(), _app.Navigator, languages);

        await settings.ChooseLanguageCommand.ExecuteAsync(language);
        await setup.ChooseLanguageCommand.ExecuteAsync(language);

        Assert.Empty(languages.Switched);
        Assert.Equal("en", _app.Repository.Stored!.Language);
    }

    [Fact]
    public async Task In_greek_english_can_be_chosen_on_setup_and_in_settings()
    {
        using var greek = new CultureScope("el-GR");
        await _app.CreatePlanAsync();
        var languages = new RecordingLanguageSwitcher();

        await _app.CreateSettings(languages).ChooseLanguageCommand.ExecuteAsync("en");
        await new SetupViewModel(_app.Clock, new SetupSession(), _app.Navigator, languages).ChooseLanguageCommand.ExecuteAsync("en");

        Assert.Equal(new[] { "en", "en" }, languages.Switched);
    }

    [Theory]
    [InlineData("Never")]
    [InlineData("9")]
    public async Task A_reminder_time_that_does_not_exist_is_not_chosen(string choice)
    {
        await _app.CreatePlanAsync();
        var setting = _app.CreateRemindersSetting();
        await setting.LoadAsync();
        var when = setting.When;

        setting.SetRemindersCommand.Execute(choice);
        await setting.WhenSavedAsync();

        Assert.Equal(when, setting.When);
        Assert.Equal(when, _app.ReminderSettings.Settings.When);
    }

    [Fact]
    public void Choosing_trimester_again_keeps_the_trimester_she_chose()
    {
        var timing = new TimingDraft();
        var age = PregnancyDating.FromDueDate(TestData.DueDate).On(TestData.Today);
        timing.Start(false, true, TimingMode.Trimester, new DateTime(2026, 9, 30), age, new DateTime(2026, 4, 12), new DateTime(2027, 1, 31));
        timing.ChooseTrimesterCommand.Execute("Third");

        timing.ChooseModeCommand.Execute("Week");
        timing.ChooseModeCommand.Execute("Trimester");

        Assert.Equal(Trimester.Third, timing.Trimester);
    }

    [Fact]
    public async Task A_card_not_done_has_no_tick()
    {
        await _app.CreatePlanAsync();
        var vm = new PlanViewModel(_app.Queries, _app.Plans, _app.Clock, _app.Navigator, _app.Notifier, _app.Errors, _app.Focus);
        await vm.LoadAsync();

        Assert.False(vm.Rows.OfType<CheckRow>().Single(r => r.Id == TestData.Glucose).IsDone);
    }

    [Fact]
    public async Task While_she_drags_a_list_that_shrank_shows_as_it_is()
    {
        var vm = await QuestionsAsync("A", "B", "C");
        vm.StartDragCommand.Execute(vm.Questions[2]);
        vm.DragOverCommand.Execute(vm.Questions[0]);
        await vm.PendingHover;
        Assert.Equal(new[] { "C", "A", "B" }, vm.DragOrder.Select(q => q.Text));

        await RemoveAsync(vm, "B");

        // The question she drags was third, and there are only two now.
        Assert.Equal(new[] { "A", "C" }, vm.DragOrder.Select(q => q.Text));
    }

    [Fact]
    public async Task While_she_drags_a_list_whose_questions_moved_up_shows_as_it_is()
    {
        var vm = await QuestionsAsync("A", "B", "C", "D");
        vm.StartDragCommand.Execute(vm.Questions[2]);
        vm.DragOverCommand.Execute(vm.Questions[0]);
        await vm.PendingHover;

        await RemoveAsync(vm, "A");

        // Third is now D, not the C she holds.
        Assert.Equal(new[] { "B", "C", "D" }, vm.DragOrder.Select(q => q.Text));
    }

    [Fact]
    public async Task While_she_drags_to_a_place_the_list_no_longer_has_it_shows_as_it_is()
    {
        var vm = await QuestionsAsync("A", "B", "C");
        vm.StartDragCommand.Execute(vm.Questions[0]);
        vm.DragOverCommand.Execute(vm.Questions[2]);
        await vm.PendingHover;
        Assert.Equal(new[] { "B", "C", "A" }, vm.DragOrder.Select(q => q.Text));

        await RemoveAsync(vm, "B");

        Assert.Equal(new[] { "A", "C" }, vm.DragOrder.Select(q => q.Text));
    }

    private async Task<QuestionsViewModel> QuestionsAsync(params string[] questions)
    {
        await _app.CreatePlanAsync();

        // Each new question goes on top: written last first, the list reads in the order given.
        foreach (var question in questions.Reverse())
        {
            await _app.Plans.AddQuestionAsync(question);
        }

        var vm = new QuestionsViewModel(_app.Queries, _app.Plans, _app.Navigator, _app.Notifier, _app.Errors);
        await vm.LoadAsync();
        return vm;
    }

    private async Task RemoveAsync(QuestionsViewModel vm, string text)
    {
        await _app.Plans.RemoveQuestionAsync(vm.Questions.Single(q => q.Text == text).Id);
        await vm.LoadAsync();
    }
}
