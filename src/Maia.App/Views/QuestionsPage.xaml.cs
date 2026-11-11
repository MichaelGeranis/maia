using System.Collections.Specialized;
using Maia.App.Controls;
using Maia.App.Services;
using Maia.Presentation.Questions;

namespace Maia.App.Views;

public partial class QuestionsPage : MaiaPage
{
    /// <summary>How long a question takes to slide up or down out of the way.</summary>
    private const uint SlideLength = 180;

    private readonly QuestionsViewModel _viewModel;

    public QuestionsPage(QuestionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // A question's editor closed: its field is gone, so the keyboard goes too.
        _viewModel.EditorClosed += (_, _) => Dispatcher.Dispatch(SoftKeyboard.Hide);

        _viewModel.DragOrderChanged += OnDragOrderChanged;
        _viewModel.Questions.CollectionChanged += OnQuestionsChanged;
    }

    /// <summary>
    /// She takes hold of a question's handle: Android drags the whole question under her finger, not just the handle, so
    /// she sees what she moves. (The view model fades it in its place meanwhile.)
    /// </summary>
    private void OnQuestionDragStarting(object? sender, DragStartingEventArgs e)
    {
#if ANDROID
        if (sender is not DragGestureRecognizer { Parent: View handle } || e.PlatformArgs is not { } platform)
        {
            return;
        }

        Element? row = handle;
        // The question's row in the list (its ClassId), which the drag draws whole.
        while (row is not null && row.ClassId != "QuestionRow")
        {
            row = row.Parent;
        }

        if (row is View { Handler.PlatformView: Android.Views.View rowView } && handle.Handler?.PlatformView is Android.Views.View handleView
            && platform.MotionEvent is { } touch)
        {
            platform.SetDragShadowBuilder(new Platforms.Android.RowDragShadow(rowView, handleView, touch.GetX(), touch.GetY()));
        }
#else
        _ = sender;
        _ = e;
#endif
    }

    /// <summary>
    /// While she drags, each question slides to where <see cref="QuestionsViewModel.DragOrder"/> puts it: the ones she
    /// passed move up or down, and the faded one moves into the gap. Only drawn there (moved, not rebuilt), so the list
    /// stays smooth; when she lets go, the list itself takes the order and the rows are back at rest at once.
    /// </summary>
    private void OnDragOrderChanged(object? sender, bool slide)
    {
        var rows = QuestionList.Children.OfType<VisualElement>().ToList();
        var questions = _viewModel.Questions;
        if (rows.Count == 0 || rows.Count != questions.Count)
        {
            return;
        }

        var rowOf = new Dictionary<QuestionItem, VisualElement>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            rowOf[questions[i]] = rows[i];
        }

        var y = rows[0].Y;
        foreach (var question in _viewModel.DragOrder)
        {
            if (rowOf.GetValueOrDefault(question) is not { } row)
            {
                continue;
            }

            Slide(row, y - row.Y, slide);
            y += row.Height + QuestionList.Spacing;
        }
    }

    private static void Slide(VisualElement row, double to, bool animate)
    {
        row.AbortAnimation("maia-slide");
        var from = row.TranslationY;
        if (!animate || Motion.IsReduced || Math.Abs(to - from) < 0.5)
        {
            row.TranslationY = to;
            return;
        }

        new Animation(v => row.TranslationY = v, from, to, Easing.CubicOut).Commit(row, "maia-slide", length: SlideLength);
    }

    /// <summary>A question moved within the list (dropped in its new place) is built again there: it doesn't pop in as new.</summary>
    private void OnQuestionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move && e.NewStartingIndex >= 0 && e.NewStartingIndex < QuestionList.Children.Count
            && QuestionList.Children[e.NewStartingIndex] is BindableObject row)
        {
            Motions.SetPop(row, false);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScreenWork.Load(_viewModel.LoadAsync, "questions.load");
    }
}
