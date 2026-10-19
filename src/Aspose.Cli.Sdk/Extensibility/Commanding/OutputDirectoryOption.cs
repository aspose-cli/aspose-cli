using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--out-dir</c> option of commands that publish a set of files into a directory
/// that may exist. It has no alias: <c>--out</c> always names one output, a file or, for
/// <c>review</c>, its evidence directory as a whole: one that does not exist yet, or with
/// <c>--overwrite</c> an empty one or an earlier evidence, which it replaces.
/// </summary>
internal sealed class OutputDirectoryOption
{
    private const string Name = StandardOptionNames.OutDir;
    private readonly Option<string?> _directory;

    /// <summary>Creates the option with command-specific help.</summary>
    public OutputDirectoryOption(string description, bool required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        _directory = new Option<string?>(Name)
        {
            Description = description,
            Required = required,
        }.WithInput(InputKind.None);
    }

    /// <summary>Adds the option to one product command.</summary>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(_directory);
    }

    /// <summary>
    /// Resolves the directory against the invocation directory, or returns null when the
    /// option was omitted. The directory may not exist yet; an existing file is refused.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when a file occupies the path.</exception>
    public string? Resolve(ParseResult parse, PathResolver paths)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(paths);
        if (parse.GetValue(_directory) is not { } value)
        {
            return null;
        }

        string path = OutputDirectories.Resolve(parse, paths, value, Name);
        return File.Exists(path)
            ? throw CliErrors.OptionInvalid(Name, $"a file already exists at '{path}'", "Pass a directory path.")
            : path;
    }

    /// <summary>Resolves a required directory.</summary>
    public string ResolveRequired(ParseResult parse, PathResolver paths) =>
        Resolve(parse, paths)
            ?? throw CliErrors.OptionInvalid(Name, "is required", "Pass the directory that receives the files.");
}

/// <summary>
/// The one decision on the name of a directory output, <c>--out-dir</c> and the evidence
/// directory of <c>review --out</c> alike: it is never <c>-</c>, and a directory to be created is
/// never named like a file of a format some command of the tree writes, such as
/// <c>parts.html</c>, which is most likely a file name given where a directory belongs.
/// </summary>
public static class OutputDirectories
{
    /// <summary>
    /// Resolves the directory <paramref name="value"/> names against the invocation directory.
    /// An existing directory is taken as it is named; a file there is the command's to refuse.
    /// </summary>
    /// <param name="parse">The parsed command line, whose command tree declares the formats.</param>
    /// <param name="paths">The resolver of the invocation directory.</param>
    /// <param name="value">The directory as given.</param>
    /// <param name="parameter">The option that names it, such as <c>--out-dir</c>.</param>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> for <c>-</c>, and for a path where nothing exists yet whose
    /// extension is that of a format a command writes.
    /// </exception>
    public static string Resolve(ParseResult parse, PathResolver paths, string value, string parameter)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        OutputFileOption.RefuseStandardStream(value, parameter, directory: true);
        string path = paths.ResolveOutput(value);
        string trimmed = value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string extension = Path.GetExtension(trimmed);
        if (extension.Length > 1 && !Directory.Exists(path) && !File.Exists(path)
            && Written(parse.RootCommandResult.Command, extension) is { } format)
        {
            throw CliErrors.OptionInvalid(
                parameter,
                $"'{value}' names a directory, but its {extension} extension is that of a {format.Id} file",
                $"{parameter} names the directory that receives the files; name it without the file extension, "
                    + $"as in {parameter} {trimmed[..^extension.Length]}.");
        }

        return path;
    }

    // The first format, in tree order, that a command of the tree writes through its --to under the extension.
    private static FormatDescriptor? Written(Command root, string extension) =>
        Commands(root)
            .SelectMany(static command => command.Options)
            .Select(StandardOptions.TargetOf)
            .OfType<TargetFormat>()
            .Select(target => target.Offered.DeclaringExtension(extension).FirstOrDefault())
            .FirstOrDefault(static format => format is not null);

    private static IEnumerable<Command> Commands(Command command) =>
        command.Subcommands.SelectMany(Commands).Prepend(command);
}
