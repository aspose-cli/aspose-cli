namespace Aspose.Cli.TestKit;

/// <summary>Locates this independent project's root from the running test assembly.</summary>
public static class RepositoryPaths
{
    private static readonly Lazy<string> Located = new(Locate);

    /// <summary>The directory holding <c>eng/distribution.json</c>.</summary>
    public static string Root => Located.Value;

    private static string Locate()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "eng", "distribution.json")))
            {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("The project root (eng/distribution.json) was not found above the test assembly.");
    }
}
