using System.Reflection;
using System.Text;
using Maia.Domain.Common;
using Maia.Domain.Templates;
using Maia.Infrastructure.Templates;

namespace Maia.Infrastructure.Tests;

public sealed class TemplateCatalogTests
{
    private readonly EmbeddedPlanTemplateCatalog _catalog = new(new TemplateCatalogOptions { AllowDraftTemplates = true });

    [Fact]
    public async Task Ships_a_greek_template_with_english_and_greek_text()
    {
        var template = await _catalog.GetAsync("gr", CancellationToken.None);

        Assert.Equal("GR", template.CountryCode);
        Assert.Contains("GR", _catalog.SupportedCountries);
        Assert.All(template.Checks, c =>
        {
            Assert.True(c.Title.Has(LocalizedText.Greek), $"'{c.Id}' has no Greek title");
            Assert.True(c.Description.Has(LocalizedText.Greek), $"'{c.Id}' has no Greek description");
        });
        Assert.Single(template.Checks, c => c.Category == CheckCategory.Milestone);
        Assert.Contains(template.Checks, c => c.Id == new CheckId("ogtt") && c.Window == new WeekWindow(24, 28));
    }

    [Fact]
    public async Task Draft_templates_are_blocked_unless_allowed()
    {
        var release = new EmbeddedPlanTemplateCatalog(new TemplateCatalogOptions { AllowDraftTemplates = false });

        var ex = await Assert.ThrowsAsync<DomainException>(() => release.GetAsync("GR", CancellationToken.None));
        Assert.Equal("template.not_reviewed", ex.Code);
    }

    [Fact]
    public async Task Unknown_countries_are_reported()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => _catalog.GetAsync("XX", CancellationToken.None));
        Assert.Equal("template.country.unsupported", ex.Code);
    }

    [Theory]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("null", "is empty")]
    [InlineData("""{"schemaVersion":2,"id":"x","country":"GR","version":1,"reviewStatus":"Draft","checks":[]}""", "unsupported schema")]
    [InlineData("""{"schemaVersion":1,"id":"x","country":"GR","version":1,"reviewStatus":"Draft","checks":[{"id":"a","category":"Routine","fromWeek":30,"toWeek":20,"title":{"en":"A"},"description":{"en":"A"}}]}""", "is invalid")]
    public void Broken_templates_fail_with_a_clear_message(string json, string expected)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var ex = Assert.Throws<InvalidDataException>(() => EmbeddedPlanTemplateCatalog.Parse(stream, "test.json"));
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "2026-11-02")]
    [InlineData("Dr. Maria Example, OB-GYN", null)]
    [InlineData("  ", "2026-11-02")]
    [InlineData(null, null)]
    public void A_template_marked_reviewed_must_say_who_reviewed_it_and_when(string? reviewedBy, string? reviewedOn)
    {
        using var stream = Json(Template("ClinicallyReviewed", reviewedBy, reviewedOn));

        var ex = Assert.Throws<InvalidDataException>(() => EmbeddedPlanTemplateCatalog.Parse(stream, "test.json"));
        Assert.Contains("reviewedBy", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reviewed_template_carries_its_sign_off()
    {
        using var stream = Json(Template("ClinicallyReviewed", "Dr. Maria Example, OB-GYN", "2026-11-02", "Content and Greek wording."));

        var template = EmbeddedPlanTemplateCatalog.Parse(stream, "test.json");

        Assert.Equal(ReviewStatus.ClinicallyReviewed, template.ReviewStatus);
        Assert.Equal("Dr. Maria Example, OB-GYN", template.ReviewedBy);
        Assert.Equal(new DateOnly(2026, 11, 2), template.ReviewedOn);
        Assert.Equal("Content and Greek wording.", template.ReviewNotes);
        Assert.True(template.HasSignOff);
    }

    [Fact]
    public void A_draft_needs_no_sign_off()
    {
        using var stream = Json(Template("Draft", null, null, "Needs review."));

        var template = EmbeddedPlanTemplateCatalog.Parse(stream, "test.json");

        Assert.Equal(ReviewStatus.Draft, template.ReviewStatus);
        Assert.False(template.HasSignOff);
    }

    [Fact]
    public async Task The_greek_template_stays_a_draft_until_a_clinician_signs_it_off()
    {
        var template = await _catalog.GetAsync("GR", CancellationToken.None);

        Assert.Equal(ReviewStatus.Draft, template.ReviewStatus);
        Assert.Null(template.ReviewedBy);
        Assert.Null(template.ReviewedOn);
        Assert.NotNull(template.ReviewNotes);
    }

    [Fact]
    public async Task A_reviewed_template_is_served_where_drafts_are_not_and_other_resources_are_ignored()
    {
        var resources = new TemplateResources(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Maia.Templates.cy.json"] = Template("ClinicallyReviewed", "Dr. Maria Example, OB-GYN", "2026-11-02").Replace("GR", "CY", StringComparison.Ordinal),
            ["Maia.Strings.en.json"] = "not a template",
        });
        var release = new EmbeddedPlanTemplateCatalog(new TemplateCatalogOptions { AllowDraftTemplates = false }, resources);

        var template = await release.GetAsync("cy", CancellationToken.None);

        Assert.Equal(("CY", ReviewStatus.ClinicallyReviewed), (template.CountryCode, template.ReviewStatus));
        Assert.Equal(["CY"], release.SupportedCountries);
    }

    [Fact]
    public void Two_templates_for_one_country_are_refused()
    {
        var resources = new TemplateResources(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Maia.Templates.gr.json"] = Template("Draft", null, null),
            ["Maia.Templates.gr-copy.json"] = Template("Draft", null, null),
        });

        var ex = Assert.Throws<InvalidDataException>(() => new EmbeddedPlanTemplateCatalog(new TemplateCatalogOptions(), resources).SupportedCountries);
        Assert.Contains("Two templates", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_template_listed_but_not_there_is_reported()
    {
        var resources = new TemplateResources(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Maia.Templates.gr.json"] = null });

        var ex = Assert.Throws<InvalidDataException>(() => new EmbeddedPlanTemplateCatalog(new TemplateCatalogOptions(), resources).SupportedCountries);
        Assert.Contains("Missing resource", ex.Message, StringComparison.Ordinal);
    }

    private static MemoryStream Json(string json) => new(Encoding.UTF8.GetBytes(json));

    private static string Template(string status, string? reviewedBy, string? reviewedOn, string? notes = null)
    {
        static string Field(string name, string? value) => value is null ? string.Empty : $"\"{name}\":\"{value}\",";
        return "{\"schemaVersion\":1,\"id\":\"x\",\"country\":\"GR\",\"version\":2,\"reviewStatus\":\"" + status + "\","
            + Field("reviewedBy", reviewedBy) + Field("reviewedOn", reviewedOn) + Field("reviewNotes", notes)
            + "\"checks\":[{\"id\":\"a\",\"category\":\"Routine\",\"fromWeek\":20,\"toWeek\":24,\"title\":{\"en\":\"A\"},\"description\":{\"en\":\"A\"}}]}";
    }

    /// <summary>An assembly holding these resources; a null one is listed but can't be opened.</summary>
    private sealed class TemplateResources(Dictionary<string, string?> resources) : Assembly
    {
        public override string[] GetManifestResourceNames() => [.. resources.Keys];

        public override Stream? GetManifestResourceStream(string name) =>
            resources[name] is { } json ? new MemoryStream(Encoding.UTF8.GetBytes(json)) : null;
    }
}
