using Maia.Application.Abstractions;
using Maia.Application.Common;
using Maia.Application.Plans;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Application.Tests;

/// <summary>The plan service at its edges: no plan yet, files too big or not there, other countries' templates, clean-up that fails.</summary>
public sealed class PlanServiceEdgeTests : IDisposable
{
    private readonly FakeClock _clock = new(TestData.Today);
    private readonly InMemoryPlanRepository _plans = new();
    private readonly FlakyAttachmentStore _attachments = new();
    private readonly RecordingErrorReporter _errors = new();
    private readonly Templates _templates = new();
    private readonly PlanService _service;

    public PlanServiceEdgeTests()
    {
        _service = new PlanService(_plans, _templates, _attachments, new RecordingDataKey(), new InMemoryBackupFormat(), _clock, _errors);
    }

    public void Dispose() => _service.Dispose();

    private Task<PregnancyPlan> CreateAsync() =>
        _service.CreatePlanAsync(new CreatePlanRequest(TestData.DueDate, "GR", LocalizedText.English));

    [Fact]
    public async Task Without_a_plan_there_is_no_home_time_zone_to_keep()
    {
        Assert.False(await _service.KeepHomeTimeZoneAsync());
        Assert.Equal(0, _plans.SaveCount);
    }

    [Fact]
    public async Task A_plan_saved_before_Maia_kept_a_home_time_zone_takes_the_phone_s_once()
    {
        await _plans.SaveAsync(TestData.Plan(), CancellationToken.None);
        Assert.Null(_plans.Stored!.TimeZoneId);

        Assert.True(await _service.KeepHomeTimeZoneAsync());
        Assert.Equal("Europe/Athens", _plans.Stored!.TimeZoneId);

        var saves = _plans.SaveCount;
        _clock.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Assert.False(await _service.KeepHomeTimeZoneAsync());
        Assert.Equal("Europe/Athens", _plans.Stored!.TimeZoneId);
        Assert.Equal(saves, _plans.SaveCount);
    }

    [Fact]
    public async Task A_newer_template_for_another_country_never_changes_her_plan()
    {
        await CreateAsync();
        var saves = _plans.SaveCount;
        _templates.ByCountry["CY"] = new PlanTemplate("test-cy", "CY", version: 9, ReviewStatus.ClinicallyReviewed, TestData.Template().Checks);

        Assert.False(await _service.UpdateFromTemplateAsync());

        Assert.Equal(new TemplateReference("test-gr", 1), _plans.Stored!.Template);
        Assert.Equal(saves, _plans.SaveCount);
    }

    [Fact]
    public async Task A_file_over_the_size_limit_is_turned_away_before_anything_is_stored()
    {
        await CreateAsync();
        var file = new FileToAttach("scan.pdf", "application/pdf", new byte[AttachmentPolicy.MaxSizeBytes + 1]);

        var ex = await Assert.ThrowsAsync<AttachmentRejectedException>(() => _service.AttachFileAsync(file));

        Assert.Equal("attachment.size.too_large", ex.Code);
        Assert.Empty(_attachments.Inner.Blobs);
        Assert.Empty(_plans.Stored!.Results);
    }

    [Fact]
    public async Task A_result_without_a_stored_file_has_nothing_to_open()
    {
        var paper = ResultAttachment.Rehydrate(AttachmentId.New(), AttachmentKind.Document, "Paper report", DateTimeOffset.UnixEpoch, blobId: null, mediaType: null, sizeBytes: null);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _service.ReadResultFileAsync(paper));

        Assert.Equal("attachment.not_a_file", ex.Code);
    }

    [Fact]
    public async Task A_backup_needs_a_plan()
    {
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<PlanNotFoundException>(() => _service.ExportBackupAsync(output, "lemon tree by the sea"));

        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task A_result_file_that_cannot_be_deleted_is_reported_by_code_and_the_result_still_goes()
    {
        await CreateAsync();
        var id = await _service.AttachFileAsync(new FileToAttach("Glucose results.pdf", "application/pdf", new byte[] { 1, 2, 3 }));
        _attachments.FailDeletes = new IOException("Storage busy: Glucose results.pdf");

        await _service.RemoveResultAsync(id);

        Assert.Empty(_plans.Stored!.Results);
        var (exception, operation) = Assert.Single(_errors.Reports);
        Assert.Equal("attachment.delete", operation);
        Assert.Same(_attachments.FailDeletes, exception);
    }

    /// <summary>The shipped templates by country; Greece has the test template.</summary>
    private sealed class Templates : IPlanTemplateCatalog
    {
        public Dictionary<string, PlanTemplate> ByCountry { get; } = new(StringComparer.Ordinal) { ["GR"] = TestData.Template() };

        public IReadOnlyList<string> SupportedCountries => [.. ByCountry.Keys.Order(StringComparer.Ordinal)];

        public Task<PlanTemplate> GetAsync(string countryCode, CancellationToken cancellationToken) =>
            Task.FromResult(ByCountry[countryCode.ToUpperInvariant()]);
    }

    /// <summary>Result files in memory whose deletes can be made to fail, as when storage is busy.</summary>
    private sealed class FlakyAttachmentStore : IAttachmentStore
    {
        public InMemoryAttachmentStore Inner { get; } = new();

        public Exception? FailDeletes { get; set; }

        public Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => Inner.SaveAsync(content, cancellationToken);

        public Task<byte[]> ReadAsync(string blobId, CancellationToken cancellationToken) => Inner.ReadAsync(blobId, cancellationToken);

        public Task DeleteAsync(string blobId, CancellationToken cancellationToken) =>
            FailDeletes is { } failure ? Task.FromException(failure) : Inner.DeleteAsync(blobId, cancellationToken);

        public Task DeleteAllAsync(CancellationToken cancellationToken) => Inner.DeleteAllAsync(cancellationToken);

        public Task<IReadOnlyList<StoredBlob>> ListAsync(CancellationToken cancellationToken) => Inner.ListAsync(cancellationToken);
    }
}
