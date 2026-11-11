using Maia.Presentation.Plan;

namespace Maia.App.Views;

/// <summary>Picks the timeline template for each kind of row on Journey.</summary>
public sealed class PlanRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Section { get; set; }

    /// <summary>A pregnancy week's heading.</summary>
    public DataTemplate? Week { get; set; }

    public DataTemplate? Here { get; set; }

    public DataTemplate? Check { get; set; }

    /// <summary>One of her tasks on its day (the "Tasks" switch on).</summary>
    public DataTemplate? Task { get; set; }

    /// <summary>A blood pressure reading she logged, small on the line.</summary>
    public DataTemplate? Reading { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container) => item switch
    {
        SectionRow => Section,
        WeekRow => Week,
        HereRow => Here,
        TaskRow => Task,
        ReadingRow => Reading,
        _ => Check,
    };
}
