using Maia.Application.Abstractions;

namespace Maia.Infrastructure.Security;

/// <summary>Where the data key is kept: the phone's keystore in the app (MAUI SecureStorage), memory in tests.</summary>
public interface ISecretStore
{
    /// <returns>The value, or null when there is none (or, on Android, when the keystore could no longer decrypt it).</returns>
    Task<string?> GetAsync(string name);

    Task SetAsync(string name, string value);

    void Remove(string name);
}

/// <summary>
/// Her files are on this phone but the key that opens them is missing from the keystore or can't be read. Maia never
/// makes a new key over them: it could never open them, and saving with it would mix two keys.
/// </summary>
public sealed class DataKeyUnavailableException : Exception
{
    public DataKeyUnavailableException()
        : base("The data key is not available.")
    {
    }

    public DataKeyUnavailableException(string message)
        : base(message)
    {
    }

    public DataKeyUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The 256-bit data key, kept in the phone's keystore (ADR 0002) and cached in memory once read.
/// <para>
/// A key is made on first use, but only while no encrypted file of hers exists. If her files are there and the key is
/// missing (the files came to a new phone without it, or the keystore was reset) or the keystore fails, it throws
/// <see cref="DataKeyUnavailableException"/> and leaves everything as it is: the start-up screen then lets her restore a
/// backup or start again. <see cref="ForgetAsync"/> removes the key once her files are deleted.
/// </para>
/// </summary>
public sealed class DataKeyProvider : IKeyProvider, IDataKey, IDisposable
{
    public const string KeyName = "maia.data-key.v1";

    private readonly ISecretStore _secrets;
    private readonly Func<bool> _hasEncryptedFiles;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile byte[]? _key;

    /// <param name="secrets">The keystore.</param>
    /// <param name="hasEncryptedFiles">Whether any file encrypted with the data key is on the phone (a plan or a result).</param>
    public DataKeyProvider(ISecretStore secrets, Func<bool> hasEncryptedFiles)
    {
        _secrets = secrets;
        _hasEncryptedFiles = hasEncryptedFiles;
    }

    /// <exception cref="DataKeyUnavailableException">Her files exist but their key doesn't, or the keystore failed.</exception>
    public async ValueTask<byte[]> GetKeyAsync(CancellationToken cancellationToken)
    {
        if (_key is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_key is { } loaded)
            {
                return loaded;
            }

            var key = await ReadStoredKeyAsync().ConfigureAwait(false);
            if (key is null)
            {
                if (_hasEncryptedFiles())
                {
                    throw new DataKeyUnavailableException("Encrypted files exist but their data key is missing.");
                }

                key = AesGcmCipher.GenerateKey();
                await StoreKeyAsync(key).ConfigureAwait(false);
            }

            _key = key;
            return key;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Removes the key from the keystore and from memory. The next <see cref="GetKeyAsync"/> makes a new one, as long
    /// as her files are gone by then.
    /// </summary>
    public async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _secrets.Remove(KeyName);

            // Dropped, not zeroed: a read or write that already holds the key finishes with it (the cipher copies it),
            // and zeroing it under that write would encrypt her file with an all-zero key.
            _key = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    /// <returns>The stored key, or null when there is none or it isn't a key Maia made.</returns>
    private async Task<byte[]?> ReadStoredKeyAsync()
    {
        string? stored;
        try
        {
            stored = await _secrets.GetAsync(KeyName).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DataKeyUnavailableException("The keystore could not be read.", ex);
        }

        if (stored is null)
        {
            return null;
        }

        var key = new byte[AesGcmCipher.KeySizeBytes];
        return Convert.TryFromBase64String(stored, key, out var written) && written == AesGcmCipher.KeySizeBytes ? key : null;
    }

    private async Task StoreKeyAsync(byte[] key)
    {
        var encoded = Convert.ToBase64String(key);
        try
        {
            await _secrets.SetAsync(KeyName, encoded).ConfigureAwait(false);

            // Android's SecureStorage drops a write without a word when it can't open its encrypted preferences. A key
            // that only lives in memory would leave everything saved today unreadable after a restart.
            if (await _secrets.GetAsync(KeyName).ConfigureAwait(false) != encoded)
            {
                throw new DataKeyUnavailableException("The keystore did not keep the new data key.");
            }
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or DataKeyUnavailableException))
        {
            throw new DataKeyUnavailableException("The keystore could not store the data key.", ex);
        }
    }
}
