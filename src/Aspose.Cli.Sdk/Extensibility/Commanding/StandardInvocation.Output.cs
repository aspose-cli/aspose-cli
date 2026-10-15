using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The one place that decides the file a command publishes and its format.</summary>
public partial class StandardInvocation
{
    private ResolvedOutput? _output;
    private ResolvedDirectory? _directoryOutput;

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

    /// <summary>The output file once the command resolved it, or null.</summary>
    internal ResolvedOutput? ResolvedFile => _output;

    /// <summary>
    /// The password for <see cref="Output"/>, or null when none was given. A password for a
    /// format that cannot carry one is refused, naming the option the caller passed, before the
    /// secret is read and before any input is loaded. When the output's extension names several
    /// formats, as <c>.xml</c> names WordML and Flat OPC, the edited input keeps its own: the
    /// overload that takes a detector of the input's format refuses that format, and while it is
    /// unknown every such format must be protectable. This overload uses the detector the product
    /// registered (<c>ProductDefinitionBuilder.DetectFormat</c>) for a menu command that edits its
    /// input, and otherwise judges <see cref="ResolvedOutput.Format"/>.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for an unprotectable format or a bad source.</exception>
    public Secret? EncryptPassword() =>
        EncryptPassword(DetectFormat is { } detect ? () => detect(Input) : null);

    /// <inheritdoc cref="EncryptPassword()"/>
    /// <param name="sourceFormat">
    /// Detects the format id of the edited input from its content without loading it, or returns
    /// null; called only for a password and an output whose extension names several formats.
    /// </param>
    public Secret? EncryptPassword(Func<string?>? sourceFormat)
    {
        PasswordOptions encrypt = Declared(_options.Encrypt, "output password");
        if (encrypt.SelectedOption(_parse) is { } option)
        {
            FormatDescriptor[] written = WrittenFormats(sourceFormat);
            if (written.Any(static format => !format.Protectable))
            {
                throw Unprotectable(option, written);
            }
        }

        return encrypt.Resolve(_parse, Inputs, ReadEnvironment);
    }

    // The format an output is written in: the edited input's own among those its extension names,
    // or for a set of files in a directory every format the command declares for them.
    private FormatDescriptor[] WrittenFormats(Func<string?>? sourceFormat)
    {
        OutputTarget target = Declared(_options.OutputTarget, "output");
        if (target.Kind == OutputKind.Directory)
        {
            return [.. target.Writes ?? throw new InvalidOperationException("A directory output that can be encrypted declares the formats of its files.")];
        }

        ResolvedOutput output = Output;
        if (output.Alternatives.Count < 2 || sourceFormat is null)
        {
            return [output.Format];
        }

        return sourceFormat() is { } id ? [output.Keeping(id)] : [.. output.Alternatives];
    }

    private CliException Unprotectable(string option, IReadOnlyList<FormatDescriptor> written)
    {
        FormatDescriptor[] protectable = [.. Writes(Declared(_options.OutputTarget, "output"))
            .Where(static candidate => candidate.Protectable)];
        string formats = string.Join(" or ", written.Select(static format => $"'{format.Id}'"));
        string reason = written.Count == 1
            ? $"the {formats} format cannot be password-protected"
            : $"the {formats} format of the edited file cannot be password-protected";
        string hint = Output.InPlace && protectable.Length > 0
            ? $"An in-place edit keeps the file's format; write a protectable copy with {StandardOptionNames.Out} instead, "
                + $"naming a file with one of {string.Join(", ", protectable.Select(static format => format.PreferredExtension).Distinct())}, "
                + $"or drop {option}."
            : $"Protect only {string.Join(", ", protectable.Select(static format => format.Id))} outputs, or drop {option}.";
        return CliErrors.OptionInvalid(option, reason, hint);
    }

    /// <summary>
    /// The directory the command publishes its files in, named by <c>--out-dir</c>, with
    /// <c>--overwrite</c>.
    /// </summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when it is missing or a file occupies the path.</exception>
    public ResolvedDirectory DirectoryOutput => _directoryOutput ??=
        new(Declared(_options.OutputDirectory, "output directory").ResolveRequired(_parse, Paths), Overwrite);

    /// <summary>
    /// Whether the caller asked for a dry run (<c>--dry-run</c>): the command checks its command
    /// line and the files it names, then stops before it runs. An edit still applies its
    /// operations in memory; every other command returns <see cref="DryRunResult"/> instead.
    /// </summary>
    public bool DryRun => _options.DryRun is { } dryRun && _parse.GetValue(dryRun);

    /// <summary>
    /// The result of a dry run of a command that is not an edit: what it resolved, which are
    /// its inputs and its output, and the paths and format it would write. Without
    /// <c>--overwrite</c>, a file the run would replace is refused as the run refuses it.
    /// </summary>
    /// <exception cref="CliException"><c>OUTPUT_EXISTS</c> for a file the run would or may replace.</exception>
    internal DryRunResult DryRunResult()
    {
        var checks = new List<string>();
        if (_inputChecked)
        {
            checks.Add(DryRunChecks.Input);
        }

        (string[] paths, string? parameter, IEnumerable<(string Path, bool Possible)> written) = _output is { } file
            ? Written(file)
            : _directoryOutput is { } directory ? Written(directory) : ([], null, []);
        if (paths.Length > 0)
        {
            checks.Add(DryRunChecks.Output);
        }

        bool overwrite = _output?.Overwrite ?? _directoryOutput?.Overwrite ?? true;
        if (!overwrite && written.FirstOrDefault(static target => File.Exists(target.Path)) is { Path: not null } existing)
        {
            throw CliErrors.OutputExists(existing.Path, parameter, existing.Possible);
        }

        return Contracts.DryRunResult.Of(checks, paths, _output?.Format.Id);
    }

    // The paths a dry run reports for an output file, and the files the run writes there: the
    // parts a selection numbers, else, when the parts depend on the document, the file itself and
    // the numbered (or, for other images, named) files beside it that the run may write.
    private (string[] Paths, string? Parameter, IEnumerable<(string Path, bool Possible)> Written) Written(ResolvedOutput file)
    {
        string parameter = _options.CreatedFile?.Name ?? StandardOptionNames.Out;
        string stem = Path.GetFileNameWithoutExtension(file.Path);
        string extension = Path.GetExtension(file.Path);
        IReadOnlyList<int>? numbers = NumberedParts.Of(_parse);
        if (numbers is { Count: > 0 })
        {
            string[] parts = [.. file.Parts(numbers)];
            return (parts, parameter, parts.Select(static part => (part, false)));
        }

        string? label = numbers is not null ? "[0-9]+" : file.Format.Image ? ".+" : null;
        return label is null
            ? ([file.Path], parameter, [(file.Path, false)])
            : ([file.Path], parameter, Beside(file.Directory, $"{Regex.Escape(stem)}\\.{label}{Regex.Escape(extension)}").Prepend(file.Path).Select(static path => (path, true)));
    }

    // The files of a split the run writes in the directory: those its command line numbers, else
    // the numbered files of the input there that it may write, whatever their extension when the
    // command does not declare the format of its parts.
    private (string[] Paths, string? Parameter, IEnumerable<(string Path, bool Possible)> Written) Written(ResolvedDirectory directory)
    {
        if (NumberedParts.Of(_parse) is not { } numbers || _input is null)
        {
            return ([directory.Path], StandardOptionNames.OutDir, []);
        }

        string stem = PartNames.SafeStem(Path.GetFileNameWithoutExtension(_input));
        if (numbers.Count > 0 && Declared(_options.OutputTarget, "output").Writes is [var format, ..] && format.PreferredExtension is { } extension)
        {
            string[] parts = [.. directory.Parts(stem, extension, numbers)];
            return (parts, StandardOptionNames.OutDir, parts.Select(static part => (part, false)));
        }

        string labels = numbers.Count > 0 ? $"({string.Join('|', PartNames.Numbered(numbers))})" : "[0-9]+";
        return ([directory.Path], StandardOptionNames.OutDir,
            Beside(directory.Path, $"{Regex.Escape(stem)}\\.{labels}\\.[^.]+").Select(static path => (path, true)));
    }

    // The files in a directory whose names match a pattern, ignoring case, in ordinal order.
    private static IEnumerable<string> Beside(string directory, string name)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var pattern = new Regex($"^{name}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return Directory.EnumerateFiles(directory)
            .Where(path => pattern.IsMatch(Path.GetFileName(path)))
            .Order(StringComparer.Ordinal);
    }

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
        throw CliErrors.Usage([$"Name the output format: give {StandardOptionNames.To} ({string.Join(", ", ids)}), "
            + $"or an {StandardOptionNames.Out} whose extension names one of them."], parameter: StandardOptionNames.To);
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
        FormatDescriptor? named = Writing(writes, output) is [var first, ..] ? first : null;
        string other = named is not null
            ? $", or pass {StandardOptionNames.To} {named.Id} to write {named.Id}"
            : string.Empty;
        return CliErrors.OutputExtensionInvalid(
            parameter,
            output,
            $"{StandardOptionNames.To} {format.Id} contradicts {parameter} '{output}':",
            problem,
            format.Extensions,
            mistyped: extension.Length > 1 && named is null,
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
        // A command that writes only images shows its input rather than keeping a copy of it, so
        // another format is written from the input; any other output is a document to convert after.
        bool images = writes.All(static format => format.Image);
        // An image command given its input's own extension was asked for what the input already is.
        bool inputs = images && !derived && extension.Length > 1 && _options.Input is { } input
            && _parse.GetValue(input) is { } given
            && string.Equals(Path.GetExtension(given), extension, StringComparison.OrdinalIgnoreCase);
        (string Format, string Command)? producer = extension.Length > 1 && !inputs ? Producer(extension, images && !derived ? output : null) : null;
        string problem = (extension.Length > 1, producer) switch
        {
            _ when inputs => $"has the {extension} extension of its input, which already is that file; {command} writes only images",
            (true, { } other) => $"has the {extension} extension of {other.Format}, which {command} does not write",
            (true, null) => $"has the {extension} extension, which names no format {command} writes",
            _ => $"has no extension, which names the format {command} writes",
        };
        string[] extensions = [.. writes.SelectMany(static format => format.Extensions).Distinct(StringComparer.OrdinalIgnoreCase)];
        // An option is named as written; a positional argument by its name, as in "the file".
        bool option = parameter.StartsWith('-');
        string fix = derived
            ? $"Name the output with {parameter} and one of these extensions"
            : $"Give {(option ? parameter : "the " + parameter)} one of these extensions";
        string hint = producer switch
        {
            { } line when images && !derived => $"{fix}, or run '{line.Command}' for {line.Format}.",
            { } line => $"{fix}, then run '{line.Command}' for {line.Format}.",
            _ => fix + ".",
        };
        return CliErrors.OutputExtensionInvalid(
            parameter,
            output,
            derived ? $"The input '{output}', whose name the output keeps without {parameter}," : $"{(option ? parameter : "The " + parameter)} '{output}'",
            problem,
            extensions,
            mistyped: extension.Length > 1 && producer is null && !inputs,
            hint,
            producer is { } named ? new JsonObject { ["format"] = named.Format } : []);
    }

    /// <summary>
    /// The format an extension names among those the commands beside this one write through
    /// their <c>--to</c>, and the command line that writes it: from this command's output, or,
    /// when <paramref name="output"/> is given, from this command's input straight to that output.
    /// </summary>
    /// <param name="extension">The extension the output was given.</param>
    /// <param name="output">The output as the caller gave it, for a command whose output is not a document to convert; otherwise null.</param>
    private (string Format, string Command)? Producer(string extension, string? output)
    {
        if (_parse.CommandResult.Parent is not CommandResult parent)
        {
            return null;
        }

        string[] path = CommandPath();
        string[] source = output is not null && _options.Input is { } input && _parse.GetValue(input) is { } given
            ? [Quoted(given)]
            : ["<that file>"];
        string[] target = output is null ? [] : [StandardOptionNames.Out, Quoted(output)];
        return parent.Command.Subcommands
            .Where(command => !command.Hidden && command != _parse.CommandResult.Command)
            .SelectMany(static command => command.Options
                .Select(StandardOptions.TargetOf)
                .OfType<TargetFormat>()
                .Select(target => (Command: command.Name, Target: target)))
            .Select(sibling => sibling.Target.Offered.DeclaringExtension(extension) is [var format, ..]
                ? ((string Format, string Command)?)(format.Id, string.Join(' ',
                    [DistributionInfo.CommandName, .. path[..^1], sibling.Command, .. source, StandardOptionNames.To, format.Id, .. target]))
                : null)
            .FirstOrDefault(static line => line is not null);
    }

    // A path as a command line names it: quoted when it holds a space.
    private static string Quoted(string path) => path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\"" : path;
}
