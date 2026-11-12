using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Publication;

namespace Aspose.Cli.Sdk.IO.Paths;

/// <summary>
/// Resolves user-supplied paths against the working directory (the
/// <c>--workdir</c> option, defaulting to the process working directory). A path the caller gave
/// in a parameter that is empty or only white space names no file, so it is refused as a mistake
/// of that parameter rather than resolved to the working directory.
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
    /// <param name="path">The path as given.</param>
    /// <param name="parameter">
    /// The option or argument the caller gave the path in, such as <c>--template</c> or
    /// <c>file</c>, which the error for an empty path names; null for a path that does not come
    /// from the command line or was checked already.
    /// </param>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> for an empty path given in <paramref name="parameter"/>;
    /// <c>FILE_NOT_FOUND</c> when the file does not exist.
    /// </exception>
    public string ResolveInput(string path, string? parameter = null)
    {
        string full = Resolve(path, parameter);
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
    /// <param name="path">The path as given.</param>
    /// <param name="parameter">The option or argument the caller gave the path in, as <see cref="ResolveInput"/> takes it.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for an empty path given in <paramref name="parameter"/>.</exception>
    public string ResolveOutput(string path, string? parameter = null) => Resolve(path, parameter);

    private string Resolve(string path, string? parameter)
    {
        if (parameter is not null && string.IsNullOrWhiteSpace(path))
        {
            throw CliErrors.OptionInvalid(parameter, "the value is empty",
                "Pass the path of a file or directory; relative paths resolve against --workdir (or the current directory).");
        }

        ArgumentException.ThrowIfNullOrEmpty(path);
        return Path.GetFullPath(path, BaseDirectory);
    }
}
