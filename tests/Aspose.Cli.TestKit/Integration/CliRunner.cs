using System.Diagnostics.CodeAnalysis;
using Aspose.Cli.Sdk;

namespace Aspose.Cli.TestKit;

/// <summary>Finds a current CLI executable inside this independent project.</summary>
public static class CliRunner
{
    public const string ExecutableEnvironmentVariable = DistributionInfo.EnvironmentVariablePrefix + "TEST_EXECUTABLE";
    private static readonly Lazy<string> Executable = new(ResolveExecutable);
    public static string ExecutablePath => Executable.Value;

    /// <summary>The current CLI executable, or false when this run has none to test.</summary>
    public static bool TryGetExecutablePath([NotNullWhen(true)] out string? path)
    {
        try
        {
            path = Executable.Value;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException)
        {
            path = null;
            return false;
        }
    }

    private static string ResolveExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable(ExecutableEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string full = Path.GetFullPath(configured);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException("CLI executable not found.", full);
            }
            ValidateExecutable(full);
            return full;
        }
        string configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent?.Name ?? "Debug";
        string executable = GetExecutablePath(RepositoryPaths.Root, configuration);
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException($"Build this project's launcher or set {ExecutableEnvironmentVariable}.");
        }
        ValidateExecutable(executable);
        EnsureCurrentBuild(RepositoryPaths.Root, executable);
        return executable;
    }

    internal static string GetExecutablePath(string root, string configuration) =>
        Path.Combine(root, "src", "Aspose.Cli", "bin", configuration, "net10.0",
            DistributionInfo.CommandName + (OperatingSystem.IsWindows() ? ".exe" : ""));

    internal static void ValidateExecutable(string path)
    {
        string expected = DistributionInfo.CommandName + (OperatingSystem.IsWindows() ? ".exe" : "");
        if (!string.Equals(Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected this project's executable '{expected}'.");
        }
    }

    internal static IEnumerable<string> GetSourceRoots(string root)
    {
        yield return Path.Combine(root, "src");
    }

    internal static IEnumerable<string> GetBuildConfigurationInputs(string root)
    {
        foreach (string name in new[] { "Directory.Build.props", "Directory.Packages.props", "global.json", "nuget.config" })
        {
            yield return Path.Combine(root, name);
        }
        if (Directory.Exists(Path.Combine(root, "eng")))
        {
            foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "eng"), "*", SearchOption.AllDirectories))
            {
                yield return path;
            }
        }
    }

    private static void EnsureCurrentBuild(string root, string executable)
    {
        DateTime built = File.GetLastWriteTimeUtc(executable);
        IEnumerable<string> inputs = GetSourceRoots(root).Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Concat(GetBuildConfigurationInputs(root));
        string? newer = inputs.FirstOrDefault(path => File.Exists(path) && File.GetLastWriteTimeUtc(path) > built);
        if (newer is not null)
        {
            throw new InvalidOperationException($"Rebuild the CLI; its input '{Path.GetRelativePath(root, newer)}' is newer.");
        }
    }
}
