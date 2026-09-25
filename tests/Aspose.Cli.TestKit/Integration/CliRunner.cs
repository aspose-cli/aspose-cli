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

    /// <summary>
    /// Rejects a launcher output older than its sources. An incremental build rewrites only the
    /// assemblies whose projects changed, not the apphost, so each project's sources are compared
    /// with its own assembly in the launcher output. Inputs shared by every project are compared
    /// with the newest assembly there, since a change to them rebuilds the projects.
    /// </summary>
    internal static void EnsureCurrentBuild(string root, string executable)
    {
        string output = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        var projectDirectories = new List<string>();
        DateTime newestAssembly = DateTime.MinValue;
        foreach (string sourceRoot in GetSourceRoots(root).Where(Directory.Exists))
        {
            foreach (string project in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories).Where(path => !IsBuildOutput(root, path)))
            {
                string directory = Path.GetDirectoryName(project)!;
                projectDirectories.Add(directory);
                // The launcher project owns the executable; any other project ships as its own assembly.
                string assembly = IsWithin(directory, executable)
                    ? Path.ChangeExtension(Path.GetFullPath(executable), ".dll")
                    : Path.Combine(output, Path.GetFileNameWithoutExtension(project) + ".dll");
                if (!File.Exists(assembly))
                {
                    continue; // Build-time projects such as analyzers never reach the launcher output.
                }

                DateTime built = File.GetLastWriteTimeUtc(assembly);
                newestAssembly = built > newestAssembly ? built : newestAssembly;
                ThrowIfNewer(root, SourceFiles(root, directory), built);
            }
        }

        IEnumerable<string> shared = GetSourceRoots(root).Where(Directory.Exists)
            .SelectMany(sourceRoot => SourceFiles(root, sourceRoot))
            .Where(path => !projectDirectories.Any(directory => IsWithin(directory, path)))
            .Concat(GetBuildConfigurationInputs(root));
        ThrowIfNewer(root, shared, newestAssembly);
    }

    // A product's committed ops.schema.json is written from its records and is not a build input.
    private static IEnumerable<string> SourceFiles(string root, string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(root, path) && !string.Equals(Path.GetFileName(path), "ops.schema.json", StringComparison.OrdinalIgnoreCase));

    private static bool IsBuildOutput(string root, string path) =>
        Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static bool IsWithin(string directory, string path) =>
        Path.GetFullPath(path).StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void ThrowIfNewer(string root, IEnumerable<string> inputs, DateTime built)
    {
        string? newer = inputs.FirstOrDefault(path => File.Exists(path) && File.GetLastWriteTimeUtc(path) > built);
        if (newer is not null)
        {
            throw new InvalidOperationException($"Rebuild the CLI; its input '{Path.GetRelativePath(root, newer)}' is newer.");
        }
    }
}
