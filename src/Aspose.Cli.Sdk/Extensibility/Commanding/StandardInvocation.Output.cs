using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>The one place that decides the file a command publishes and its format.</summary>
public partial class StandardInvocation
{
    private ResolvedOutput? _output;
    private ResolvedDirectory? _directoryOutput;

    /// <summary>
    /// The file the command publishes, resolved once. Its format is the one <c>--to</c> names
    /// when the command has a <c>--to</c>; otherwise the format among those the command writes
    /// that the named output's extension chooses (<see cref="FormatDescriptor.ChosenByExtension"/>);
    /// otherwise the edited input's format, the <c>--to</c> default or the command's only format.
    /// The output's extension must be one of that format's: a missing or foreign extension is
    /// refused before any input is read.
    /// </summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> for an output that names an input, a contradictory edit mode or
    /// a format nothing names; <c>USAGE_ERROR</c> for an extension that is not the format's or
    /// that only formats <c>--to</c> alone chooses declare;
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

    /// <summary>
    /// The option that names the output password's environment variable and the variable the
    /// caller named in it, or null when the caller named none.
    /// </summary>
    internal (string Option, string Variable)? EncryptEnvironment() =>
        _options.Encrypt is { } encrypt && encrypt.EnvironmentName(_parse) is { } variable
            ? (encrypt.EnvironmentOption, variable)
            : null;

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
            : EncryptedAfterwards(option, written)
                ?? $"Protect only {string.Join(", ", protectable.Select(static format => format.Id))} outputs, or drop {option}.";
        return CliErrors.OptionInvalid(option, reason, hint);
    }

    // The hint that writes the output without a password, then encrypts it in place with the edit
    // that encrypts its format (ProtectionAdvice.EncryptInPlace), with the variable the caller named
    // for the output's password; null when no edit encrypts the format or the command line holds a
    // secret given literally, which a hint never repeats.
    private string? EncryptedAfterwards(string option, IReadOnlyList<FormatDescriptor> written)
    {
        if (written is not [var format] || _options.Encrypt is not { } encrypt)
        {
            return null;
        }

        string password = encrypt.EnvironmentName(_parse) ?? ContinuationCommand.PasswordPlaceholder;
        return ProtectionAdvice.EncryptInPlace(_parse.RootCommandResult.Command, format.Id, Output.Path, password) is { } encrypted
            && Again([.. encrypt.Options]) is { } plain
                ? $"Write the {format.Id} file without {option}, then encrypt it in place with a password to open it that grants every permission: "
                    // What the first command cannot repeat is said right after it.
                    + $"run {plain.Command.Quoted()}{plain.Repeating}, then {encrypted.Quoted()}{ProtectionAdvice.Naming(password)}."
                : null;
    }

    /// <summary>The options that name the output's password, which an edit that only encrypts takes the place of.</summary>
    internal IReadOnlyList<Option> OutputPasswordOptions => _options.Encrypt?.Options ?? [];

    /// <summary>
    /// This invocation as a command line for a hint: <see cref="ContinuationAt"/> of its own
    /// command, or that command alone when it has no input, then every other argument and option the
    /// caller gave, as given, except <paramref name="dropped"/> and the output format, which every
    /// such command line names itself. The input's password is passed on as
    /// <see cref="ContinuationOf"/> passes it, however the caller gave it; any other secret read from
    /// stdin or given literally is never repeated, so the result is null. A document read from stdin keeps its
    /// <c>-</c>, which <see cref="RepeatedInvocation.Repeating"/> tells the caller to pipe again, so
    /// a document that may hold secrets is never written into the hint. The caller appends what it
    /// changes. Null, too, for a command parsed on its own, outside the command tree, which has no
    /// path to repeat.
    /// </summary>
    /// <param name="dropped">The parameters the caller replaces or leaves out; a null entry is ignored.</param>
    internal RepeatedInvocation? Again(params Symbol?[] dropped)
    {
        ArgumentNullException.ThrowIfNull(dropped);
        string[] path = CommandPath();
        if (path.Length == 0)
        {
            return null;
        }

        // ContinuationAt names the input and passes its password on; a command without an input
        // passes the password of the files it reads on after them.
        ContinuationCommand again = _options.Input is null ? new ContinuationCommand(path) : ContinuationAt(path);
        Symbol[] named = [.. _options.Input is null ? [] : new Symbol[] { _options.Input }, .. _options.InputPassword?.Options ?? []];
        var replay = new CommandLineReplay(again, [.. dropped.OfType<Symbol>(), .. named]);
        if (!replay.Append(_parse.RootCommandResult) || replay.LeftOut.Count > 0)
        {
            return null;
        }

        if (_options.Input is null && _options.InputPassword?.Continued(_parse) is var (option, variable))
        {
            again.PasswordVariable(option, variable);
        }

        return new RepeatedInvocation(again, replay.Piped);
    }

    /// <summary>
    /// This invocation's command line again, without <paramref name="replaced"/>, for a hint of a
    /// command that is not a product's, such as <c>review</c>, which appends what it changes: the
    /// replay of <see cref="Again"/> itself, not a second one. Null where <see cref="Again"/> is,
    /// and for an invocation that read a document from stdin.
    /// </summary>
    /// <param name="replaced">The parameters the caller replaces or leaves out; a null entry is ignored.</param>
    public ContinuationCommand? Repeated(params Symbol?[] replaced) =>
        Again(replaced) is { Piped: null } again ? again.Command : null;

    /// <summary>
    /// The command lines, for a hint, that write a file of <paramref name="format"/> at
    /// <paramref name="output"/>, replacing it, from this command's input with the conversion at
    /// <paramref name="converter"/>: that conversion of the input itself, or, when the invocation
    /// selected pages with a range option, this product's split of those pages into a folder of its
    /// own beside the output, then that conversion of the part, so the file holds only the pages
    /// selected. Each starts from the input as <see cref="ContinuationOf"/> names it, the part as the
    /// input with another document, since the split keeps the input's password; the caller may append
    /// options to the last. Null when the invocation gave the command other arguments or options of
    /// its own, which the conversion might not take, or selected pages its product has no split by groups for.
    /// </summary>
    /// <param name="converter">The command path of the conversion.</param>
    /// <param name="format">The format id the conversion writes.</param>
    /// <param name="output">The file the conversion writes.</param>
    /// <param name="purpose">What the conversion is for, such as <c>editable</c>, which names the folder of the part.</param>
    internal InputConversion? FromInput(string[] converter, string format, string output, string purpose)
    {
        ArgumentNullException.ThrowIfNull(converter);
        ContinuationInput input = ContinuationOf(Input);
        ContinuationCommand Convert(ContinuationInput source) => source.ContinuationAt(converter)
            .Option(StandardOptionNames.To, format)
            .Out(ContinuationOutput.Written(output));
        Symbol[] given = [.. _options.Own.Where(Gave)];
        if (given.Length == 0)
        {
            return new InputConversion([Convert(input)], Part: null);
        }

        if (given is not [Option range] || !PartRangeOption.Selects(range, SplitBy.Pages)
            || _parse.GetResult(range) is not OptionResult { Tokens: [var selection] }
            || GroupedSplit() is not { } split)
        {
            return null;
        }

        // A folder named for the purpose that names nothing yet, so the split creates it and meets
        // no file of the caller's there, and the hints of one invocation split into folders apart.
        string named = Path.Combine(Path.GetDirectoryName(output)!, $"{Path.GetFileNameWithoutExtension(output)}-{purpose}-part");
        string folder = named;
        for (int number = 2; File.Exists(folder) || Directory.Exists(folder); number++)
        {
            folder = $"{named}-{number}";
        }

        string part = new ResolvedDirectory(folder, overwrite: false).Parts(Path.GetFileNameWithoutExtension(Input), split.Extension, [1])[0];
        return new InputConversion(
        [
            // The part is a new file in a new folder, written only where nothing is.
            input.ContinuationAt(split.Path)
                .Option(SplitOptions.ByOption, SplitPlan.Spell(SplitBy.Pages))
                .Option(SplitOptions.GroupsOption, selection.Value)
                .OutDir(ContinuationOutput.New(folder)),
            Convert(input with { Document = part }),
        ], part);
    }

    /// <summary>
    /// Whether the caller gave any of the command's own arguments and options, which a
    /// conversion to another format, or by another command, might not take.
    /// </summary>
    internal bool GaveOwnParameters => _options.Own.Any(Gave);

    // Whether the caller gave the command's own argument or option.
    private bool Gave(Symbol parameter) => parameter switch
    {
        Option option => _parse.GetResult(option) is OptionResult { Implicit: false },
        Argument argument => _parse.GetResult(argument) is ArgumentResult { Tokens.Count: > 0 },
        _ => false,
    };

    // The visible split beside this command that writes one part per page group (SplitOptions),
    // with the extension of its parts; null when there is none.
    private (string[] Path, string Extension)? GroupedSplit()
    {
        if (_parse.CommandResult.Parent is not CommandResult product)
        {
            return null;
        }

        Command? split = product.Command.Subcommands.FirstOrDefault(static command => !command.Hidden && SplitOptions.SplitsByGroups(command));
        return split is not null && StandardOptions.Of(split) is { OutputTarget.Kind: OutputKind.Directory } options
            ? ([.. CommandPath()[..^1], split.Name], options.OutputTarget.Writes?.FirstOrDefault()?.PreferredExtension ?? Path.GetExtension(Input))
            : null;
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
            return ChosenBy(writes, option, output) is [var first, ..] alternatives
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

    // The formats among those written that the named output's extension chooses, in declared
    // order. An extension that only formats --to alone chooses declare is refused, naming them.
    private static FormatDescriptor[] ChosenBy(IReadOnlyList<FormatDescriptor> writes, string parameter, string output)
    {
        FormatDescriptor[] declaring = Writing(writes, output);
        FormatDescriptor[] chosen = [.. declaring.Where(static format => format.ChosenByExtension)];
        if (chosen.Length > 0 || declaring.Length == 0)
        {
            return chosen;
        }

        string option = parameter.StartsWith('-') ? parameter : "the " + parameter;
        throw CliErrors.Usage(
            [$"Name the output format: the {Path.GetExtension(output)} extension of {option} '{output}' is shared by "
                + $"{Listed(declaring, conjunction: "and")}, which only {StandardOptionNames.To} chooses."],
            parameter: StandardOptionNames.To,
            hint: $"Pass {Listed(declaring, StandardOptionNames.To + " ")} with {option} '{output}'.");
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
        FormatDescriptor format,
        IReadOnlyList<FormatDescriptor> writes)
    {
        string extension = Path.GetExtension(output);
        string problem = extension.Length > 1
            ? $"its {extension} extension is not an extension of {format.Id}"
            : $"it has no extension, and an output in {format.Id} needs one";
        FormatDescriptor[] named = Writing(writes, output);
        string other = named switch
        {
            [var only] => $", or pass {StandardOptionNames.To} {only.Id} to write {only.Id}",
            [_, ..] => $", or pass {Listed(named, StandardOptionNames.To + " ")} to write one of those formats",
            _ => string.Empty,
        };
        return CliErrors.OutputExtensionInvalid(
            parameter,
            output,
            $"{StandardOptionNames.To} {format.Id} contradicts {parameter} '{output}':",
            problem,
            format.Extensions,
            mistyped: extension.Length > 1 && named.Length == 0,
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
        // A command that writes only images shows its input rather than keeping a copy of it, and
        // a conversion writes its input in another format, so another format is written from the
        // input; any other output, as an edit's, is a document to convert after.
        bool images = writes.All(static format => format.Image);
        bool fromInput = images || _options.Target is not null;
        // An image command given its input's own extension was asked for what the input already is.
        bool inputs = images && !derived && extension.Length > 1 && _options.Input is { } input
            && _parse.GetValue(input) is { } given
            && string.Equals(Path.GetExtension(given), extension, StringComparison.OrdinalIgnoreCase);
        string[] extensions = [.. writes.SelectMany(static format => format.Extensions).Distinct(StringComparer.OrdinalIgnoreCase)];
        string? producerOutput = fromInput && !derived ? output : null;
        (IReadOnlyList<FormatDescriptor> Formats, IReadOnlyList<ContinuationCommand> Commands)? producer = extension.Length > 1 && !inputs ? Producer(extension, producerOutput) : null;
        // A mistyped extension close to none the command writes may stand for one a sibling
        // writes, as .htlm stands for the .html of convert: that extension is answered as itself.
        string? meant = extension.Length > 1 && !inputs && producer is null
            && Mistake.Of(extension, extensions, keyOf: ExtensionKey).Suggestions.Count == 0
            ? Mistake.Of(extension, SiblingExtensions(), keyOf: ExtensionKey).Suggestions.FirstOrDefault()
            : null;
        if (meant is not null)
        {
            producer = Producer(meant, producerOutput is null ? null : Path.ChangeExtension(producerOutput, meant));
        }

        string problem = (extension.Length > 1, producer) switch
        {
            _ when inputs => $"has the {extension} extension of its input, which already is that file; {command} writes only images",
            (true, { } other) when meant is not null => $"has the {extension} extension, which names no format; the closest, {meant}, names {Listed(other.Formats, conjunction: "and")}, which {command} does not write",
            (true, { } other) => $"has the {extension} extension of {Listed(other.Formats, conjunction: "and")}, which {command} does not write",
            (true, null) => $"has the {extension} extension, which names no format {command} writes",
            _ => $"has no extension, which names the format {command} writes",
        };
        // An option is named as written; a positional argument by its name, as in "the file".
        bool option = parameter.StartsWith('-');
        string fix = derived
            ? $"Name the output with {parameter} and one of these extensions"
            : $"Give {(option ? parameter : "the " + parameter)} one of these extensions";
        string hint = producer switch
        {
            { } line when fromInput && !derived => $"{fix}, or run {Run(line.Commands)} for {Listed(line.Formats)}.{string.Concat(line.Commands.Select(static command => command.LeftOut))}",
            { } line => $"{fix}, then run {Run(line.Commands)} for {Listed(line.Formats)}.{string.Concat(line.Commands.Select(static command => command.LeftOut))}",
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
            producer is { Formats: [var named] } ? new JsonObject { ["format"] = named.Id } : []);
    }

    // An extension as a mistake compares it: without its dot, ignoring case.
    private static string ExtensionKey(string extension) => extension.TrimStart('.').ToLowerInvariant();

    // The visible commands beside this one, with the formats each writes through its --to.
    private IEnumerable<(string Command, TargetFormat Target)> SiblingTargets() =>
        _parse.CommandResult.Parent is not CommandResult parent
            ? []
            : parent.Command.Subcommands
                .Where(command => !command.Hidden && command != _parse.CommandResult.Command)
                .SelectMany(static command => command.Options
                    .Select(StandardOptions.TargetOf)
                    .OfType<TargetFormat>()
                    .Select(target => (command.Name, target)));

    // The extensions of the formats the commands beside this one write, in declared order.
    private string[] SiblingExtensions() =>
        [.. SiblingTargets()
            .SelectMany(static sibling => sibling.Target.Offered)
            .SelectMany(static format => format.Extensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The format an extension chooses among those the commands beside this one write through
    /// their <c>--to</c>, or every format it names when only <c>--to</c> chooses them, as
    /// an archive extension may name several archival profiles, and the command line that writes each: from this
    /// command's output, or, when <paramref name="output"/> is given, from this command's input
    /// straight to that output, as <see cref="ContinuationAt"/> continues this invocation. The
    /// sibling writes it, unless the format names the product that
    /// writes an editable file of it (<see cref="FormatDescriptor.EditableBy"/>) and that product
    /// has a command of the sibling's name that writes the format: then that command does.
    /// </summary>
    /// <param name="extension">The extension the output was given.</param>
    /// <param name="output">The output as the caller gave it, for a command whose output is not a document to convert; otherwise null.</param>
    private (IReadOnlyList<FormatDescriptor> Formats, IReadOnlyList<ContinuationCommand> Commands)? Producer(string extension, string? output)
    {
        string[] path = CommandPath();
        // From this command's input, the command continues this invocation (ContinuationAt), so
        // it opens the input with the same password; otherwise the caller names the file.
        ContinuationCommand Line(string[] writer, FormatDescriptor format)
        {
            ContinuationCommand line = (output is not null && _options.Input is not null
                ? ContinuationAt(writer).Carrying(_parse, writer.Aggregate(_parse.RootCommandResult.Command, static (command, name) => Visible(command, name)!), format.Id,
                    StandardOptionNames.To, StandardOptionNames.Out, StandardOptionNames.Overwrite)
                : new ContinuationCommand(writer).Placeholder("that file")).Option(StandardOptionNames.To, format.Id);
            return output is null ? line : line.Out(ContinuationOutput.Requested(output, Overwrite));
        }

        return SiblingTargets()
            .Select(sibling => Named(sibling.Target.Offered.DeclaringExtension(extension)) is { Length: > 0 } formats
                ? ((IReadOnlyList<FormatDescriptor> Formats, IReadOnlyList<ContinuationCommand> Commands)?)(formats,
                    [.. formats.Select(format => Line(Writer(path[..^1], sibling.Command, format), format))])
                : null)
            .FirstOrDefault(static line => line is not null);
    }

    /// <summary>
    /// The command lines, quoted for a hint and run one after the other
    /// (<see cref="ContinuationCommand.Steps"/>), that write an editable file in place of the one
    /// this command writes (<see cref="Output"/>), from this command's input: this command's name
    /// under the product the output's format names as writing one (<see cref="FormatDescriptor.EditableBy"/>),
    /// with the input and its password's environment variable as <see cref="Continuation()"/>
    /// repeats them, then <c>--to</c> the format, <c>--out</c> this command's output and
    /// <c>--overwrite</c>, so it replaces that file. A selection of pages is split from the input
    /// first and the part converted, so the file keeps only those pages, and the folder of the part
    /// may be deleted once converted. The text ends with <c>instead</c> and what to delete, as in
    /// <c>run 'a', then 'b' instead; …</c>. Null when the format names no such product, this build
    /// has no such command, or the command was given other options of its own.
    /// </summary>
    public string? EditableConversion()
    {
        ResolvedOutput output = Output;
        return Editor(output.Format, CommandPath()[^1]) is { } path && FromInput(path, output.Format.Id, output.Path, "editable") is { } conversion
            ? $"{ContinuationCommand.Steps(conversion.Commands)} instead{conversion.Leftover}"
            : null;
    }

    // The command path that writes a format a sibling of this command writes: the editor's (Editor)
    // when there is one, otherwise the sibling's own.
    private string[] Writer(string[] product, string sibling, FormatDescriptor format) =>
        Editor(format, sibling) ?? [.. product, sibling];

    // The path of the visible command of a name under the product the format names as writing an
    // editable file of it, when that command's --to offers the format; otherwise null.
    private string[]? Editor(FormatDescriptor format, string command)
    {
        Command? editor = format.EditableBy is { } id
            ? Visible(_parse.RootCommandResult.Command, id) is { } product ? Visible(product, command) : null
            : null;
        return editor is not null && editor.Options
            .Select(StandardOptions.TargetOf)
            .OfType<TargetFormat>()
            .Any(target => target.Offered.Any(offered => string.Equals(offered.Id, format.Id, StringComparison.OrdinalIgnoreCase)))
                ? [format.EditableBy!, command]
                : null;
    }

    // The visible subcommand of a command by its name, or null.
    private static Command? Visible(Command command, string name) =>
        command.Subcommands.FirstOrDefault(subcommand => !subcommand.Hidden && string.Equals(subcommand.Name, name, StringComparison.Ordinal));

    // The first format an extension chooses among those that declare it, or all of them when
    // only --to chooses them.
    private static FormatDescriptor[] Named(IReadOnlyList<FormatDescriptor> declaring) =>
        declaring.FirstOrDefault(static format => format.ChosenByExtension) is { } chosen ? [chosen] : [.. declaring];

    // Command lines as a hint quotes them, one or several to choose from.
    private static string Run(IReadOnlyList<ContinuationCommand> commands) => Listed([.. commands.Select(static line => line.Quoted())]);
}

/// <summary>
/// An invocation as a command line for a hint (<see cref="StandardInvocation.Again"/>), with the
/// document it read from stdin, which it could not write into it.
/// </summary>
/// <param name="Command">The command line; the caller appends what it changes.</param>
/// <param name="Piped">The option of a document the invocation read from stdin, which keeps its <c>-</c>; null for none.</param>
internal sealed record RepeatedInvocation(ContinuationCommand Command, string? Piped)
{
    /// <summary>
    /// What a hint says right after the command of what it cannot repeat: the document to pipe
    /// to it again on stdin; empty when it repeats everything.
    /// </summary>
    public string Repeating =>
        Piped is null ? string.Empty : $", piping the same {Piped} document to it on stdin as this command read it";
}

/// <summary>
/// The command lines that convert an invocation's input, or the part split from it
/// (<see cref="StandardInvocation.FromInput"/>), run one after the other.
/// </summary>
/// <param name="Commands">The command lines; the caller may append options to the last.</param>
/// <param name="Part">The part the split writes for the conversion to read, in a folder the split creates, or null when the input itself is converted.</param>
internal sealed record InputConversion(IReadOnlyList<ContinuationCommand> Commands, string? Part)
{
    /// <summary>
    /// What a hint says after the commands of the folder they leave behind, which holds only the
    /// part the conversion reads: to delete it, by its full path quoted as the commands quote it, a
    /// folder that named nothing when the hint was made, so it never holds a file of the caller's;
    /// empty without one.
    /// </summary>
    public string Leftover => Part is null
        ? string.Empty
        : $"; once the conversion has run, delete {CommandLineText.Quote(Path.GetDirectoryName(Part)!)}, the folder the split created beside the output for the part";
}
