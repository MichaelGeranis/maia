using Maia.Infrastructure.Security;

namespace Maia.App.Services;

/// <summary>
/// The phone's keystore, through MAUI SecureStorage (Android: preferences encrypted with a Keystore key; iOS: Keychain).
/// It holds only the data key; <see cref="DataKeyProvider"/> decides when a key may be made. Android backup and
/// device-to-device transfer are off (AndroidManifest.xml), because files restored without their Keystore key could
/// never be opened.
/// </summary>
public sealed class SecureStorageSecretStore : ISecretStore
{
    public Task<string?> GetAsync(string name) => SecureStorage.Default.GetAsync(name);

    public Task SetAsync(string name, string value) => SecureStorage.Default.SetAsync(name, value);

    public void Remove(string name) => SecureStorage.Default.Remove(name);
}
