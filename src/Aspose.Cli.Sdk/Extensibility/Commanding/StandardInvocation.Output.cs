using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The one place that decides the file a command publishes and its format.</summary>
public partial class StandardInvocation
{
    private ResolvedOutput? _output;

    /// <summary>
    /// The file the command publishes, resolved once. Its format is the one <c>--to</c> names
    /// when the command has a <c>--to</c>; otherwise the format among those the command writes
    /// that the named output's extension declares; otherwise the edited input's format, the
    /// <c>--to</c> default or the command's only format. The output's extension must be one of
    /// that format's: a missing or foreign extension is refused before any input is read.
    /// </summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> for an output that names an input, a contradictory edit mode or
    /// a format nothing names; <c>USAGE_ERROR</c> for an extension that is not the format's;
    /// <c>FILE_NOT_FOUND</c> for a missing input an output is derived from.
    /// </exception>
    public ResolvedOutput Output => _output ??= ResolveOutput();

    /// <summary>
    /// The password for <see cref="Output"/>, or null when none was given. A password for a
    /// format that cannot carry one is refused, naming the option the caller passed, before the
    /// secret is read.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for an unprotectable format or a bad source.</exception>
    public Secret? EncryptPassword()
    {
        PasswordOptions encrypt = Declared(_options.Encrypt, "output password");
        FormatDescriptor format = Output.Format;
        if (!format.Protectable && encrypt.SelectedOption(_parse) is { } option)
        {
            IEnumerable<string> protectable = Writes(Declared(_options.OutputTarget, "output"))
                .Where(static candidate => candidate.Protectable)
                .Select(static candidate => candidate.Id);
            throw CliErrors.OptionInvalid(
                option,
                $"the '{format.Id}' format cannot be password-protected",
                $"Protect only {string.Join(", ", protectable)} outputs, or drop {option}.");
        }

        return encrypt.Resolve(_parse, Inputs, ReadEnvironment);
    }

    /// <summary>
    /// The directory the command publishes its files in, named by <c>--out-dir</c>, with
    /// <c>--overwrite</c>.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when it is missing or a file occupies the path.</exception>
    public ResolvedDirectory DirectoryOutput =>
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

        IReadOnlyList<FormatDescriptor> writes = Writes(target);
        (FormatDescriptor format, IReadOnlyList<FormatDescriptor> alternatives) = ResolveFormat(target, writes);
        if (target.Kind == OutputKind.Mutation)
        {
            return Mutated(format, alternatives);
        }

        string path = target.Kind == OutputKind.CreatedFile
            ? CreatedPath
            : _options.OutputFile!.Required
                ? _options.OutputFile.ResolveRequired(_parse, Paths, DeclaredInputs())
                : RequestedOutputPath() ?? Derived(
                    OutputFileOption.DerivePath(Input, target.DerivedMarker + format.PreferredExtension),
                    inPlaceAvailable: false);
        return new ResolvedOutput(format, path, Overwrite, inPlace: false, backupPath: null, alternatives);
    }

    // The command template refuses an output file that has neither --to nor declared formats.
    private IReadOnlyList<FormatDescriptor> Writes(OutputTarget target) => _options.Target?.Offered ?? target.Writes!;

    private (FormatDescriptor Format, IReadOnlyList<FormatDescriptor> Alternatives) ResolveFormat(
        OutputTarget target,
        IReadOnlyList<FormatDescriptor> writes)
    {
        (string Parameter, string Value)? named = NamedOutput();
        if (RequestedTarget() is { } requested)
        {
            if (named is var (parameter, value) && !requested.Names(value))
            {
                throw NotTheFormatsExtension(parameter, value, requested, writes);
            }

            return (requested, []);
        }

        if (named is var (option, output))
        {
            return Writing(writes, output) is [var first, ..] alternatives
                ? (first, alternatives)
                : throw NamesNoFormatWritten(option, output, writes, derived: false);
        }

        if (target.Kind == OutputKind.Mutation)
        {
            string input = _parse.GetRequiredValue(_options.Input!);
            return Writing(writes, input) is [var first, ..] alternatives
                ? (first, alternatives)
                : throw NamesNoFormatWritten(StandardOptionNames.Out, input, writes, derived: true);
        }

        if (_options.Target?.Default is { } fallback)
        {
            return (_options.Target.Named(fallback)!, []);
        }

        if (writes is [var only])
        {
            return (only, []);
        }

        string[] ids = [.. writes.Select(static format => format.Id)];
        throw CliErrors.OptionInvalid(
            StandardOptionNames.To,
            "the output format is not named",
            $"Pass {StandardOptionNames.To} {string.Join(", ", ids)}, or name an {StandardOptionNames.Out} with one of their extensions.",
            Mistake.Of(string.Empty, ids));
    }

    // The formats among those written that declare the file's extension, in declared order.
    private static FormatDescriptor[] Writing(IReadOnlyList<FormatDescriptor> writes, string file) =>
        [.. writes.Where(format => format.Names(file))];

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
        FormatDescriptor format,
        IReadOnlyList<FormatDescriptor> writes)
    {
        string extension = Path.GetExtension(output);
        string problem = extension.Length > 1
            ? $"its {extension} extension is not a {format.Id} extension"
            : $"it has no extension, and a {format.Id} output needs one";
        string other = Writing(writes, output) is [var named, ..]
            ? $", or pass {StandardOptionNames.To} {named.Id} to write {named.Id}"
            : string.Empty;
        return CliErrors.OutputExtensionInvalid(
            parameter,
            output,
            $"{StandardOptionNames.To} {format.Id} contradicts {parameter} '{output}':",
            problem,
            format.Extensions,
            $"Give {parameter} the {string.Join(" or ", format.Extensions)} extension, {CorrectedPaths(output, format)}{other}.",
            new JsonObject { ["format"] = format.Id });
    }

    /// <summary>
    /// The paths the caller most likely meant by <paramref name="output"/>: the output with the
    /// format's extension and, for an output without one, which often names a folder, a file
    /// inside that folder, beside which an output of several parts numbers its files.
    /// </summary>
    private static string CorrectedPaths(string output, FormatDescriptor format)
    {
        string preferred = format.PreferredExtension ?? string.Empty;
        if (Path.GetExtension(output).Length > 1)
        {
            return $"as in {Path.ChangeExtension(output, preferred)}";
        }

        string folder = output.TrimEnd('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $"as in {folder}{preferred}, or name a file inside the {folder} folder, as in {Path.Combine(folder, "page" + preferred)}; "
            + "an output of several parts is written beside that file as numbered files";
    }

    /// <param name="parameter">The option or argument that names the output.</param>
    /// <param name="output">The named output, or the input an edit's output is derived from.</param>
    /// <param name="writes">The formats the command writes.</param>
    /// <param name="derived">Whether <paramref name="output"/> is the input, since no output was named.</param>
    private CliException NamesNoFormatWritten(
        string parameter,
        string output,
        IReadOnlyList<FormatDescriptor> writes,
        bool derived)
    {
        string command = string.Join(' ', [DistributionInfo.CommandName, .. CommandPath()]);
        string extension = Path.GetExtension(output);
        (string Format, string Command)? producer = extension.Length > 1 ? Producer(extension) : null;
        string problem = (extension.Length > 1, producer) switch
        {
            (true, { } other) => $"asks for {other.Format}, which {command} does not write",
            (true, null) => $"has the {extension} extension, which names no format {command} writes",
            _ => $"has no extension, which names the format {command} writes",
        };
        string[] extensions = [.. writes.SelectMany(static format => format.Extensions).Distinct(StringComparer.OrdinalIgnoreCase)];
        string fix = derived
            ? $"Name the output with {parameter} and one of these extensions"
            : $"Give {parameter} one of these extensions";
        return CliErrors.OutputExtensionInvalid(
            parameter,
            output,
            derived ? $"The input '{output}', whose name the output keeps without {parameter}," : $"{parameter} '{output}'",
            problem,
            extensions,
            producer is { } line ? $"{fix}, then run '{line.Command}' for {line.Format}." : fix + ".",
            new JsonObject
            {
                ["requested"] = producer?.Format,
                ["supported"] = new JsonArray([.. writes.Select(static format => JsonValue.Create(format.Id))]),
            });
    }

    /// <summary>
    /// The format an extension names among those the commands beside this one write through
    /// their <c>--to</c>, and the command line that writes it from this command's output.
    /// </summary>
    private (string Format, string Command)? Producer(string extension)
    {
        if (_parse.CommandResult.Parent is not CommandResult parent)
        {
            return null;
        }

        string[] path = CommandPath();
        return parent.Command.Subcommands
            .Where(command => !command.Hidden && command != _parse.CommandResult.Command)
            .SelectMany(static command => command.Options
                .Select(StandardOptions.TargetOf)
                .OfType<TargetFormat>()
                .Select(target => (Command: command.Name, Target: target)))
            .Select(sibling => sibling.Target.Offered.DeclaringExtension(extension) is [var format, ..]
                ? ((string Format, string Command)?)(format.Id, string.Join(' ',
                    [DistributionInfo.CommandName, .. path[..^1], sibling.Command, "<that file>", StandardOptionNames.To, format.Id]))
                : null)
            .FirstOrDefault(static line => line is not null);
    }
}
