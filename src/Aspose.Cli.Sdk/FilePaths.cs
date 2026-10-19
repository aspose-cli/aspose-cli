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
    /// The spelling of a path in a cross-process lock name or an identity hash: upper case
    /// (invariant) on Windows, as spelled elsewhere. The publication locks of other Aspose CLI
    /// distributions name their lock files and mutexes with these bytes, so they never change.
    /// </summary>
    public static string LockKey(string path) => LockKey(path, OperatingSystem.IsWindows());

    /// <summary>The <see cref="LockKey(string)"/> of a path on Windows or elsewhere.</summary>
    internal static string LockKey(string path, bool windows)
    {
        ArgumentNullException.ThrowIfNull(path);
        return windows ? path.ToUpperInvariant() : path;
    }

    /// <summary>
    /// The spelling of a path in a key that older versions fold to upper case (invariant) on
    /// every platform, such as the update status file name. Its bytes never change.
    /// </summary>
    public static string FoldedKey(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.ToUpperInvariant();
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
}
