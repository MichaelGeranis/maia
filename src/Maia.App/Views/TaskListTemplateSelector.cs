using Maia.Presentation.Week;

namespace Maia.App.Views;

/// <summary>Picks the template for each row of Your tasks: a group's heading or a task.</summary>
public sealed class TaskListTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Group { get; set; }

    public DataTemplate? Task { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container) => item switch
    {
        TaskGroupRow => Group,
        _ => Task,
    };
}
