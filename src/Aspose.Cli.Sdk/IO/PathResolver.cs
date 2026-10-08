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
    /// The absolute path <paramref name="path"/> names relative to <paramref name="baseDirectory"/>,
    /// or null when it is not a valid path.
    /// </summary>
    public static string? TryResolve(string baseDirectory, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
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
}
