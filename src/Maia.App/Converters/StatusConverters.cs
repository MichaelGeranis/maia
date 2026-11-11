using System.Globalization;

namespace Maia.App.Converters;

/// <summary>
/// Maps a check's status kind (from the view model) to a colour token, per the status table in
/// <c>docs/design/README.md</c>. One subclass per visual property; unknown kinds fall back to the not-booked look. A
/// booking whose time has passed comes as "Done" (<c>CheckRow.StatusKind</c>).
/// </summary>
public abstract class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = Pick(value as string ?? string.Empty);
        return Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    protected abstract string Pick(string statusKind);
}

/// <summary>Status chip background.</summary>
public sealed class StatusBackgroundConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind switch
    {
        "Done" or "ProbablyDone" => "SageTint",
        "Booked" => "Sky",
        "Milestone" => "Sand",
        _ => "Line",
    };
}

/// <summary>Status chip text.</summary>
public sealed class StatusForegroundConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind switch
    {
        "Done" or "ProbablyDone" => "Pine",
        "Booked" => "SkyDeep",
        "Milestone" => "SandDeep",
        _ => "Muted",
    };
}

/// <summary>
/// Timeline card background: White at 85 % as every card, done entries receding to 55 % and the open card white
/// (docs/design/README.md, Status colours).
/// </summary>
public sealed class StatusCardConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind switch
    {
        "Done" or "ProbablyDone" => "CardDone",
        "Expanded" => "White",
        _ => "CardSoft",
    };
}

/// <summary>Timeline card border: Line for every status, which the chip and the node carry; Sage on the open card.</summary>
public sealed class StatusCardBorderConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind is "Expanded" ? "Sage" : "Line";
}

/// <summary>Check name on the timeline: muted once done.</summary>
public sealed class StatusTitleConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind is "Done" or "ProbablyDone" ? "Muted" : "Ink";
}

/// <summary>Timeline node fill.</summary>
public sealed class StatusNodeFillConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind switch
    {
        "Done" or "ProbablyDone" => "Sage",
        "Booked" => "Sea",
        "Milestone" => "Sun",
        _ => "White",
    };
}

/// <summary>Timeline node ring.</summary>
public sealed class StatusNodeRingConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind switch
    {
        "Done" or "ProbablyDone" => "Sage",
        "Booked" => "Sea",
        "Milestone" => "Sun",
        _ => "MutedHalf",
    };
}

/// <summary>Setup tag chip background: "Probably done" in sage, "Routine" muted, the rest in sky.</summary>
public sealed class TagBackgroundConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind switch
    {
        "ProbablyDone" => "SageTint",
        "Routine" => "Line",
        _ => "Sky",
    };
}

/// <summary>Setup tag chip text.</summary>
public sealed class TagForegroundConverter : StatusColorConverter
{
    protected override string Pick(string statusKind) => statusKind switch
    {
        "ProbablyDone" => "Pine",
        "Routine" => "Muted",
        _ => "SkyDeep",
    };
}
