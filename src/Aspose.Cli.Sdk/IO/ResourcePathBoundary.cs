namespace Aspose.Cli.Sdk.IO;

/// <summary>Checks lexical and resolved local resource paths against one document root.</summary>
public sealed class ResourcePathBoundary
{
    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly string _resolvedRoot;
    private readonly string _resolvedRootPrefix;

    public ResourcePathBoundary(string rootDirectory)
    {
        _root = Normalize(rootDirectory);
        _rootPrefix = WithSeparator(_root);
        _resolvedRoot = ResolveLink(new DirectoryInfo(_root));
        _resolvedRootPrefix = WithSeparator(_resolvedRoot);
    }

    /// <summary>Returns whether a path and every resolved link remain below the root.</summary>
    public bool Contains(string path)
    {
        string fullPath = Normalize(path);
        if (!IsSameOrInside(_root, _rootPrefix, fullPath))
        {
            return false;
        }

        string relative = Path.GetRelativePath(_root, fullPath);
        string resolved = _resolvedRoot;
        foreach (string segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(resolved, segment);
            FileSystemInfo? info = Directory.Exists(candidate)
                ? new DirectoryInfo(candidate)
                : File.Exists(candidate)
                    ? new FileInfo(candidate)
                    : null;
            resolved = info is null ? Normalize(candidate) : ResolveLink(info);
            if (!IsSameOrInside(_resolvedRoot, _resolvedRootPrefix, resolved))
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolveLink(FileSystemInfo info)
    {
        if (info.LinkTarget is null)
        {
            return Normalize(info.FullName);
        }

        FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
        return Normalize(target?.FullName ?? info.FullName);
    }

    private static bool IsSameOrInside(string root, string rootPrefix, string path) =>
        path.Equals(root, PathComparison) || path.StartsWith(rootPrefix, PathComparison);

    private static string Normalize(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? pathRoot = Path.GetPathRoot(fullPath);
        return pathRoot is not null && fullPath.Equals(pathRoot, PathComparison)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string WithSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
