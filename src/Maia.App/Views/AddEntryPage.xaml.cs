using Maia.App.Controls;
using Maia.Presentation.Add;

namespace Maia.App.Views;

public partial class AddEntryPage : MaiaPage
{
    private readonly AddEntryViewModel _viewModel;

    public AddEntryPage(AddEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScreenWork.Load(_viewModel.LoadAsync, "add.load");
    }

    /// <summary>Puts the keyboard away when she taps Save, so she sees what happened.</summary>
    private void OnSaveClicked(object? sender, EventArgs e)
    {
        foreach (var entry in new InputView[] { TaskEntry, NameEntry, NoteEditor, SystolicEntry, DiastolicEntry })
        {
            if (entry.IsFocused)
            {
                ScreenWork.Run(
                    async () =>
                    {
                        await entry.HideSoftInputAsync(CancellationToken.None);
                        entry.Unfocus();
                    },
                    "add.keyboard");
            }
        }
    }
}
