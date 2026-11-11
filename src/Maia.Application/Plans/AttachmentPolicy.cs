using Maia.Domain.Plans;

namespace Maia.Application.Plans;

/// <summary>Which files can be attached as results. Kept deliberately narrow: reports and photos.</summary>
public static class AttachmentPolicy
{
    public static readonly IReadOnlySet<string> AllowedMediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/heic",
        "image/heif",
        "image/webp",
    };

    public static long MaxSizeBytes => ResultAttachment.MaxFileSizeBytes;

    public static bool IsAllowed(string mediaType) => AllowedMediaTypes.Contains(mediaType);
}
