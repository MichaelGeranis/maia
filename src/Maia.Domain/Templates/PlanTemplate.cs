using Maia.Domain.Common;

namespace Maia.Domain.Templates;

/// <summary>Whether a template's clinical content has been signed off.</summary>
public enum ReviewStatus
{
    /// <summary>Written by the team; must not ship to users.</summary>
    Draft = 0,

    /// <summary>Reviewed by a licensed clinician for the template's country.</summary>
    ClinicallyReviewed = 1,
}

/// <summary>
/// The typical set of checks for one country, versioned. Plans remember the template id and
/// version they were created from, so content updates can be offered without losing user edits.
/// </summary>
public sealed class PlanTemplate
{
    /// <param name="id">Stable id, for example <c>gr-standard</c>.</param>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code.</param>
    /// <param name="version">Goes up with every change to the content.</param>
    /// <param name="reviewStatus">Whether a clinician signed this version off.</param>
    /// <param name="checks">The checks, in any order.</param>
    /// <param name="reviewedBy">Who signed this version off: the clinician's name and role.</param>
    /// <param name="reviewedOn">The day they signed it off.</param>
    /// <param name="reviewNotes">Notes about the review, or about what a draft still needs.</param>
    public PlanTemplate(
        string id,
        string countryCode,
        int version,
        ReviewStatus reviewStatus,
        IEnumerable<CheckTemplate> checks,
        string? reviewedBy = null,
        DateOnly? reviewedOn = null,
        string? reviewNotes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(checks);

        var list = checks.OrderBy(c => c.Window.FromWeek).ThenBy(c => c.Window.ToWeek).ToList();
        if (list.Count == 0)
        {
            throw new DomainException("template.empty", $"Template '{id}' has no checks.");
        }

        var duplicate = list.GroupBy(c => c.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainException("template.check.duplicate", $"Template '{id}' lists the same check twice.");
        }

        Id = id;
        CountryCode = countryCode.ToUpperInvariant();
        Version = version;
        ReviewStatus = reviewStatus;
        Checks = list.AsReadOnly();
        ReviewedBy = string.IsNullOrWhiteSpace(reviewedBy) ? null : reviewedBy.Trim();
        ReviewedOn = reviewedOn;
        ReviewNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();
    }

    public string Id { get; }

    /// <summary>ISO 3166-1 alpha-2 country code, for example <c>GR</c>.</summary>
    public string CountryCode { get; }

    public int Version { get; }

    public ReviewStatus ReviewStatus { get; }

    /// <summary>Who signed this version off (name and role), or null for a draft.</summary>
    public string? ReviewedBy { get; }

    /// <summary>The day this version was signed off, or null for a draft.</summary>
    public DateOnly? ReviewedOn { get; }

    public string? ReviewNotes { get; }

    /// <summary>Whether the sign-off is on record: who reviewed this version, and when.</summary>
    public bool HasSignOff => ReviewedBy is not null && ReviewedOn is not null;

    public IReadOnlyList<CheckTemplate> Checks { get; }

    public CheckTemplate? Find(CheckId id) => Checks.FirstOrDefault(c => c.Id == id);
}
