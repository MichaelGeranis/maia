using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Maia.Application.Abstractions;
using Maia.Domain.Common;
using Maia.Domain.Templates;

namespace Maia.Infrastructure.Templates;

public sealed record TemplateCatalogOptions
{
    /// <summary>
    /// Allow templates not yet clinically reviewed. True for development and test builds only;
    /// release builds must set false so unreviewed clinical content can never reach users.
    /// </summary>
    public bool AllowDraftTemplates { get; init; }
}

/// <summary>
/// Plan templates shipped inside the app as JSON (one file per country, see Templates/Content).
/// Parsed once and validated through the domain constructors, so a broken template fails fast
/// in tests rather than on a user's phone.
/// </summary>
public sealed class EmbeddedPlanTemplateCatalog : IPlanTemplateCatalog
{
    private const string ResourcePrefix = "Maia.Templates.";

    private readonly Lazy<IReadOnlyDictionary<string, PlanTemplate>> _templates;
    private readonly TemplateCatalogOptions _options;

    public EmbeddedPlanTemplateCatalog(TemplateCatalogOptions options)
        : this(options, typeof(EmbeddedPlanTemplateCatalog).Assembly)
    {
    }

    /// <param name="options">Whether drafts may be used.</param>
    /// <param name="resources">The assembly the templates are embedded in: Maia's own, or another in tests.</param>
    internal EmbeddedPlanTemplateCatalog(TemplateCatalogOptions options, Assembly resources)
    {
        _options = options;
        _templates = new Lazy<IReadOnlyDictionary<string, PlanTemplate>>(() => LoadAll(resources));
    }

    public IReadOnlyList<string> SupportedCountries => _templates.Value.Keys.Order(StringComparer.Ordinal).ToList();

    public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        if (!_templates.Value.TryGetValue(countryCode.ToUpperInvariant(), out var template))
        {
            throw new DomainException("template.country.unsupported", $"No plan template for country '{countryCode}'.");
        }

        if (template.ReviewStatus == ReviewStatus.Draft && !_options.AllowDraftTemplates)
        {
            throw new DomainException("template.not_reviewed", $"Template '{template.Id}' has not been clinically reviewed.");
        }

        return Task.FromResult(template);
    }

    /// <summary>Parses one template document. Exposed for tests and tooling.</summary>
    internal static PlanTemplate Parse(Stream json, string sourceName)
    {
        TemplateDocument document;
        try
        {
            document = JsonSerializer.Deserialize(json, TemplateJsonContext.Default.TemplateDocument)
                ?? throw new InvalidDataException($"Template '{sourceName}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Template '{sourceName}' is not valid JSON: {ex.Message}", ex);
        }

        if (document.SchemaVersion != TemplateDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Template '{sourceName}' uses unsupported schema {document.SchemaVersion}."));
        }

        // A sign-off is only as good as its record (ADR 0003): who reviewed this version, and when.
        if (document.ReviewStatus == ReviewStatus.ClinicallyReviewed
            && (string.IsNullOrWhiteSpace(document.ReviewedBy) || document.ReviewedOn is null))
        {
            throw new InvalidDataException($"Template '{sourceName}' is marked clinically reviewed but does not say by whom (reviewedBy) and when (reviewedOn).");
        }

        try
        {
            return new PlanTemplate(
                document.Id,
                document.Country,
                document.Version,
                document.ReviewStatus,
                document.Checks.Select(c => new CheckTemplate(
                    new CheckId(c.Id),
                    new LocalizedText(c.Title),
                    new LocalizedText(c.Description),
                    c.Category,
                    new WeekWindow(c.FromWeek, c.ToWeek),
                    c.IncludedByDefault)),
                document.ReviewedBy,
                document.ReviewedOn,
                document.ReviewNotes);
        }
        catch (DomainException ex)
        {
            throw new InvalidDataException($"Template '{sourceName}' is invalid: {ex.Message}", ex);
        }
    }

    private static Dictionary<string, PlanTemplate> LoadAll(Assembly assembly)
    {
        var templates = new Dictionary<string, PlanTemplate>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidDataException($"Missing resource '{name}'.");
            var template = Parse(stream, name);
            if (!templates.TryAdd(template.CountryCode, template))
            {
                throw new InvalidDataException($"Two templates target country '{template.CountryCode}'.");
            }
        }

        return templates;
    }
}

internal sealed record TemplateDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string Country { get; init; }

    public required int Version { get; init; }

    public required ReviewStatus ReviewStatus { get; init; }

    /// <summary>Who signed this version off: the clinician's name and role. Required once reviewed.</summary>
    public string? ReviewedBy { get; init; }

    /// <summary>The day they signed it off, as <c>2026-11-02</c>. Required once reviewed.</summary>
    public DateOnly? ReviewedOn { get; init; }

    public string? ReviewNotes { get; init; }

    public required List<TemplateCheckDocument> Checks { get; init; }
}

internal sealed record TemplateCheckDocument
{
    public required string Id { get; init; }

    public required CheckCategory Category { get; init; }

    public required int FromWeek { get; init; }

    public required int ToWeek { get; init; }

    public bool IncludedByDefault { get; init; } = true;

    public required Dictionary<string, string> Title { get; init; }

    public required Dictionary<string, string> Description { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(TemplateDocument))]
internal sealed partial class TemplateJsonContext : JsonSerializerContext;
