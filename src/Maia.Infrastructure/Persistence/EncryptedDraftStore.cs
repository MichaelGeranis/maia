using System.Security.Cryptography;
using Maia.Application.Abstractions;
using Maia.Infrastructure.Security;

namespace Maia.Infrastructure.Persistence;

/// <summary>
/// Keeps each draft as one encrypted file (<c>setup.maia</c>), written to a temp file first and renamed into place.
/// A draft is only ever a convenience: one that is damaged, or whose key went with deleted data, reads as none.
/// </summary>
public sealed class EncryptedDraftStore : IDraftStore
{
    private const string Extension = ".maia";

    private readonly string _directory;
    private readonly AesGcmCipher _cipher;

    public EncryptedDraftStore(string directory, AesGcmCipher cipher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(cipher);
        _directory = directory;
        _cipher = cipher;
    }

    public async Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var envelope = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return await _cipher.DecryptAsync(envelope, Purpose(name), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is CryptographicException or DataKeyUnavailableException or IOException)
        {
            // Torn, damaged, or encrypted with a key that was forgotten with her data: no draft.
            return null;
        }
    }

    public async Task WriteAsync(string name, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        Directory.CreateDirectory(_directory);
        var envelope = await _cipher.EncryptAsync(content, Purpose(name), cancellationToken).ConfigureAwait(false);
        var temp = path + ".tmp";
        await DurableFile.WriteAsync(temp, envelope, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        var path = PathFor(name);
        if (Directory.Exists(_directory))
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }

        return Task.CompletedTask;
    }

    private static string Purpose(string name) => "draft:" + name;

    private string PathFor(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.All(c => char.IsAsciiLetterLower(c) || c == '-'))
        {
            throw new ArgumentException("A draft name is lower-case letters and dashes.", nameof(name));
        }

        return Path.Combine(_directory, name + Extension);
    }
}
