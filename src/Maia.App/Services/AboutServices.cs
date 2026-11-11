using Maia.Presentation.About;

namespace Maia.App.Services;

/// <summary>This build's version, as the phone reports it (ApplicationDisplayVersion and ApplicationVersion).</summary>
public sealed class MauiAppVersion : IAppVersion
{
    public string Version => AppInfo.Current.VersionString;

    public string Build => AppInfo.Current.BuildString;
}

/// <summary>The licence texts bundled from the repository's <c>licenses</c> folder (see the MauiAsset in the project).</summary>
public sealed class BundledLicenceTexts : ILicenceTexts
{
    public async Task<string> ReadAsync(string fileName)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("licences/" + fileName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
