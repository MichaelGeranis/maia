using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Presentation.Localization;

namespace Maia.Presentation.Results;

/// <summary>
/// A result as a row or tile: "Anomaly scan report.pdf" / "PDF · 410 KB".
/// </summary>
public sealed record ResultItem(AttachmentId Id, string Name, string Meta, string KindLabel, AttachmentKind Kind)
{
    public bool IsPhoto => Kind == AttachmentKind.Photo;

    public bool IsReport => Kind == AttachmentKind.Document;

    public static ResultItem From(ResultAttachment result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var label = KindLabelOf(result);
        var meta = result.SizeBytes is { } size ? $"{label} · {FileSize(size)}" : label;
        return new ResultItem(result.Id, ShareFiles.WithoutEnding(result.DisplayName), meta, label, result.Kind);
    }

    /// <summary>"PDF" or "Photo".</summary>
    internal static string KindLabelOf(ResultAttachment result) =>
        Text.Get(result.Kind == AttachmentKind.Photo ? "Results_KindPhoto" : "Results_KindPdf");

    /// <summary>"2.3 MB" above a megabyte, else "410 KB" (never "0 KB").</summary>
    internal static string FileSize(long bytes) => bytes >= 1024 * 1024
        ? Text.Format("Results_SizeMb", (bytes / (1024d * 1024)).ToString("0.0", Text.Culture))
        : Text.Format("Results_SizeKb", Math.Max(1, (int)Math.Round(bytes / 1024d)));
}
