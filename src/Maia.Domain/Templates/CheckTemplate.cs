using Maia.Domain.Common;

namespace Maia.Domain.Templates;

/// <summary>How strongly a check is recommended. Drives the default switch in plan setup.</summary>
public enum CheckCategory
{
    /// <summary>Part of standard care; included by default.</summary>
    Routine = 0,

    /// <summary>Offered to everyone; the parent decides.</summary>
    Offered = 1,

    /// <summary>Only when the doctor advises it.</summary>
    IfAdvised = 2,

    /// <summary>Depends on the clinic or region.</summary>
    WhereOffered = 3,

    /// <summary>Not an exam: a date to show on the timeline, such as the due date.</summary>
    Milestone = 4,
}

/// <summary>One check in a plan template, as written by the clinical advisor.</summary>
public sealed record CheckTemplate
{
    public CheckTemplate(
        CheckId id,
        LocalizedText title,
        LocalizedText description,
        CheckCategory category,
        WeekWindow window,
        bool includedByDefault)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        if (id.IsCustom)
        {
            throw new DomainException("template.check.id_reserved", "Template checks cannot use the custom- prefix.");
        }

        Id = id;
        Title = title;
        Description = description;
        Category = category;
        Window = window;
        IncludedByDefault = category == CheckCategory.Milestone || includedByDefault;
    }

    public CheckId Id { get; }

    public LocalizedText Title { get; }

    public LocalizedText Description { get; }

    public CheckCategory Category { get; }

    public WeekWindow Window { get; }

    public bool IncludedByDefault { get; }
}
