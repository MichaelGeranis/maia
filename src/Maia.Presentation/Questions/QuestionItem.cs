using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maia.Domain.Common;

namespace Maia.Presentation.Questions;

/// <summary>What a question row asks of the Questions screen: its changes go through the plan there.</summary>
internal interface IQuestionHost
{
    Task ToggleAsync(QuestionItem question);

    /// <summary>Opens this row to change it and closes any other.</summary>
    void OpenEditor(QuestionItem question);

    Task SaveAsync(QuestionItem question);

    Task DeleteAsync(QuestionItem question);

    /// <summary>A row's editor closed (saved, deleted or cancelled): the keyboard it used can go.</summary>
    void EditorClosed();
}

/// <summary>
/// A question on the checklist; ticked once the doctor has answered it. Tapping the box ticks it; tapping the words
/// opens it in place to reword or delete it (asked once more, in place).
/// </summary>
public sealed partial class QuestionItem : ObservableObject
{
    private readonly IQuestionHost _host;

    internal QuestionItem(QuestionId id, IQuestionHost host)
    {
        Id = id;
        _host = host;
    }

    public QuestionId Id { get; }

    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAnswered { get; set; }

    /// <summary>Whether the row is open to change it.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string EditText { get; set; } = string.Empty;

    /// <summary>Whether "Delete this question?" is showing.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; private set; }

    /// <summary>Whether she is dragging this question: it shows faded in its place while it follows her finger.</summary>
    [ObservableProperty]
    public partial bool IsDragged { get; internal set; }

    internal void Show(string text, bool isAnswered)
    {
        Text = text;
        IsAnswered = isAnswered;
    }

    internal void BeginEdit()
    {
        EditText = Text;
        IsConfirmingDelete = false;
        IsEditing = true;
    }

    internal void EndEdit()
    {
        IsConfirmingDelete = false;
        if (IsEditing)
        {
            IsEditing = false;
            _host.EditorClosed();
        }
    }

    [RelayCommand]
    private Task ToggleAsync() => _host.ToggleAsync(this);

    /// <summary>Opens the row to change it; tapping its words again closes it.</summary>
    [RelayCommand]
    private void Edit()
    {
        if (IsEditing)
        {
            EndEdit();
        }
        else
        {
            _host.OpenEditor(this);
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(EditText) && EditText.Trim().Length <= QuestionsViewModel.MaxLength;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => _host.SaveAsync(this);

    [RelayCommand]
    private void Cancel() => EndEdit();

    [RelayCommand]
    private void AskDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void KeepIt() => IsConfirmingDelete = false;

    [RelayCommand]
    private Task DeleteAsync() => _host.DeleteAsync(this);
}
