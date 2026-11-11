using Maia.Domain.Common;

namespace Maia.Domain.Plans;

public enum AttachmentKind
{
    /// <summary>A document such as a PDF report.</summary>
    Document = 0,

    /// <summary>A photo: a scan picture or a photographed paper report.</summary>
    Photo = 1,
}

/// <summary>
/// A result she keeps on Results. File content is never held in the domain: <see cref="BlobId"/>
/// points to encrypted storage owned by the infrastructure layer.
/// </summary>
public sealed record ResultAttachment
{
    public const int MaxNameLength = 200;
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;

    private ResultAttachment(
        AttachmentId id,
        AttachmentKind kind,
        string displayName,
        DateTimeOffset addedAt,
        string? blobId,
        string? mediaType,
        long? sizeBytes)
    {
        Id = id;
        Kind = kind;
        DisplayName = displayName;
        AddedAt = addedAt;
        BlobId = blobId;
        MediaType = mediaType;
        SizeBytes = sizeBytes;
    }

    public AttachmentId Id { get; }

    public AttachmentKind Kind { get; }

    public string DisplayName { get; }

    public DateTimeOffset AddedAt { get; }

    public string? BlobId { get; }

    public string? MediaType { get; }

    public long? SizeBytes { get; }

    public static ResultAttachment ForFile(
        AttachmentId id,
        string displayName,
        string mediaType,
        long sizeBytes,
        string blobId,
        DateTimeOffset addedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobId);
        if (sizeBytes is <= 0 or > MaxFileSizeBytes)
        {
            throw new DomainException("attachment.size.invalid", $"File size {sizeBytes} is outside the allowed range.");
        }

        var kind = mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? AttachmentKind.Photo
            : AttachmentKind.Document;
        return new ResultAttachment(id, kind, CleanName(displayName), addedAt, blobId, mediaType.ToLowerInvariant(), sizeBytes);
    }

    /// <summary>Rebuilds a stored attachment without re-running creation rules. Persistence only.</summary>
    public static ResultAttachment Rehydrate(
        AttachmentId id,
        AttachmentKind kind,
        string displayName,
        DateTimeOffset addedAt,
        string? blobId,
        string? mediaType,
        long? sizeBytes) =>
        new(id, kind, displayName, addedAt, blobId, mediaType, sizeBytes);

    /// <summary>The same result under a new name she gave it; the file, its kind and the day it was added stay.</summary>
    public ResultAttachment Renamed(string displayName) =>
        new(Id, Kind, CleanName(displayName), AddedAt, BlobId, MediaType, SizeBytes);

    private static string CleanName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength];
    }
}
