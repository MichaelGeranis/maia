using System.Security.Cryptography;
using System.Text;

namespace Maia.Infrastructure.Security;

/// <summary>Supplies the 256-bit data key. On devices it lives in the platform keystore.</summary>
public interface IKeyProvider
{
    ValueTask<byte[]> GetKeyAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Authenticated encryption for everything Maia writes to disk.
/// <para>
/// Envelope: <c>"MAIA"</c> magic (4) · format version (1) · nonce (12) · tag (16) · ciphertext.
/// The caller passes a <em>purpose</em> (for example <c>plan</c> or <c>blob:3f2a…</c>) that is bound
/// as associated data, so a file cannot be swapped into another slot without detection.
/// </para>
/// </summary>
public sealed class AesGcmCipher
{
    public const int KeySizeBytes = 32;
    public const byte FormatVersion = 1;

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] Magic = "MAIA"u8.ToArray();
    private static readonly int HeaderSize = Magic.Length + 1 + NonceSize + TagSize;

    private readonly IKeyProvider _keys;

    public AesGcmCipher(IKeyProvider keys)
    {
        _keys = keys;
    }

    public async Task<byte[]> EncryptAsync(ReadOnlyMemory<byte> plaintext, string purpose, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        var key = await GetValidKeyAsync(cancellationToken).ConfigureAwait(false);

        var output = new byte[HeaderSize + plaintext.Length];
        Magic.CopyTo(output, 0);
        output[Magic.Length] = FormatVersion;
        var nonce = output.AsSpan(Magic.Length + 1, NonceSize);
        var tag = output.AsSpan(Magic.Length + 1 + NonceSize, TagSize);
        var ciphertext = output.AsSpan(HeaderSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext.Span, ciphertext, tag, Encoding.UTF8.GetBytes(purpose));
        CryptographicOperations.ZeroMemory(key);
        return output;
    }

    /// <exception cref="CryptographicException">The data is corrupt, tampered with, or for another purpose.</exception>
    public async Task<byte[]> DecryptAsync(ReadOnlyMemory<byte> envelope, string purpose, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (envelope.Length < HeaderSize || !envelope.Span[..Magic.Length].SequenceEqual(Magic))
        {
            throw new CryptographicException("Not a Maia encrypted file.");
        }

        var version = envelope.Span[Magic.Length];
        if (version != FormatVersion)
        {
            throw new CryptographicException($"Unsupported encryption format version {version}.");
        }

        var key = await GetValidKeyAsync(cancellationToken).ConfigureAwait(false);
        var nonce = envelope.Span.Slice(Magic.Length + 1, NonceSize);
        var tag = envelope.Span.Slice(Magic.Length + 1 + NonceSize, TagSize);
        var ciphertext = envelope.Span[HeaderSize..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(purpose));
        CryptographicOperations.ZeroMemory(key);
        return plaintext;
    }

    /// <summary>Generates a new random data key, for a key provider's first run.</summary>
    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(KeySizeBytes);

    /// <summary>Reads the format version without decrypting; used by migrations.</summary>
    public static byte? PeekVersion(ReadOnlySpan<byte> envelope) =>
        envelope.Length > Magic.Length && envelope[..Magic.Length].SequenceEqual(Magic) ? envelope[Magic.Length] : null;

    private async Task<byte[]> GetValidKeyAsync(CancellationToken cancellationToken)
    {
        var key = await _keys.GetKeyAsync(cancellationToken).ConfigureAwait(false);
        if (key.Length != KeySizeBytes)
        {
            throw new CryptographicException($"Data key must be {KeySizeBytes} bytes.");
        }

        // Copy so zeroing after use never touches the provider's cached key.
        var copy = new byte[KeySizeBytes];
        key.CopyTo(copy, 0);
        return copy;
    }
}

/// <summary>Holds a key in memory. For tests and for platforms that load the key once at start-up.</summary>
public sealed class InMemoryKeyProvider : IKeyProvider
{
    private readonly byte[] _key;

    public InMemoryKeyProvider(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _key = (byte[])key.Clone();
    }

    public ValueTask<byte[]> GetKeyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_key);
}
