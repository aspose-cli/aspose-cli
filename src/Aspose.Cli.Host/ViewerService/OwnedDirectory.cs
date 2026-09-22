namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// Proves the deletion boundary for Host-owned preview directories. Deletion
/// is allowed only for a direct child with an expected name and a bounded tree
/// containing no links or reparse points.
/// </summary>
internal static class OwnedDirectory
{
    private const int MaximumCleanupEntries = 131_072;
    private const int MaximumCleanupDepth = 20;

    public static bool CanDelete(
        string parentDirectory,
        string candidateDirectory,
        Func<string, bool> isOwnedName)
    {
        ArgumentNullException.ThrowIfNull(isOwnedName);
        try
        {
            string parent = Normalize(parentDirectory);
            string candidate = Normalize(candidateDirectory);
            return Directory.Exists(candidate)
                && DirectoriesEqual(Path.GetDirectoryName(candidate), parent)
                && isOwnedName(Path.GetFileName(candidate))
                && IsRealDirectory(parent)
                && IsRealDirectory(candidate)
                && HasBoundedRealTree(candidate);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return false;
        }
    }

    private static bool HasBoundedRealTree(string root)
    {
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((root, 0));
        int entries = 0;
        while (pending.Count > 0)
        {
            (string current, int depth) = pending.Pop();
            if (depth > MaximumCleanupDepth)
            {
                return false;
            }

            foreach (FileSystemInfo item in
                new DirectoryInfo(current).EnumerateFileSystemInfos())
            {
                entries++;
                if (entries > MaximumCleanupEntries
                    || (item.Attributes & FileAttributes.ReparsePoint) != 0
                    || item.LinkTarget is not null)
                {
                    return false;
                }

                if (item is DirectoryInfo directory)
                {
                    pending.Push((directory.FullName, depth + 1));
                }
                else if (item is not FileInfo)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsRealDirectory(string path) =>
        Directory.Exists(path)
        && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
        && new DirectoryInfo(path).LinkTarget is null;

    private static bool DirectoriesEqual(string? left, string right) =>
        left is not null
        && string.Equals(
            Normalize(left),
            right,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
