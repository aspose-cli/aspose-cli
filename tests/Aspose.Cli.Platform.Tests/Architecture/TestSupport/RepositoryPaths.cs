namespace Aspose.Cli.Architecture.Tests;

/// <summary>Locates repository-owned inputs used by architecture gates.</summary>
internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "Directory.Packages.props"))
                && File.Exists(Path.Combine(
                    directory.FullName,
                    "eng", "distribution.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root.");
    }
}
