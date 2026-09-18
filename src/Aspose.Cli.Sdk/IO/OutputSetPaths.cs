using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Computes a common local directory without changing the filesystem.</summary>
internal static class OutputSetPaths
{
    internal static string CommonDirectory(IEnumerable<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        string[] paths = directories.Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))).ToArray();
        if (paths.Length == 0) { throw new ArgumentException("At least one output directory is required.", nameof(directories)); }
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string root = paths[0];
        foreach (string path in paths)
        {
            OutputPathValidator.EnsureSafeDirectory(path);
            if (!string.Equals(Path.GetPathRoot(root), Path.GetPathRoot(path), comparison))
            { throw CliErrors.OutputUnwritable(path, "all outputs in one transaction must share a filesystem root", phase: "output-set-admission"); }
            while (!string.Equals(path, root, comparison) && !path.StartsWith(
                Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, comparison))
            { root = Path.GetDirectoryName(root)!; }
        }
        return root;
    }
}
