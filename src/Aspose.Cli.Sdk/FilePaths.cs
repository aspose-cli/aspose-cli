namespace Aspose.Cli.Sdk;

/// <summary>
/// How the CLI compares local file-system paths (decision D12). Windows file names ignore case,
/// so two spellings of one path are the same path: comparisons are ordinal and ignore case.
/// Callers pass full paths; this type does not normalize them. Keys derived from a path fold
/// case to a value instead, and their bytes are shared with older versions and other Aspose CLI
/// distributions.
/// </summary>
public static class FilePaths
{
    /// <summary>Compares or collects paths: equal paths are one entry.</summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>The comparison for an <c>Equals</c>, <c>StartsWith</c> or <c>Contains</c> on paths.</summary>
    public static StringComparison Comparison => StringComparison.OrdinalIgnoreCase;

    /// <summary>Whether two paths name the same path as spelled, without resolving links or short names.</summary>
    public static bool Same(string? left, string? right) => string.Equals(left, right, Comparison);

    /// <summary>
    /// The spelling of a path in an identity hash: upper case (invariant) on Windows, as spelled
    /// elsewhere. The license identity hashes these bytes, so they never change.
    /// </summary>
    public static string LockKey(string path) => LockKey(path, OperatingSystem.IsWindows());

    /// <summary>The <see cref="LockKey(string)"/> of a path on Windows or elsewhere.</summary>
    internal static string LockKey(string path, bool windows)
    {
        ArgumentNullException.ThrowIfNull(path);
        return windows ? path.ToUpperInvariant() : path;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is below <paramref name="root"/>, or is the root itself when
    /// <paramref name="allowEqual"/>. A root that ends in a separator, such as a drive root
    /// <c>C:\</c>, is not given a second one, and <c>C:\ab</c> is not below <c>C:\a</c>.
    /// </summary>
    public static bool IsWithin(string root, string path, bool allowEqual)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        if (Same(root, path)) { return allowEqual; }
        return path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, Comparison);
    }

    /// <summary>
    /// Whether a path is a UNC path (<c>\\server\share</c>, also written <c>//server/share</c>)
    /// or a device path (<c>\\?\</c>, <c>\\.\</c>): neither names a plain local file.
    /// </summary>
    public static bool IsRemoteOrDevice(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>Whether a path is a device path (<c>\\?\</c>, <c>\\.\</c>, also written with <c>/</c>), as opposed to a UNC path.</summary>
    public static bool IsDevice(string path) =>
        IsRemoteOrDevice(path) && path.Length > 3 && path[2] is '?' or '.' && path[3] is '\\' or '/';

    /// <summary>
    /// The plain path a device path spells, <c>C:\x</c> for <c>\\?\C:\x</c> and <c>\\s\p</c> for
    /// <c>\\?\UNC\s\p</c>, and any other path itself; null for a device path that spells no plain
    /// path, such as <c>\\.\PhysicalDrive0</c>.
    /// </summary>
    public static string? Plain(string path)
    {
        if (!IsDevice(path))
        {
            return path;
        }

        string rest = path[4..];
        return rest is ['U' or 'u', 'N' or 'n', 'C' or 'c', '\\' or '/', _, ..] ? @"\\" + rest[4..]
            : rest is [var drive, ':', '\\' or '/', ..] && char.IsAsciiLetter(drive) ? rest
            : null;
    }
}
