namespace Maia.Presentation.Tests;

/// <summary>Files in the repository, for checks that read the app's own source and resources.</summary>
internal static class Repo
{
    public static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Maia.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Maia.slnx not found above the test output.");
    }

    /// <summary>A path under <c>src/Maia.App</c>.</summary>
    public static string App(params string[] parts) => Path.Combine([Root(), "src", "Maia.App", .. parts]);

    /// <summary>The app's own C# files, without what the build generates.</summary>
    public static IEnumerable<string> AppSources() =>
        Directory.GetFiles(App(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>A file's code without its comment lines, so words in documentation are not taken for code.</summary>
    public static string CodeOf(string file) =>
        string.Join('\n', File.ReadAllLines(file).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
}
