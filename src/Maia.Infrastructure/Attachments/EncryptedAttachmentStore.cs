using Maia.Application.Abstractions;
using Maia.Infrastructure.Security;

namespace Maia.Infrastructure.Attachments;

/// <summary>
/// Stores each result file encrypted under a random blob id. File names and types are kept in the
/// plan, never on disk, so the folder reveals nothing about its contents.
/// </summary>
public sealed class EncryptedAttachmentStore : IAttachmentStore
{
    private const string Extension = ".blob";
    private const string TempExtension = ".tmp";

    private readonly string _directory;
    private readonly AesGcmCipher _cipher;

    public EncryptedAttachmentStore(string directory, AesGcmCipher cipher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _cipher = cipher;
    }

    /// <summary>Whether any result file is stored in <paramref name="directory"/>, readable or not.</summary>
    public static bool HasStoredFiles(string directory) =>
        Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*" + Extension).Any();

    public async Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var blobId = Guid.NewGuid().ToString("N");
        var envelope = await _cipher.EncryptAsync(content, PurposeFor(blobId), cancellationToken).ConfigureAwait(false);

        // Written in full and flushed under a temp name first, so a crash never leaves a torn file under the blob's id.
        var tempPath = Path.Combine(_directory, blobId + TempExtension);
        try
        {
            await DurableFile.WriteAsync(tempPath, envelope, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, PathFor(blobId));
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        return blobId;
    }

    public async Task<byte[]> ReadAsync(string blobId, CancellationToken cancellationToken)
    {
        var envelope = await File.ReadAllBytesAsync(PathFor(blobId), cancellationToken).ConfigureAwait(false);
        return await _cipher.DecryptAsync(envelope, PurposeFor(blobId), cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string blobId, CancellationToken cancellationToken)
    {
        var path = PathFor(blobId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        foreach (var blobId in ListBlobIds().ToList())
        {
            File.Delete(PathFor(blobId));
        }

        // And any temp file a crash left half-way through a save.
        if (Directory.Exists(_directory))
        {
            foreach (var path in Directory.EnumerateFiles(_directory, "*" + TempExtension).ToList())
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredBlob>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredBlob> blobs = ListBlobIds()
            .Where(IsValidId)
            .Select(id => new StoredBlob(id, new DateTimeOffset(File.GetLastWriteTimeUtc(PathFor(id)), TimeSpan.Zero)))
            .ToList();
        return Task.FromResult(blobs);
    }

    /// <summary>Blob ids that exist on disk; used to find orphans after a crash.</summary>
    public IEnumerable<string> ListBlobIds() =>
        Directory.Exists(_directory)
            ? Directory.EnumerateFiles(_directory, "*" + Extension).Select(Path.GetFileNameWithoutExtension).OfType<string>()
            : [];

    private static string PurposeFor(string blobId) => $"blob:{blobId}";

    private static bool IsValidId(string blobId) => blobId.Length == 32 && blobId.All(char.IsAsciiHexDigitLower);

    private string PathFor(string blobId)
    {
        // Ids are generated here as 32 hex chars; anything else is a bug or an attack (path traversal).
        if (!IsValidId(blobId))
        {
            throw new ArgumentException($"Invalid blob id '{blobId}'.", nameof(blobId));
        }

        return Path.Combine(_directory, blobId + Extension);
    }
}
