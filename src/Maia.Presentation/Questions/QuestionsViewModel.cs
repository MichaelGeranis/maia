using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Application.Abstractions;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Collections;
using Maia.Presentation.Common;
using Maia.Presentation.Formatting;
using Maia.Presentation.Localization;
using Maia.Presentation.Navigation;

namespace Maia.Presentation.Questions;

/// <summary>
/// Her questions (design screen 13): a checklist she keeps for whenever she needs it, tied to no appointment.
/// Answered questions stay, struck through, so she can see what was covered, until she clears them (asked once more,
/// in place). A question can be reworded, deleted or dragged by its handle to another place in the list. Each refresh
/// reads the plan once and updates the list in place, so only a new question pops in.
/// </summary>
public sealed partial class QuestionsViewModel : ObservableObject, IQuestionHost
{
    public const int MaxLength = DoctorQuestion.MaxTextLength;

    private readonly PlanQueries _queries;
    private readonly PlanService _plans;
    private readonly INavigator _navigator;
    private readonly INotifier _notifier;
    private readonly IErrorReporter _errors;
    private readonly Dictionary<QuestionId, QuestionItem> _rows = [];

    /// <summary>The question she is dragging by its handle, until she drops it on another.</summary>
    private QuestionItem? _dragged;

    /// <summary>Where the question she drags was when she took hold of it.</summary>
    private int _dragFrom;

    /// <summary>Where it would land now: the gap the other questions have made for it.</summary>
    private int _dragTo;

    /// <summary>The question it last made room past: held over it still, it stays put rather than going back.</summary>
    private QuestionItem? _passed;

    /// <summary>The question she is holding it over, waiting for <see cref="HoverDelay"/> before the others make room.</summary>
    private QuestionItem? _hovered;

    /// <summary>Counts each wait over a question, so a wait that was given up (she moved on) does nothing when it ends.</summary>
    private int _hoverCount;

    public QuestionsViewModel(PlanQueries queries, PlanService plans, INavigator navigator, INotifier notifier, IErrorReporter errors)
    {
        _queries = queries;
        _plans = plans;
        _navigator = navigator;
        _notifier = notifier;
        _errors = errors;
    }

    public ObservableCollection<QuestionItem> Questions { get; } = [];

    /// <summary>Raised when a question's editor closes, so the page can put the keyboard away.</summary>
    public event EventHandler? EditorClosed;

    /// <summary>
    /// Raised when the questions should be drawn in <see cref="DragOrder"/>: another one made room for the question she
    /// drags, or the drag ended. True: slide there. False: be there at once (she let go, and the list now holds that order).
    /// </summary>
    public event EventHandler<bool>? DragOrderChanged;

    /// <summary>How long she holds the question over another before that one makes room for it.</summary>
    public static TimeSpan HoverDelay { get; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Whether she is dragging a question.</summary>
    public bool IsDragging => _dragged is not null;

    /// <summary>
    /// The questions in the order she sees them while she drags: the one she drags in the gap made for it. Without a drag,
    /// the list as it is.
    /// </summary>
    public IReadOnlyList<QuestionItem> DragOrder
    {
        get
        {
            var order = Questions.ToList();
            if (_dragged is not null && _dragFrom != _dragTo && _dragFrom < order.Count && order[_dragFrom] == _dragged && _dragTo < order.Count)
            {
                order.RemoveAt(_dragFrom);
                order.Insert(_dragTo, _dragged);
            }

            return order;
        }
    }

    /// <summary>The wait before a question makes room, for tests to await.</summary>
    internal Task PendingHover { get; private set; } = Task.CompletedTask;

    /// <summary>"2 still to ask. Tick one off once it's answered."</summary>
    [ObservableProperty]
    public partial string Footer { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    [NotifyPropertyChangedFor(nameof(CanAddDraft))]
    public partial string Draft { get; set; } = string.Empty;

    /// <summary>Whether there is a question to add: the + looks faded and does nothing until she has written one.</summary>
    public bool CanAddDraft => CanAdd();

    /// <summary>Whether some questions are ticked off, so they can be cleared.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClearAnswered))]
    public partial bool HasAnswered { get; private set; }

    /// <summary>Whether "Clear the answered questions?" is showing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClearAnswered))]
    public partial bool IsConfirmingClear { get; private set; }

    /// <summary>Whether "Clear answered" is offered.</summary>
    public bool ShowClearAnswered => HasAnswered && !IsConfirmingClear;

    /// <summary>"Clear 2 answered questions? The ones still to ask stay."</summary>
    [ObservableProperty]
    public partial string ClearQuestion { get; private set; } = string.Empty;

    /// <summary>"Keep them", or "Keep it" for one.</summary>
    [ObservableProperty]
    public partial string KeepAnsweredLabel { get; private set; } = string.Empty;

    public async Task LoadAsync()
    {
        var week = await _queries.GetWeekAsync();
        if (week is null)
        {
            await _navigator.GoToSetupAsync();
            return;
        }

        var wanted = new List<QuestionItem>(week.Questions.Count);
        foreach (var question in week.Questions)
        {
            if (!_rows.TryGetValue(question.Id, out var row))
            {
                row = new QuestionItem(question.Id, this);
                _rows[question.Id] = row;
            }

            row.Show(question.Text, question.IsAnswered);
            wanted.Add(row);
        }

        CollectionSync.Apply(Questions, wanted, (a, b) => a.Id == b.Id);
        foreach (var gone in _rows.Keys.Except(wanted.Select(q => q.Id)).ToList())
        {
            _rows.Remove(gone);
        }

        Count();
    }

    private bool CanAdd() => !string.IsNullOrWhiteSpace(Draft) && Draft.Trim().Length <= MaxLength;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var text = Draft.Trim();
        var error = await PlanChange.RunAsync(() => _plans.AddQuestionAsync(text), "questions.add", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        Draft = string.Empty;
        await ReloadAfterChangeAsync();
    }

    /// <summary>
    /// She holds a question and starts dragging it: any question open for changes closes, and the question shows faded in
    /// its place while the phone draws it under her finger.
    /// </summary>
    [RelayCommand]
    private void StartDrag(QuestionItem question)
    {
        IsConfirmingClear = false;
        CloseEditors();
        EndDrag();
        var from = question is null ? -1 : Questions.IndexOf(question);
        if (question is null || from < 0)
        {
            return;
        }

        _dragged = question;
        _dragFrom = _dragTo = from;
        question.IsDragged = true;
    }

    /// <summary>
    /// The question she drags is over <paramref name="target"/>. Held there for <see cref="HoverDelay"/>, that one slides
    /// up or down to make room, and the gap shows where it would land. Over its own gap, nothing moves.
    /// </summary>
    [RelayCommand]
    private void DragOver(QuestionItem target)
    {
        if (_dragged is null || target is null || target == _hovered)
        {
            return;
        }

        CancelHover();
        if (target == _dragged)
        {
            // Back over its gap: the question it just passed can make room again.
            _passed = null;
            return;
        }

        if (target == _passed)
        {
            return;
        }

        _hovered = target;
        PendingHover = MakeRoomSoonAsync(target, ++_hoverCount);
    }

    /// <summary>It left <paramref name="target"/> before that one made room: it stays where it is.</summary>
    [RelayCommand]
    private void DragLeave(QuestionItem target)
    {
        if (target is not null && target == _hovered)
        {
            CancelHover();
        }
    }

    /// <summary>The drag ended without a drop on a question: the others slide back, and every question shows as usual.</summary>
    [RelayCommand]
    private void EndDrag()
    {
        if (_dragged is not null)
        {
            Finish(animate: true);
        }
    }

    private async Task MakeRoomSoonAsync(QuestionItem target, int hover)
    {
        await Task.Delay(HoverDelay);
        if (hover == _hoverCount && _hovered == target && _dragged is not null)
        {
            _hovered = null;
            MakeRoomAt(target, notify: true);
        }
    }

    /// <summary>The question she drags goes where <paramref name="target"/> shows now; that one moves up or down one.</summary>
    private void MakeRoomAt(QuestionItem target, bool notify)
    {
        var at = DragOrder.ToList().IndexOf(target);
        if (at < 0)
        {
            return;
        }

        _dragTo = at;
        _passed = target;
        if (notify)
        {
            DragOrderChanged?.Invoke(this, true);
        }
    }

    private void CancelHover()
    {
        _hoverCount++;
        _hovered = null;
    }

    /// <summary>The drag is over: every question shows as usual, in <see cref="DragOrder"/>'s place or back in its own.</summary>
    private void Finish(bool animate)
    {
        CancelHover();
        _dragged = null;
        _passed = null;
        _dragFrom = _dragTo = 0;
        foreach (var question in Questions)
        {
            question.IsDragged = false;
        }

        DragOrderChanged?.Invoke(this, animate);
    }

    /// <summary>
    /// She lets go of the question she drags over <paramref name="target"/>. Over the gap made for it (or the question that
    /// just made it), it lands in the gap; over another question, it takes that one's place at once. The list holds the
    /// new order straight away; if the change can't be saved, it goes back as it was.
    /// </summary>
    [RelayCommand]
    private async Task DropOnAsync(QuestionItem target)
    {
        var dragged = _dragged;
        if (dragged is null || target is null)
        {
            EndDrag();
            return;
        }

        if (target != dragged && target != _passed)
        {
            MakeRoomAt(target, notify: false);
        }

        var (from, to) = (_dragFrom, _dragTo);
        Finish(animate: false);
        if (from == to || from >= Questions.Count || to >= Questions.Count || Questions[from] != dragged)
        {
            return;
        }

        Questions.Move(from, to);
        var error = await PlanChange.RunAsync(() => _plans.MoveQuestionAsync(dragged.Id, to), "questions.move", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            await ReloadAfterChangeAsync();
        }
    }

    /// <summary>After a change: the list shows it, and a reload that fails says so calmly instead of ending Maia.</summary>
    private async Task ReloadAfterChangeAsync()
    {
        if (await ScreenLoad.ReloadAsync(LoadAsync, "questions.load", _errors) is { } failed)
        {
            _notifier.ShowNotice(failed);
        }
    }

    /// <summary>"Back", to Your week.</summary>
    [RelayCommand]
    private Task BackAsync() => _navigator.GoBackAsync();

    [RelayCommand]
    private void AskClear()
    {
        CloseEditors();
        IsConfirmingClear = HasAnswered;
    }

    [RelayCommand]
    private void KeepAnswered() => IsConfirmingClear = false;

    /// <summary>Clears the questions she ticked off; the ones still to ask stay.</summary>
    [RelayCommand]
    private async Task ClearAnsweredAsync()
    {
        var cleared = 0;
        var error = await PlanChange.RunAsync(async () => cleared = await _plans.ClearAnsweredQuestionsAsync(), "questions.clear", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        IsConfirmingClear = false;
        _notifier.Show(cleared == 1 ? Text.Get("Toast_QuestionsClearedOne") : Text.Format("Toast_QuestionsCleared", cleared));
        await ReloadAfterChangeAsync();
    }

    async Task IQuestionHost.ToggleAsync(QuestionItem question)
    {
        var answered = !question.IsAnswered;
        var error = await PlanChange.RunAsync(() => _plans.SetQuestionAnsweredAsync(question.Id, answered), "questions.answer", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        question.IsAnswered = answered;
        Count();
    }

    void IQuestionHost.EditorClosed() => EditorClosed?.Invoke(this, EventArgs.Empty);

    void IQuestionHost.OpenEditor(QuestionItem question)
    {
        IsConfirmingClear = false;
        CloseEditors();
        question.BeginEdit();
    }

    async Task IQuestionHost.SaveAsync(QuestionItem question)
    {
        var text = question.EditText.Trim();
        var error = await PlanChange.RunAsync(() => _plans.EditQuestionAsync(question.Id, text), "questions.edit", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        question.EndEdit();
        await ReloadAfterChangeAsync();
    }

    async Task IQuestionHost.DeleteAsync(QuestionItem question)
    {
        var error = await PlanChange.RunAsync(() => _plans.RemoveQuestionAsync(question.Id), "questions.delete", _errors);
        if (error is not null)
        {
            _notifier.ShowNotice(error);
            return;
        }

        question.EndEdit();
        _notifier.Show(Text.Get("Toast_QuestionDeleted"));
        await ReloadAfterChangeAsync();
    }

    private void CloseEditors()
    {
        foreach (var open in Questions.Where(q => q.IsEditing))
        {
            open.EndEdit();
        }
    }

    private void Count()
    {
        var open = Questions.Count(q => !q.IsAnswered);
        var answered = Questions.Count - open;
        Footer = (Questions.Count, open) switch
        {
            (0, _) => Text.Get("Questions_Empty"),
            (_, 0) => Text.Get("Questions_AllAsked"),
            _ => Text.Get("Questions_Left"),
        };
        HasAnswered = answered > 0;
        ClearQuestion = Text.Get("Questions_ClearConfirm");
        KeepAnsweredLabel = Text.Get(answered == 1 ? "Common_KeepIt" : "Common_KeepThem");
        if (!HasAnswered)
        {
            IsConfirmingClear = false;
        }
    }
}
