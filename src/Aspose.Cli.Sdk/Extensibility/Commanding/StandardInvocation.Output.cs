using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one place that decides the file a command publishes and its format, except an edit's
/// format: that is its input's, which the edit decides once the input is open.
/// </summary>
public partial class StandardInvocation
{
    private ResolvedOutput? _output;
    private ResolvedDirectory? _directoryOutput;

    /// <summary>
    /// The file the command publishes, resolved once. Its format is the one <c>--to</c> names
    /// when the command has a <c>--to</c>; otherwise the format among those the command writes
    /// that the named output's extension chooses (<see cref="FormatDescriptor.ChosenByExtension"/>);
    /// otherwise the <c>--to</c> default or the command's only format.
    /// The output's extension must be one of that format's: a missing or foreign extension is
    /// refused before any input is read. An edit, whose format is its input's, resolves only
    /// <see cref="EditOutput"/> here.
    /// </summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> for an output that names an input or a format nothing names;
    /// <c>USAGE_ERROR</c> for an extension that is not the format's or
    /// that only formats <c>--to</c> alone chooses declare;
    /// <c>FILE_NOT_FOUND</c> for a missing input an output is derived from.
    /// </exception>
    public ResolvedOutput Output => _output ??= ResolveOutput();

    /// <summary>The output file once the command resolved it, or null.</summary>
    internal ResolvedOutput? ResolvedFile => _output;

    /// <summary>
    /// The password for the command's output, or null when none was given. A password for a
    /// format that cannot carry one is refused, naming the option the caller passed, before the
    /// secret is read and before any input is loaded. An edit writes its input's format, which
    /// the edit judges once the input is open (<see cref="Tables.TableEdit"/>).
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for an unprotectable format or a bad source.</exception>
    public Secret? EncryptPassword()
    {
        PasswordOptions encrypt = Declared(_options.Encrypt, "output password");
        if (encrypt.SelectedOption(_parse) is { } option
            && WrittenFormats().FirstOrDefault(static format => !format.Protectable) is { } unprotectable)
        {
            throw CliErrors.Unprotectable(option, unprotectable.Id, [.. _options.Protects.Select(static format => format.Id)]);
        }

        return encrypt.Resolve(_parse, Inputs, ReadEnvironment);
    }

    /// <summary>
    /// The option that gave the output password (<c>--encrypt</c>, <c>--encrypt-env</c> or
    /// <c>--encrypt-stdin</c>), which an error about its use names; null when none was given.
    /// </summary>
    internal string? EncryptOption => _options.Encrypt?.SelectedOption(_parse);

    // The formats an output is written in, as far as they are known before any input opens: for a
    // set of files in a directory every format the command declares for them, and for an edit none.
    private FormatDescriptor[] WrittenFormats()
    {
        OutputTarget target = Declared(_options.OutputTarget, "output");
        return target.Kind switch
        {
            OutputKind.Directory => [.. target.Writes ?? throw new InvalidOperationException("A directory output that can be encrypted declares the formats of its files.")],
            OutputKind.Mutation => [],
            _ => [Output.Format],
        };
    }

    /// <summary>
    /// The directory the command publishes its files in, named by <c>--out-dir</c>, with
    /// <c>--overwrite</c>.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when it is missing or a file occupies the path.</exception>
    public ResolvedDirectory DirectoryOutput => _directoryOutput ??=
        new(Declared(_options.OutputDirectory, "output directory").ResolveRequired(_parse, Paths), Overwrite);

    /// <summary>Whether the caller named the format with <c>--to</c>.</summary>
    public bool TargetRequested => _options.To is { } to && _parse.GetResult(to) is { Implicit: false };

    private ResolvedOutput ResolveOutput()
    {
        OutputTarget target = Declared(_options.OutputTarget, "output");
        if (target.Kind == OutputKind.Directory)
        {
            throw new InvalidOperationException("A command that publishes a directory resolves no output file.");
        }

        if (target.Kind == OutputKind.Mutation)
        {
            throw new InvalidOperationException("An edit writes its input's format, decided once the input is open: it resolves EditOutput.");
        }

        FormatDescriptor format = ResolveFormat(Writes(target));
        string path = target.Kind == OutputKind.CreatedFile
            ? CreatedPath
            : _options.OutputFile!.Required
                ? _options.OutputFile.ResolveRequired(_parse, Paths, DeclaredInputs())
                : RequestedOutputPath() ?? Derived(OutputFileOption.DerivePath(Input, target.DerivedMarker + format.PreferredExtension));
        return new ResolvedOutput(format, path, Overwrite);
    }

    // The command template refuses an output file that has neither --to nor declared formats.
    private IReadOnlyList<FormatDescriptor> Writes(OutputTarget target) => _options.Target?.Offered ?? target.Writes!;

    private FormatDescriptor ResolveFormat(IReadOnlyList<FormatDescriptor> writes)
    {
        (string Parameter, string Value)? named = NamedOutput();
        if (RequestedTarget() is { } requested)
        {
            if (named is var (parameter, value) && !requested.Names(value))
            {
                throw NotTheFormatsExtension(parameter, value, requested);
            }

            return requested;
        }

        if (named is var (option, output))
        {
            return ChosenBy(writes, option, output) is [var first, ..]
                ? first
                : throw NamesNoFormatWritten(option, output, writes);
        }

        if (_options.Target?.Default is { } fallback)
        {
            return _options.Target.Named(fallback)!;
        }

        if (writes is [var only])
        {
            return only;
        }

        string[] ids = [.. writes.Select(static format => format.Id)];
        if (_options.Target is null)
        {
            // A command without --to names its format only by the extension of its output.
            throw CliErrors.Usage([$"Name the output: give an {StandardOptionNames.Out} whose extension names one of the formats it writes ({string.Join(", ", ids)})."],
                parameter: StandardOptionNames.Out);
        }

        throw CliErrors.Usage([$"Name the output format: give {StandardOptionNames.To} ({string.Join(", ", ids)}), "
            + $"or an {StandardOptionNames.Out} whose extension names one of them."], parameter: StandardOptionNames.To);
    }

    // The formats among those written that the named output's extension chooses, in declared
    // order. An extension that only formats --to alone chooses declare is refused, naming them.
    private static FormatDescriptor[] ChosenBy(IReadOnlyList<FormatDescriptor> writes, string parameter, string output)
    {
        FormatDescriptor[] declaring = [.. writes.Where(format => format.Names(output))];
        FormatDescriptor[] chosen = [.. declaring.Where(static format => format.ChosenByExtension)];
        if (chosen.Length > 0 || declaring.Length == 0)
        {
            return chosen;
        }

        string option = parameter.StartsWith('-') ? parameter : "the " + parameter;
        throw CliErrors.Usage(
            [$"Name the output format: the {Path.GetExtension(output)} extension of {option} '{output}' is shared by "
                + $"{Listed(declaring, conjunction: "and")}, which only {StandardOptionNames.To} chooses."],
            parameter: StandardOptionNames.To);
    }

    // Format ids as a sentence lists them, as in "a-1, a-2 or a-3", each after a prefix.
    private static string Listed(IReadOnlyList<FormatDescriptor> formats, string prefix = "", string conjunction = "or") =>
        Listed([.. formats.Select(format => prefix + format.Id)], conjunction);

    // Items as a sentence lists them, as in "a, b or c".
    private static string Listed(string[] items, string conjunction = "or") =>
        items.Length == 1 ? items[0] : $"{string.Join(", ", items[..^1])} {conjunction} {items[^1]}";

    /// <summary>The format an explicit <c>--to</c> names, or null when it was not given.</summary>
    private FormatDescriptor? RequestedTarget()
    {
        if (!TargetRequested)
        {
            return null;
        }

        // The parser refuses a value that names no offered format.
        return _options.Target!.Named(_parse.GetValue(_options.To!)!)
            ?? throw new InvalidOperationException("--to names no offered format.");
    }

    private static CliException NotTheFormatsExtension(
        string parameter,
        string output,
        FormatDescriptor format)
    {
        string extension = Path.GetExtension(output);
        string problem = extension.Length > 1
            ? $"its {extension} extension is not an extension of {format.Id}"
            : $"it has no extension, and an output in {format.Id} needs one";
        return CliErrors.OutputExtensionInvalid(
            parameter,
            output,
            $"{StandardOptionNames.To} {format.Id} contradicts {parameter} '{output}':",
            problem,
            format.Extensions,
            new JsonObject { ["format"] = format.Id });
    }

    /// <param name="parameter">The option or argument that names the output.</param>
    /// <param name="output">The named output.</param>
    /// <param name="writes">The formats the command writes.</param>
    private CliException NamesNoFormatWritten(
        string parameter,
        string output,
        IReadOnlyList<FormatDescriptor> writes)
    {
        string command = string.Join(' ', [DistributionInfo.CommandName, .. CommandPath()]);
        string extension = Path.GetExtension(output);
        bool images = writes.All(static format => format.Image);
        // An image command given its input's own extension was asked for what the input already is.
        bool inputs = images && extension.Length > 1 && _options.Input is { } input
            && _parse.GetValue(input) is { } given
            && string.Equals(Path.GetExtension(given), extension, StringComparison.OrdinalIgnoreCase);
        string[] extensions = [.. writes.SelectMany(static format => format.Extensions).Distinct(StringComparer.OrdinalIgnoreCase)];
        IReadOnlyList<FormatDescriptor>? producer = extension.Length > 1 && !inputs ? Producer(extension) : null;

        string problem = (extension.Length > 1, producer) switch
        {
            _ when inputs => $"has the {extension} extension of its input, which already is that file; {command} writes only images",
            (true, { } other) => $"has the {extension} extension of {Listed(other, conjunction: "and")}, which {command} does not write",
            (true, null) => $"has the {extension} extension, which names no format {command} writes",
            _ => $"has no extension, which names the format {command} writes",
        };
        // An option is named as written; a positional argument by its name, as in "the file".
        bool option = parameter.StartsWith('-');
        return CliErrors.OutputExtensionInvalid(
            parameter,
            output,
            $"{(option ? parameter : "The " + parameter)} '{output}'",
            problem,
            extensions,
            producer is [var named] ? new JsonObject { ["format"] = named.Id } : []);
    }

    // The visible commands beside this one, with the formats each writes through its --to.
    private IEnumerable<(string Command, TargetFormat Target)> SiblingTargets() =>
        _parse.CommandResult.Parent is not CommandResult parent
            ? []
            : parent.Command.Subcommands
                .Where(command => command != _parse.CommandResult.Command)
                .SelectMany(static command => command.Options
                    .Select(StandardOptions.TargetOf)
                    .OfType<TargetFormat>()
                    .Select(target => (command.Name, target)));

    /// <summary>
    /// The format an extension chooses among those the commands beside this one write through
    /// their <c>--to</c>, or every format it names when only <c>--to</c> chooses them, as
    /// an archive extension may name several archival profiles; null when none names it.
    /// </summary>
    /// <param name="extension">The extension the output was given.</param>
    private IReadOnlyList<FormatDescriptor>? Producer(string extension) =>
        SiblingTargets()
            .Select(sibling => Named(sibling.Target.Offered.DeclaringExtension(extension)))
            .FirstOrDefault(static formats => formats.Length > 0);

    // The first format an extension chooses among those that declare it, or all of them when
    // only --to chooses them.
    private static FormatDescriptor[] Named(IReadOnlyList<FormatDescriptor> declaring) =>
        declaring.FirstOrDefault(static format => format.ChosenByExtension) is { } chosen ? [chosen] : [.. declaring];
}
