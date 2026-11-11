using Maia.Presentation.Questions;

namespace Maia.Presentation.Tests;

/// <summary>Her questions when a change can't be saved, when there is no plan, and drags that end up nowhere.</summary>
public sealed class QuestionsEdgeTests : IDisposable
{
    private const string WentWrong = "Something went wrong. Please try again.";

    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    [Fact]
    public async Task Without_a_plan_questions_go_to_setup()
    {
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal(new[] { "setup" }, _app.Navigator.Visited);
        Assert.Empty(vm.Questions);
    }

    [Fact]
    public async Task A_question_open_for_changes_closes_when_she_taps_it_again_or_cancels()
    {
        var vm = await OpenAsync("Is spotting normal?");
        var question = vm.Questions[0];

        question.EditCommand.Execute(null);
        Assert.True(question.IsEditing);
        Assert.True(question.SaveCommand.CanExecute(null));
        question.EditCommand.Execute(null);
        Assert.False(question.IsEditing);

        question.EditCommand.Execute(null);
        question.CancelCommand.Execute(null);
        Assert.False(question.IsEditing);
    }

    [Fact]
    public async Task Clear_answered_is_offered_once_some_are_answered_and_not_while_she_is_asked()
    {
        var vm = await OpenAsync("Is spotting normal?", "Can I fly?");
        Assert.False(vm.ShowClearAnswered);

        await vm.Questions[0].ToggleCommand.ExecuteAsync(null);
        Assert.True(vm.ShowClearAnswered);

        vm.AskClearCommand.Execute(null);
        Assert.False(vm.ShowClearAnswered);
    }

    [Fact]
    public async Task Clearing_several_answered_questions_says_how_many()
    {
        var vm = await OpenAsync("Is spotting normal?", "Can I fly?", "Iron?");
        await vm.Questions[0].ToggleCommand.ExecuteAsync(null);
        await vm.Questions[1].ToggleCommand.ExecuteAsync(null);

        await vm.ClearAnsweredCommand.ExecuteAsync(null);

        Assert.Equal("2 answered questions cleared.", _app.Notifier.Shown.Last());
        Assert.Equal("Iron?", Assert.Single(vm.Questions).Text);
    }

    [Fact]
    public async Task Clearing_one_answered_question_says_so()
    {
        var vm = await OpenAsync("Is spotting normal?", "Can I fly?");
        await vm.Questions[0].ToggleCommand.ExecuteAsync(null);

        await vm.ClearAnsweredCommand.ExecuteAsync(null);

        Assert.Equal("1 answered question cleared.", _app.Notifier.Shown.Last());
    }

    [Fact]
    public async Task Clearing_that_cannot_be_saved_says_so_and_keeps_the_question_up()
    {
        var vm = await OpenAsync("Is spotting normal?");
        await vm.Questions[0].ToggleCommand.ExecuteAsync(null);
        vm.AskClearCommand.Execute(null);
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.ClearAnsweredCommand.ExecuteAsync(null);

        Assert.Equal(WentWrong, _app.Notifier.Notices.Single());
        Assert.True(vm.IsConfirmingClear);
        Assert.Single(_app.Repository.Stored!.Questions);
    }

    [Fact]
    public async Task Ticking_a_question_that_cannot_be_saved_says_so_and_leaves_it_unticked()
    {
        var vm = await OpenAsync("Is spotting normal?");
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.Questions[0].ToggleCommand.ExecuteAsync(null);

        Assert.Equal(WentWrong, _app.Notifier.Notices.Single());
        Assert.False(vm.Questions[0].IsAnswered);
        Assert.Equal("questions.answer", Assert.Single(_app.Errors.Reports).Operation);
    }

    [Fact]
    public async Task An_edit_that_cannot_be_saved_says_so_and_stays_open()
    {
        var vm = await OpenAsync("Is spotting normal?");
        var question = vm.Questions[0];
        question.EditCommand.Execute(null);
        question.EditText = "Is light spotting normal?";
        _app.Repository.FailNextSave = new IOException("disk full");

        await question.SaveCommand.ExecuteAsync(null);

        Assert.Equal(WentWrong, _app.Notifier.Notices.Single());
        Assert.True(question.IsEditing);
        Assert.Equal("Is spotting normal?", _app.Repository.Stored!.Questions.Single().Text);
    }

    [Fact]
    public async Task A_delete_that_cannot_be_saved_says_so_and_keeps_the_question()
    {
        var vm = await OpenAsync("Is spotting normal?");
        var question = vm.Questions[0];
        _app.Repository.FailNextSave = new IOException("disk full");

        await question.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(WentWrong, _app.Notifier.Notices.Single());
        Assert.Single(vm.Questions);
        Assert.Single(_app.Repository.Stored!.Questions);
    }

    [Fact]
    public async Task A_move_that_cannot_be_saved_says_so_and_puts_the_list_back()
    {
        var vm = await OpenAsync("First", "Second", "Third");
        vm.StartDragCommand.Execute(vm.Questions[2]);
        _app.Repository.FailNextSave = new IOException("disk full");

        await vm.DropOnCommand.ExecuteAsync(vm.Questions[0]);

        Assert.Equal(WentWrong, _app.Notifier.Notices.Single());
        Assert.Equal(new[] { "First", "Second", "Third" }, vm.Questions.Select(q => q.Text));
    }

    [Fact]
    public async Task A_drag_that_starts_on_nothing_or_a_question_no_longer_listed_is_no_drag()
    {
        var vm = await OpenAsync("First", "Second");
        var second = vm.Questions[1];
        await _app.Plans.RemoveQuestionAsync(second.Id);
        await vm.LoadAsync();

        vm.StartDragCommand.Execute(null);
        Assert.False(vm.IsDragging);
        vm.StartDragCommand.Execute(second);
        Assert.False(vm.IsDragging);
    }

    [Fact]
    public async Task Hovering_without_a_drag_over_nothing_or_again_over_the_same_question_waits_once()
    {
        var vm = await OpenAsync("First", "Second", "Third");
        vm.DragOverCommand.Execute(vm.Questions[1]);
        Assert.True(vm.PendingHover.IsCompleted);

        vm.StartDragCommand.Execute(vm.Questions[0]);
        vm.DragOverCommand.Execute(null);
        Assert.True(vm.PendingHover.IsCompleted);

        vm.DragOverCommand.Execute(vm.Questions[1]);
        var hover = vm.PendingHover;
        vm.DragOverCommand.Execute(vm.Questions[1]);
        Assert.Same(hover, vm.PendingHover);

        // Leaving nothing, or a question it isn't over, lets the one it is over make room.
        vm.DragLeaveCommand.Execute(null);
        vm.DragLeaveCommand.Execute(vm.Questions[2]);
        await hover;

        Assert.Equal(new[] { "Second", "First", "Third" }, vm.DragOrder.Select(q => q.Text));
    }

    [Fact]
    public async Task Dropping_on_a_question_no_longer_listed_moves_nothing()
    {
        var vm = await OpenAsync("First", "Second", "Third");
        var third = vm.Questions[2];
        await _app.Plans.RemoveQuestionAsync(third.Id);
        await vm.LoadAsync();
        var saves = _app.Repository.SaveCount;
        vm.StartDragCommand.Execute(vm.Questions[0]);

        await vm.DropOnCommand.ExecuteAsync(third);

        Assert.False(vm.IsDragging);
        Assert.Equal(new[] { "First", "Second" }, vm.Questions.Select(q => q.Text));
        Assert.Equal(saves, _app.Repository.SaveCount);
    }

    private QuestionsViewModel Create() => new(_app.Queries, _app.Plans, _app.Navigator, _app.Notifier, _app.Errors);

    private async Task<QuestionsViewModel> OpenAsync(params string[] questions)
    {
        await _app.CreatePlanAsync();

        // Each new question goes on top: written last first, the list reads in the order given.
        foreach (var question in questions.Reverse())
        {
            await _app.Plans.AddQuestionAsync(question);
        }

        var vm = Create();
        await vm.LoadAsync();
        return vm;
    }
}
