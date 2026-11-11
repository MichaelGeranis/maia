using Maia.Application.Plans;
using Maia.Domain.Care;
using Maia.Domain.Plans;
using Maia.Presentation.Questions;
using Maia.Testing;

namespace Maia.Presentation.Tests;

public sealed class QuestionsTests : IDisposable
{
    private readonly CultureScope _culture = new("en-US");
    private readonly TestApp _app = new();

    public void Dispose()
    {
        _app.Dispose();
        _culture.Dispose();
    }

    private QuestionsViewModel Create() => new(_app.Queries, _app.Plans, _app.Navigator, _app.Notifier, _app.Errors);

    /// <summary>Questions she wrote, the last one first: each new one goes on top, so the list reads in the order given.</summary>
    private async Task WriteAsync(params string[] questions)
    {
        foreach (var question in questions.Reverse())
        {
            await _app.Plans.AddQuestionAsync(question);
        }
    }

    [Fact]
    public void The_list_is_hers_and_tied_to_no_doctor_or_appointment()
    {
        // No "Questions for Dr. …" and no "Next appointment · …": one title, the same whoever and whatever is booked.
        Assert.Null(typeof(QuestionsViewModel).GetProperty("Title"));
        Assert.Null(typeof(QuestionsViewModel).GetProperty("Eyebrow"));
        Assert.Equal("Questions", Maia.Presentation.Localization.Text.In("Questions_Title", "en"));
        Assert.Equal("Ερωτήσεις", Maia.Presentation.Localization.Text.In("Questions_Title", "el"));
        var page = File.ReadAllText(Repo.App("Views", "QuestionsPage.xaml"));
        Assert.Contains("{loc:Translate Questions_Title}", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Eyebrow", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_doctor_or_an_appointment_the_list_still_works()
    {
        await _app.CreatePlanAsync();
        var vm = Create();

        await vm.LoadAsync();

        Assert.Equal("No questions yet. Add one when it comes to mind.", vm.Footer);
        Assert.False(vm.AddCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_plus_looks_faded_and_does_nothing_until_she_writes_a_question()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();

        Assert.False(vm.CanAddDraft);
        vm.Draft = "   ";
        Assert.False(vm.CanAddDraft);
        Assert.False(vm.AddCommand.CanExecute(null));

        vm.Draft = "Is it still safe to cycle to work?";
        Assert.True(vm.CanAddDraft);
        Assert.True(vm.AddCommand.CanExecute(null));

        // The page fades the + until then.
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "QuestionsPage.xaml"));
        var plus = page.Descendants().Single(e => AccessibilityTests.Attr(e, "SemanticProperties.Description") == "{loc:Translate Questions_Add}");
        Assert.Equal("0.45", AccessibilityTests.Attr(plus, "Opacity"));
        Assert.Contains(plus.Descendants(), e => e.Name.LocalName == "DataTrigger" && AccessibilityTests.Attr(e, "Binding") == "{Binding CanAddDraft}");
    }

    [Fact]
    public async Task Dragging_a_question_onto_another_moves_it_there_and_keeps_the_order()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("First", "Second", "Third");
        var vm = Create();
        await vm.LoadAsync();
        var third = vm.Questions[2];

        vm.StartDragCommand.Execute(third);
        await vm.DropOnCommand.ExecuteAsync(vm.Questions[0]);

        Assert.Equal(new[] { "Third", "First", "Second" }, vm.Questions.Select(q => q.Text));
        Assert.Equal(new[] { "Third", "First", "Second" }, _app.Repository.Stored!.Questions.Select(q => q.Text));

        // Down the list too; and the order stays when the list is read again.
        vm.StartDragCommand.Execute(third);
        await vm.DropOnCommand.ExecuteAsync(vm.Questions[2]);
        await vm.LoadAsync();
        Assert.Equal(new[] { "First", "Second", "Third" }, vm.Questions.Select(q => q.Text));
        Assert.Same(third, vm.Questions[2]);
    }

    [Fact]
    public async Task Dropping_a_question_on_itself_or_without_a_drag_changes_nothing()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("First", "Second");
        var vm = Create();
        await vm.LoadAsync();
        var saves = _app.Repository.SaveCount;

        await vm.DropOnCommand.ExecuteAsync(vm.Questions[0]);
        vm.StartDragCommand.Execute(vm.Questions[1]);
        await vm.DropOnCommand.ExecuteAsync(vm.Questions[1]);

        Assert.Equal(new[] { "First", "Second" }, vm.Questions.Select(q => q.Text));
        Assert.Equal(saves, _app.Repository.SaveCount);
    }

    [Fact]
    public async Task Starting_a_drag_closes_a_question_open_for_changes()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("First", "Second");
        var vm = Create();
        await vm.LoadAsync();
        vm.Questions[0].EditCommand.Execute(null);

        vm.StartDragCommand.Execute(vm.Questions[1]);

        Assert.False(vm.Questions[0].IsEditing);
    }

    [Fact]
    public void Each_question_has_a_handle_to_drag_and_the_whole_question_takes_a_drop()
    {
        var page = System.Xml.Linq.XDocument.Load(Repo.App("Views", "QuestionsPage.xaml"));
        var drag = page.Descendants().Single(e => e.Name.LocalName == "DragGestureRecognizer");
        var drop = page.Descendants().Single(e => e.Name.LocalName == "DropGestureRecognizer");
        var row = drop.Parent!.Parent!;

        Assert.Contains("StartDragCommand", AccessibilityTests.Attr(drag, "DragStartingCommand"), StringComparison.Ordinal);
        Assert.Contains("EndDragCommand", AccessibilityTests.Attr(drag, "DropCompletedCommand"), StringComparison.Ordinal);
        Assert.Equal("{loc:Translate Questions_Move}", AccessibilityTests.Attr(drag.Parent!.Parent!, "SemanticProperties.Description"));
        Assert.Contains("DropOnCommand", AccessibilityTests.Attr(drop, "DropCommand"), StringComparison.Ordinal);
        Assert.Contains("DragOverCommand", AccessibilityTests.Attr(drop, "DragOverCommand"), StringComparison.Ordinal);
        Assert.Contains("DragLeaveCommand", AccessibilityTests.Attr(drop, "DragLeaveCommand"), StringComparison.Ordinal);

        // The whole question follows her finger, not just the handle: the page draws the row (by its ClassId) as the drag
        // picture, and the row fades in its place; the others slide out of its way (TranslationY, not a rebuild).
        Assert.Equal("QuestionRow", AccessibilityTests.Attr(row, "ClassId"));
        Assert.Equal("OnQuestionDragStarting", AccessibilityTests.Attr(drag, "DragStarting"));
        var code = Repo.CodeOf(Repo.App("Views", "QuestionsPage.xaml.cs"));
        Assert.Contains("SetDragShadowBuilder(new Platforms.Android.RowDragShadow(", code, StringComparison.Ordinal);
        Assert.Contains("\"QuestionRow\"", code, StringComparison.Ordinal);
        var triggers = row.Descendants().Where(e => e.Name.LocalName == "DataTrigger" && e.Parent!.Parent == row).ToList();
        Assert.Contains(triggers, t => AccessibilityTests.Attr(t, "Binding") == "{Binding IsDragged}");
        Assert.Contains("DragOrderChanged", code, StringComparison.Ordinal);
        Assert.Contains("TranslationY", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Held_over_another_question_that_one_makes_room_and_she_lets_go_in_the_gap()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("First", "Second", "Third");
        var vm = Create();
        await vm.LoadAsync();
        var (first, second, third) = (vm.Questions[0], vm.Questions[1], vm.Questions[2]);
        var slides = new List<bool>();
        vm.DragOrderChanged += (_, slide) => slides.Add(slide);

        vm.StartDragCommand.Execute(first);
        Assert.True(vm.IsDragging);
        Assert.True(first.IsDragged);

        // Over Second for a moment: Second slides up, and First's gap is under it. The list itself is not touched yet.
        vm.DragOverCommand.Execute(second);
        await vm.PendingHover;
        Assert.Equal(new[] { second, first, third }, vm.DragOrder);
        Assert.Equal(new[] { first, second, third }, vm.Questions);
        Assert.Equal(new[] { true }, slides);

        // Still over Second (it moved under her finger): nothing goes back. Over its gap, then Third: Third makes room too.
        vm.DragOverCommand.Execute(second);
        await vm.PendingHover;
        vm.DragOverCommand.Execute(first);
        vm.DragOverCommand.Execute(third);
        await vm.PendingHover;
        Assert.Equal(new[] { second, third, first }, vm.DragOrder);

        // She lets go over the gap: First lands there, saved, with the rows at rest at once.
        await vm.DropOnCommand.ExecuteAsync(first);
        Assert.Equal(new[] { "Second", "Third", "First" }, vm.Questions.Select(q => q.Text));
        Assert.Equal(new[] { "Second", "Third", "First" }, _app.Repository.Stored!.Questions.Select(q => q.Text));
        Assert.False(slides[^1]);
        Assert.False(vm.IsDragging);
        Assert.All(vm.Questions, q => Assert.False(q.IsDragged));
    }

    [Fact]
    public async Task Passing_over_a_question_without_waiting_moves_nothing_and_letting_go_outside_puts_it_back()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("First", "Second", "Third");
        var vm = Create();
        await vm.LoadAsync();
        var (first, second, third) = (vm.Questions[0], vm.Questions[1], vm.Questions[2]);
        var saves = _app.Repository.SaveCount;

        vm.StartDragCommand.Execute(first);
        vm.DragOverCommand.Execute(second);
        var passing = vm.PendingHover;
        vm.DragLeaveCommand.Execute(second);
        await passing;
        Assert.Equal(new[] { first, second, third }, vm.DragOrder);

        vm.DragOverCommand.Execute(third);
        await vm.PendingHover;
        Assert.Equal(new[] { second, third, first }, vm.DragOrder);

        // Let go outside the list: the others slide back, nothing is saved, nothing stays faded.
        var slid = (bool?)null;
        vm.DragOrderChanged += (_, slide) => slid = slide;
        vm.EndDragCommand.Execute(null);
        Assert.True(slid);
        Assert.Equal(new[] { first, second, third }, vm.DragOrder);
        Assert.Equal(new[] { "First", "Second", "Third" }, vm.Questions.Select(q => q.Text));
        Assert.Equal(saves, _app.Repository.SaveCount);
        Assert.All(vm.Questions, q => Assert.False(q.IsDragged));
    }

    [Fact]
    public async Task A_new_question_goes_on_the_list_and_the_field_clears()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();

        vm.Draft = " Is it still safe to cycle to work? ";
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal("Is it still safe to cycle to work?", vm.Questions.Single().Text);
        Assert.Equal(string.Empty, vm.Draft);
        Assert.Equal("Tick one off once it's answered.", vm.Footer);
        Assert.Single(_app.Repository.Stored!.Questions);
    }

    [Fact]
    public async Task Ticking_a_question_strikes_it_and_the_footer_says_how_without_counting()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("Is it still safe to cycle to work?", "When should the hospital bag be ready?", "Can we talk through pain relief options?");
        var vm = Create();
        await vm.LoadAsync();

        await vm.Questions[2].ToggleCommand.ExecuteAsync(null);

        Assert.True(vm.Questions[2].IsAnswered);
        Assert.Equal("Tick one off once it's answered.", vm.Footer);
        Assert.True(_app.Repository.Stored!.Questions[2].IsAnswered);
    }

    [Fact]
    public async Task With_every_question_answered_the_footer_says_so()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddQuestionAsync("Is it still safe to cycle to work?");
        var vm = Create();
        await vm.LoadAsync();

        await vm.Questions[0].ToggleCommand.ExecuteAsync(null);

        Assert.Equal("All asked. Add another when one comes up.", vm.Footer);
    }

    [Fact]
    public async Task A_failed_save_is_reported_by_code_and_keeps_her_draft()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();
        _app.Repository.FailNextSave = new IOException("disk full");

        vm.Draft = "Is it still safe to cycle to work?";
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal("Is it still safe to cycle to work?", vm.Draft);
        Assert.Equal("Something went wrong. Please try again.", _app.Notifier.Shown.Single());
        Assert.Contains(_app.Errors.Reports, r => r.Operation == "questions.add");
        Assert.Empty(_app.Repository.Stored!.Questions);
    }

    [Fact]
    public async Task Tapping_a_question_opens_it_to_reword_it()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddQuestionAsync("When should the bag be ready?");
        var vm = Create();
        await vm.LoadAsync();
        var row = vm.Questions.Single();

        row.EditCommand.Execute(null);
        Assert.True(row.IsEditing);
        Assert.Equal("When should the bag be ready?", row.EditText);

        row.EditText = "  ";
        Assert.False(row.SaveCommand.CanExecute(null));
        row.EditText = " When should the hospital bag be ready? ";
        await row.SaveCommand.ExecuteAsync(null);

        Assert.False(row.IsEditing);
        Assert.Same(row, vm.Questions.Single());
        Assert.Equal("When should the hospital bag be ready?", row.Text);
        Assert.Equal("When should the hospital bag be ready?", _app.Repository.Stored!.Questions.Single().Text);
    }

    [Fact]
    public async Task Closing_a_question_editor_lets_the_page_put_the_keyboard_away()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddQuestionAsync("Is swimming fine?");
        var vm = Create();
        await vm.LoadAsync();
        var closed = 0;
        vm.EditorClosed += (_, _) => closed++;
        var row = vm.Questions.Single();

        row.EditCommand.Execute(null);
        row.EditText = "Is swimming still fine?";
        await row.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, closed);
        Assert.False(row.IsEditing);
    }

    [Fact]
    public async Task Deleting_a_question_asks_once_more()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("Is it still safe to cycle to work?", "When should the hospital bag be ready?");
        var vm = Create();
        await vm.LoadAsync();
        var cycling = vm.Questions[0];

        cycling.EditCommand.Execute(null);
        cycling.AskDeleteCommand.Execute(null);
        Assert.True(cycling.IsConfirmingDelete);
        cycling.KeepItCommand.Execute(null);
        Assert.Equal(2, _app.Repository.Stored!.Questions.Count);

        cycling.AskDeleteCommand.Execute(null);
        await cycling.DeleteCommand.ExecuteAsync(null);

        Assert.Equal("When should the hospital bag be ready?", vm.Questions.Single().Text);
        Assert.Equal("Question deleted.", _app.Notifier.Shown.Single());
        Assert.Single(_app.Repository.Stored!.Questions);
        Assert.Equal("Tick one off once it's answered.", vm.Footer);
    }

    [Fact]
    public async Task Answered_questions_can_be_cleared_after_asking_once_more()
    {
        await _app.CreatePlanAsync();
        await WriteAsync("Is it still safe to cycle to work?", "When should the hospital bag be ready?", "Can we talk through pain relief options?");
        var vm = Create();
        await vm.LoadAsync();
        Assert.False(vm.HasAnswered);

        await vm.Questions[0].ToggleCommand.ExecuteAsync(null);
        await vm.Questions[2].ToggleCommand.ExecuteAsync(null);
        Assert.True(vm.HasAnswered);

        vm.AskClearCommand.Execute(null);
        Assert.True(vm.IsConfirmingClear);
        Assert.Equal("Clear the answered questions? The ones still to ask stay.", vm.ClearQuestion);
        Assert.Equal("Keep them", vm.KeepAnsweredLabel);
        vm.KeepAnsweredCommand.Execute(null);
        Assert.False(vm.IsConfirmingClear);
        Assert.Equal(3, _app.Repository.Stored!.Questions.Count);

        vm.AskClearCommand.Execute(null);
        await vm.ClearAnsweredCommand.ExecuteAsync(null);

        Assert.Equal("When should the hospital bag be ready?", vm.Questions.Single().Text);
        Assert.False(vm.HasAnswered);
        Assert.False(vm.IsConfirmingClear);
        Assert.Equal("2 answered questions cleared.", _app.Notifier.Shown.Single());
        Assert.Single(_app.Repository.Stored!.Questions);
    }

    [Fact]
    public async Task A_new_question_goes_on_top_of_the_rows_already_there()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.AddQuestionAsync("Is it still safe to cycle to work?");
        var vm = Create();
        await vm.LoadAsync();
        var first = vm.Questions.Single();
        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        vm.Questions.CollectionChanged += (_, e) => changes.Add(e.Action);

        await vm.LoadAsync();
        vm.Draft = "When should the hospital bag be ready?";
        await vm.AddCommand.ExecuteAsync(null);

        // One row joins, at the top; the one already there keeps its row.
        Assert.Equal(new[] { System.Collections.Specialized.NotifyCollectionChangedAction.Add }, changes);
        Assert.Equal("When should the hospital bag be ready?", vm.Questions[0].Text);
        Assert.Same(first, vm.Questions[1]);
        Assert.Equal(2, vm.Questions.Count);
    }

    [Fact]
    public async Task Back_goes_back_to_your_week()
    {
        await _app.CreatePlanAsync();
        var vm = Create();
        await vm.LoadAsync();

        await vm.BackCommand.ExecuteAsync(null);

        Assert.Equal("back", _app.Navigator.Visited[^1]);
    }

    [Fact]
    public async Task The_list_reads_the_plan_once()
    {
        await _app.CreatePlanAsync();
        await _app.Plans.LinkContactsAsync([new ContactToLink(CareRole.ObGyn, "Dr. Eleni Papadaki", "+30 690 000 4567", "Mobile", "c1")]);
        var vm = Create();
        var before = _app.Repository.LoadCount;

        await vm.LoadAsync();

        Assert.Equal(before + 1, _app.Repository.LoadCount);
    }
}
