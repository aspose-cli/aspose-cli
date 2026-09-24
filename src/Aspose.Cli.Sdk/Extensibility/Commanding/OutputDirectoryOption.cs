using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one <c>--out-dir</c> option of commands that publish a set of files. It has no
/// alias: <c>--out</c> always names a single output file.
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

        string path = paths.ResolveOutput(value);
        return File.Exists(path)
            ? throw CliErrors.OptionInvalid(Name, $"a file already exists at '{path}'", "Pass a directory path.")
            : path;
    }

    /// <summary>Resolves a required directory.</summary>
    public string ResolveRequired(ParseResult parse, PathResolver paths) =>
        Resolve(parse, paths)
            ?? throw CliErrors.OptionInvalid(Name, "is required", "Pass the directory that receives the files.");
}
