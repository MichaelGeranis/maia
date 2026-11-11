using System.Text.Json.Serialization;

namespace Maia.Infrastructure.Persistence;

/*
 * Storage shape of a plan. Deliberately separate from the domain model:
 *  - the domain can be refactored freely; only the mapper changes;
 *  - the stored format evolves through SchemaVersion and PlanDocumentMigrator;
 *  - enums are stored as strings so reordering them never corrupts data.
 * Never remove or rename a field without bumping SchemaVersion and adding a migration.
 */

internal sealed record PlanDocument
{
    public const int CurrentSchemaVersion = 9;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required Guid Id { get; init; }

    public required long Revision { get; init; }

    public required DateOnly DueDate { get; init; }

    public required string TemplateId { get; init; }

    public required int TemplateVersion { get; init; }

    public required string Language { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public List<CheckDocument> Checks { get; init; } = [];

    public List<TaskDocument> Tasks { get; init; } = [];

    public List<ContactDocument> Contacts { get; init; } = [];

    /// <summary>Added in schema 2.</summary>
    public List<QuestionDocument> Questions { get; init; } = [];

    /// <summary>
    /// Added in schema 4: the plan's home time zone (an IANA id), which every booking time is in. Missing for a plan
    /// saved before, until Maia sets it to the phone's zone.
    /// </summary>
    public string? TimeZone { get; init; }

    /// <summary>
    /// Added in schema 6: every result she keeps, apart from the entries on her journey. Until schema 6 a result could be
    /// filed under an entry (each check's <c>results</c>) or belong to none (<c>untaggedResults</c>, schema 5). Schema 5
    /// also gave entries she added a <see cref="CheckDocument.Kind"/>, and made her next visit (schema 3's
    /// <c>nextVisit</c>) a booked doctor-visit entry.
    /// </summary>
    public List<AttachmentDocument> Results { get; init; } = [];

    /// <summary>Added in schema 9: the blood pressure readings she logged.</summary>
    public List<ReadingDocument> BloodPressure { get; init; } = [];
}

internal sealed record CheckDocument
{
    public required string Id { get; init; }

    public required string Origin { get; init; }

    public required Dictionary<string, string> Title { get; init; }

    public Dictionary<string, string>? Description { get; init; }

    public required string Category { get; init; }

    public required int FromWeek { get; init; }

    public required int ToWeek { get; init; }

    public required string State { get; init; }

    public AppointmentDocument? Appointment { get; init; }

    /// <summary>Written before entries were done by their booking or a trimester or week; read by migrations only.</summary>
    public DateOnly? CompletedOn { get; init; }

    /// <summary>Added in schema 5: what an entry she added is (an <c>EntryKind</c>); absent for a check from the template.</summary>
    public string? Kind { get; init; }

    /// <summary>
    /// The trimester (a <c>Trimester</c>), when she gave only that; absent otherwise. <c>AfterBirth</c> from schema 8.
    /// </summary>
    public string? Trimester { get; init; }

    /// <summary>Whether she has booked, moved, marked or renamed it (<c>PlannedCheck.IsEdited</c>); absent when not.</summary>
    public bool? Edited { get; init; }
}

internal sealed record AppointmentDocument
{
    public required DateOnly Date { get; init; }

    public TimeOnly? Time { get; init; }

    public string? Location { get; init; }

    public Guid? ContactId { get; init; }
}

internal sealed record AttachmentDocument
{
    public required Guid Id { get; init; }

    public required string Kind { get; init; }

    public required string DisplayName { get; init; }

    public required DateTimeOffset AddedAt { get; init; }

    public string? BlobId { get; init; }

    public string? MediaType { get; init; }

    public long? SizeBytes { get; init; }
}

internal sealed record TaskDocument
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public DateOnly? DueDate { get; init; }

    public TimeOnly? DueTime { get; init; }

    /// <summary>Added in schema 8: the trimester (a <c>Trimester</c>) a task without a date is for; absent otherwise.</summary>
    public string? Trimester { get; init; }

    /// <summary>Added in schema 8: the pregnancy week a task without a date is for; absent otherwise.</summary>
    public int? Week { get; init; }

    public bool IsDone { get; init; }
}

internal sealed record ReadingDocument
{
    public required Guid Id { get; init; }

    public required int Systolic { get; init; }

    public required int Diastolic { get; init; }

    public required DateOnly Date { get; init; }

    public required TimeOnly Time { get; init; }
}

internal sealed record QuestionDocument
{
    public required Guid Id { get; init; }

    public required string Text { get; init; }

    public bool IsAnswered { get; init; }
}

internal sealed record ContactDocument
{
    public required Guid Id { get; init; }

    public required string Role { get; init; }

    public required string DisplayName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Label { get; init; }

    public string? DeviceContactId { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(PlanDocument))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class PersistenceJsonContext : JsonSerializerContext;
