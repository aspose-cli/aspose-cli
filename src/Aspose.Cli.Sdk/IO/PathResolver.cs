using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Resolves user-supplied paths against the working directory (the
/// <c>--workdir</c> option, defaulting to the process working directory).
/// </summary>
public sealed class PathResolver
{
    public PathResolver(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        BaseDirectory = Path.GetFullPath(baseDirectory);
    }

    /// <summary>Absolute base directory for relative paths.</summary>
    public string BaseDirectory { get; }

    /// <summary>Resolves an input path and verifies the file exists.</summary>
    /// <exception cref="CliException"><c>FILE_NOT_FOUND</c> when it does not exist.</exception>
    public string ResolveInput(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string full = Path.GetFullPath(path, BaseDirectory);
        return File.Exists(full) ? full : throw CliErrors.FileNotFound(full);
    }

    /// <summary>
    /// Resolves a product-managed local resource beneath the invocation root.
    /// Unlike a primary input, a resource path must be relative and may not
    /// traverse a symbolic link, junction, or other reparse point.
    /// </summary>
    public string ResolveLocalResource(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (Path.IsPathRooted(path)
            || Uri.TryCreate(path, UriKind.Absolute, out _)
            || path.Contains(':'))
        {
            throw UnsafeResource(path);
        }

        string full = Path.GetFullPath(path, BaseDirectory);
        string root = BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string prefix = root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw UnsafeResource(path);
        }

        string? cursor = full;
        while (cursor is not null
               && cursor.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(cursor) || Directory.Exists(cursor))
            {
                FileAttributes attributes = File.GetAttributes(cursor);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw UnsafeResource(path);
                }
            }
            if (string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            cursor = Path.GetDirectoryName(cursor);
        }

        return File.Exists(full) ? full : throw CliErrors.FileNotFound(full);
    }

    /// <summary>
    /// Resolves an output path. Existence is checked later by
    /// <see cref="SafeFileWriter"/> under the overwrite policy.
    /// </summary>
    public string ResolveOutput(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Path.GetFullPath(path, BaseDirectory);
    }

    private static CliException UnsafeResource(string path) =>
        CliErrors.OptionInvalid(
            "resource path",
            $"'{path}' is outside the invocation resource root or traverses a reparse point",
            "Use a relative local file beneath --workdir without '..', symbolic links, junctions, UNC paths, URLs, or device paths.");
}
