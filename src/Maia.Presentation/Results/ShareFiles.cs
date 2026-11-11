namespace Maia.Presentation.Results;

/// <summary>
/// How results are named and typed for the phone's share sheet. The receiving app often goes by the file's ending (a
/// result she renamed "20-week scan" would otherwise reach WhatsApp as an unknown file), and the share sheet offers the
/// apps that take every file shared.
/// </summary>
public static class ShareFiles
{
    private static readonly Dictionary<string, string> Endings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/heic"] = ".heic",
        ["image/heif"] = ".heif",
    };

    /// <summary>
    /// A file name for <paramref name="name"/>: characters a file name can't hold become "_", and the ending for
    /// <paramref name="mediaType"/> is added when the name doesn't have it ("20-week scan" → "20-week scan.jpg").
    /// </summary>
    public static string FileName(string name, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = string.Concat((name ?? string.Empty).Trim().Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c));
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "result";
        }

        return Endings.TryGetValue(mediaType, out var ending) && !safe.EndsWith(ending, StringComparison.OrdinalIgnoreCase)
            && !(ending == ".jpg" && safe.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                ? safe + ending
                : safe;
    }

    /// <summary>
    /// <paramref name="name"/> as it reads in Maia, without the file ending ("Photo Sat 3 Oct, 21:52.jpg" → "Photo Sat 3
    /// Oct, 21:52"): her tile already says photo or PDF. The name kept is unchanged, and sharing or opening the file
    /// elsewhere adds the ending back (<see cref="FileName"/>). Only the endings of the files Maia keeps go; a name that is
    /// only an ending stays.
    /// </summary>
    public static string WithoutEnding(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var ending in Endings.Values.Append(".jpeg"))
        {
            if (name.Length > ending.Length && name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^ending.Length];
            }
        }

        return name;
    }

    /// <summary>
    /// The one type the share sheet is asked for: the files' own type when they share one ("application/pdf"),
    /// "image/*" for photos of different kinds, and "*/*" for reports and photos together.
    /// </summary>
    public static string CommonType(IEnumerable<string> mediaTypes)
    {
        ArgumentNullException.ThrowIfNull(mediaTypes);
        var types = mediaTypes.Select(t => t.ToLowerInvariant()).Distinct().ToList();
        return types.Count switch
        {
            0 => "*/*",
            1 => types[0],
            _ when types.All(t => t.StartsWith("image/", StringComparison.Ordinal)) => "image/*",
            _ => "*/*",
        };
    }
}
