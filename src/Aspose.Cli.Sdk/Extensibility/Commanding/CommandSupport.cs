using System.CommandLine;
using System.Globalization;
using System.Runtime.CompilerServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Reusable validation for numeric command options. A bounded integer option declares its bounds
/// once with <see cref="InRange{T}(Option{T}, long, long)"/>, which states them in its help in
/// the one wording <see cref="RangeText"/> gives, and is read with <c>GetInRange</c>, which
/// enforces them.
/// </summary>
public static class OptionGuards
{
    private static readonly ConditionalWeakTable<Option, Bounds> Declared = new();

    /// <summary>The one wording of an option's bounds in its description: <c>Range: MIN-MAX.</c></summary>
    public static string RangeText(long minimum, long maximum) =>
        string.Create(CultureInfo.InvariantCulture, $"Range: {minimum}-{maximum}.");

    /// <summary>
    /// Declares the inclusive bounds of an integer option: its description ends with
    /// <see cref="RangeText"/>, and <c>GetInRange</c> rejects a value outside them.
    /// </summary>
    /// <returns>The same option.</returns>
    public static Option<T> InRange<T>(this Option<T> option, long minimum, long maximum)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        string range = RangeText(minimum, maximum);
        option.Description = string.IsNullOrEmpty(option.Description) ? range : $"{option.Description} {range}";
        Declared.AddOrUpdate(option, new Bounds(minimum, maximum));
        return option;
    }

    /// <summary>Reads an option declared with <see cref="InRange{T}"/>.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> outside the declared bounds.</exception>
    public static int GetInRange(this ParseResult parse, Option<int> option, string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int value = parse.GetValue(option);
        Ensure(option, value, hint);
        return value;
    }

    /// <summary>Reads an optional option declared with <see cref="InRange{T}"/>; an absent value is null.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> outside the declared bounds.</exception>
    public static int? GetInRange(this ParseResult parse, Option<int?> option, string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int? value = parse.GetValue(option);
        if (value is { } present)
        {
            Ensure(option, present, hint);
        }

        return value;
    }

    /// <summary>Reads an optional option declared with <see cref="InRange{T}"/>; an absent value is null.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> outside the declared bounds.</exception>
    public static long? GetInRange(this ParseResult parse, Option<long?> option, string? hint = null)
    {
        ArgumentNullException.ThrowIfNull(parse);
        long? value = parse.GetValue(option);
        if (value is { } present)
        {
            Ensure(option, present, hint);
        }

        return value;
    }

    /// <summary>Rejects an integer outside the inclusive range.</summary>
    public static void EnsureInRange(
        string option,
        int value,
        int min,
        int max,
        string hint)
    {
        if (value < min || value > max)
        {
            throw CliErrors.OptionInvalid(
                option,
                $"value must be between {min} and {max}",
                hint);
        }
    }

    private static void Ensure(Option option, long value, string? hint)
    {
        if (!Declared.TryGetValue(option, out Bounds? bounds))
        {
            throw new InvalidOperationException($"Option {option.Name} declares no range; declare it with InRange.");
        }

        if (value < bounds.Minimum || value > bounds.Maximum)
        {
            throw CliErrors.OptionInvalid(
                option.Name,
                string.Create(CultureInfo.InvariantCulture, $"value must be between {bounds.Minimum} and {bounds.Maximum}"),
                hint ?? string.Create(CultureInfo.InvariantCulture, $"Pass a value from {bounds.Minimum} to {bounds.Maximum}."));
        }
    }

    private sealed record Bounds(long Minimum, long Maximum);
}

/// <summary>The transport selected by a JSON source value.</summary>
public enum JsonSourceKind { File, Inline, StandardInput }

/// <summary>Reads JSON from inline text, a file, or standard input.</summary>
public static class JsonInputSource
{
    /// <summary>Reads and resolves one document source.</summary>
    internal static string Read(
        string source,
        PathResolver paths,
        InputSource inputs,
        string option)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(inputs);
        JsonSourceKind kind = Classify(source, option);
        if (kind == JsonSourceKind.StandardInput)
        {
            return inputs.ReadStandardInputText(Console.OpenStandardInput());
        }
        if (kind == JsonSourceKind.Inline)
        {
            return inputs.ReadInlineText(source, "inline-text");
        }

        try
        {
            return inputs.ReadTextFile(
                paths.ResolveInput(source));
        }
        catch (CliException exception)
            when (exception.Code == ErrorCodes.FileNotFound)
        {
            throw CliException.Create(
                exception.Code,
                exception.Message,
                hint: $"{exception.Hint} To pass the JSON itself, start the value with {{ or [ (inline) or use '-' for stdin.",
                details: exception.Details,
                docs: exception.Docs);
        }
    }

    /// <summary>Classifies JSON input using the same rules as the actual reader.</summary>
    public static JsonSourceKind Classify(string source, string option)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw CliErrors.OptionInvalid(option, "the value is empty",
                "Pass a path to the document, inline JSON starting with { or [, or '-' for stdin.");
        }
        if (source == "-") { return JsonSourceKind.StandardInput; }
        string trimmed = source.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[')
            ? JsonSourceKind.Inline : JsonSourceKind.File;
    }

}

/// <summary>
/// The one rule for standard input: an invocation reads it for at most one value, so no option
/// reads what another option's value was, and no error repeats it. The options that read it
/// are the <c>--*-stdin</c> flags and the JSON sources given as <c>-</c>, such as <c>--ops -</c>.
/// </summary>
internal static class StandardInputSources
{
    /// <summary>Refuses an invocation in which two options read standard input, before either reads it.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c>, naming the second option.</exception>
    public static void EnsureOne(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        Option[] readers = [.. parse.CommandResult.Command.Options.Where(option => Reads(parse, option))];
        if (readers.Length > 1)
        {
            throw CliErrors.OptionInvalid(
                readers[1].Name,
                $"stdin can hold one value, and {Spelled(readers[0])} reads it",
                "Pass one value on stdin and the other through a file or its -env option.");
        }
    }

    private static bool Reads(ParseResult parse, Option option)
    {
        ParameterMetadata metadata = option.GetParameterMetadata();
        return metadata.ValueSource == ParameterValueSource.StandardInput
            ? option is Option<bool> flag && parse.GetValue(flag)
            : metadata.InputKind == InputKind.JsonSource && option is Option<string> source && parse.GetValue(source) == "-";
    }

    private static string Spelled(Option option) =>
        option.GetParameterMetadata().InputKind == InputKind.JsonSource ? $"{option.Name} -" : option.Name;
}

/// <summary>
/// The standard <c>--out</c> option of commands that publish one file, and the one rule for
/// every output file a caller names: it resolves against the working directory and never
/// names an input.
/// </summary>
internal sealed class OutputFileOption
{
    private const string OutOption = StandardOptionNames.Out;

    /// <summary>Creates the option with command-specific help.</summary>
    public OutputFileOption(string description, bool required)
    {
        Option = new Option<string?>(OutOption, StandardOptionNames.OutAlias)
        {
            Description = description,
            Required = required,
        }.WithInput(InputKind.None);
    }

    /// <summary>The <c>--out</c> option.</summary>
    public Option<string?> Option { get; }

    /// <summary>Whether the command cannot run without <c>--out</c>.</summary>
    public bool Required => Option.Required;

    /// <summary>Adds the option to a command.</summary>
    public void AddTo(Command command) => command.Options.Add(Option);

    /// <summary>Resolves <c>--out</c>, which must name none of the inputs, or returns null when it was omitted.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the output resolves to an input.</exception>
    public string? Resolve(
        ParseResult parseResult,
        PathResolver paths,
        params IReadOnlyList<string?> inputPaths) =>
        parseResult.GetValue(Option) is { } explicitOut
            ? ResolveExplicit(paths, explicitOut, OutOption, inputPaths)
            : null;

    /// <summary>Resolves a required <c>--out</c>, which must name none of the inputs.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when it is missing or resolves to an input.</exception>
    public string ResolveRequired(
        ParseResult parseResult,
        PathResolver paths,
        params IReadOnlyList<string?> inputPaths) =>
        Resolve(parseResult, paths, inputPaths)
            ?? throw CliErrors.OptionInvalid(OutOption, "is required", "Pass the output file path.");

    /// <summary>
    /// Resolves an output file the caller names, such as a create command's file argument,
    /// and rejects one that names any of the command's inputs: replacing an input is the
    /// in-place mode's job alone, with its backup and fingerprint precondition.
    /// </summary>
    /// <param name="paths">The invocation path resolver.</param>
    /// <param name="output">The output path as given.</param>
    /// <param name="parameter">The option or argument that names the output, for the error.</param>
    /// <param name="inputPaths">The command's resolved input files; an absent optional input is null.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the output resolves to an input.</exception>
    public static string ResolveExplicit(
        PathResolver paths,
        string output,
        string parameter,
        params IReadOnlyList<string?> inputPaths) =>
        ResolveExplicit(paths, output, parameter, inPlaceAvailable: false, inputPaths);

    internal static string ResolveExplicit(
        PathResolver paths,
        string output,
        string parameter,
        bool inPlaceAvailable,
        IReadOnlyList<string?> inputPaths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        RefuseStandardStream(output, parameter);
        string resolved = paths.ResolveOutput(output);
        EnsureNotInput(resolved, parameter, inPlaceAvailable, inputPaths);
        return resolved;
    }

    /// <summary>
    /// Refuses an output named <c>-</c>: <c>-</c> names standard input only, and every output
    /// is a file or a directory, never standard output.
    /// </summary>
    /// <param name="output">The output as given.</param>
    /// <param name="parameter">The option or argument that names it.</param>
    /// <param name="directory">Whether the output is a directory.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for <c>-</c>.</exception>
    internal static void RefuseStandardStream(string output, string parameter, bool directory = false)
    {
        if (output == "-")
        {
            throw CliErrors.OutputIsStandardStream(parameter, directory);
        }
    }

    /// <summary>Rejects a resolved output, named by the caller or derived, that is one of the inputs.</summary>
    /// <param name="output">The resolved output path.</param>
    /// <param name="parameter">The option or argument that names the output, or would name it, for the error.</param>
    /// <param name="inPlaceAvailable">Whether the command can replace its input in place instead.</param>
    /// <param name="inputPaths">The resolved input files; an absent optional input is null.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the output resolves to an input.</exception>
    internal static void EnsureNotInput(
        string output,
        string parameter,
        bool inPlaceAvailable,
        IReadOnlyList<string?> inputPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentNullException.ThrowIfNull(inputPaths);
        if (inputPaths.Any(input => input is not null && OutputPathValidator.IsSameFile(output, input)))
        {
            throw CliErrors.OutputIsInput(parameter, output, inPlaceAvailable);
        }
    }

    internal static string DerivePath(
        string inputPath,
        string targetExtension)
    {
        string candidate = Path.ChangeExtension(inputPath, targetExtension);
        if (!FilePaths.Same(candidate, inputPath))
        {
            return candidate;
        }

        string directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, stem + ".out" + targetExtension);
    }
}

/// <summary>
/// The one reader of a password the caller passes on the command line: mutually exclusive
/// literal, environment and stdin options, or a required password given by exactly one of an
/// environment and a stdin option. It resolves the password to a <see cref="Secret"/> and never
/// serializes it.
/// </summary>
public sealed class PasswordOptions
{
    private readonly string _prefix;
    private readonly Option<string?>? _literal;
    private readonly Option<string?> _fromEnvironment;
    private readonly Option<bool> _fromStandardInput;
    private readonly bool _required;

    /// <summary>Creates a password source for one protected input or output.</summary>
    internal PasswordOptions(string prefix, string subject)
        : this(prefix, subject, Sources.All)
    {
    }

    private PasswordOptions(string prefix, string subject, Sources sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        _prefix = prefix;
        _required = sources == Sources.Required;
        _literal = _required
            ? null
            : new Option<string?>(prefix)
            {
                Description =
                    $"Password for {subject}. Discouraged: visible in the process list; prefer {prefix}-env.",
            }.WithInput(InputKind.None, secret: true);
        _fromEnvironment = new Option<string?>(prefix + StandardOptionNames.EnvironmentSuffix)
        {
            Description =
                $"Name of an environment variable holding the password for {subject}.",
        }.WithInput(InputKind.None, ParameterValueSource.EnvironmentVariableName, secret: true);
        _fromStandardInput = new Option<bool>(prefix + StandardOptionNames.StandardInputSuffix)
            {
                Description =
                    $"Read the password for {subject} from the first line of stdin.",
            }.WithInput(InputKind.None, ParameterValueSource.StandardInput, secret: true);
    }

    private enum Sources { All, Required }

    /// <summary>
    /// A password the caller must give, such as a signing certificate's, by exactly one of
    /// <c><paramref name="prefix"/>-env</c> and <c><paramref name="prefix"/>-stdin</c>; neither
    /// option is required alone, and there is no literal form. Declare <see cref="Options"/>
    /// among the command's parameters and read the password with
    /// <see cref="StandardInvocation.Password"/>.
    /// </summary>
    /// <param name="prefix">The option prefix, such as <c>--certificate-password</c>.</param>
    /// <param name="subject">What the password protects, as its help names it.</param>
    public static PasswordOptions Required(string prefix, string subject) =>
        new(prefix, subject, Sources.Required);

    /// <summary>The error of a required password that no option gave.</summary>
    internal CliException Missing() => CliErrors.OptionInvalid(
        $"{_prefix}-env",
        "the password is required",
        $"Name the environment variable that holds it with {_prefix}-env, or pass it on stdin with {_prefix}-stdin.");

    /// <summary>The options that supply the password, in help order.</summary>
    public IReadOnlyList<Option> Options =>
    [
        .. _literal is null ? [] : new Option[] { _literal },
        _fromEnvironment,
        _fromStandardInput,
    ];

    /// <summary>Adds the supported secret sources to a command.</summary>
    internal void AddTo(Command command)
    {
        foreach (Option option in Options)
        {
            command.Options.Add(option);
        }
    }

    /// <summary>The option that names the password's environment variable.</summary>
    internal string EnvironmentOption => _prefix + StandardOptionNames.EnvironmentSuffix;

    /// <summary>The environment variable the caller named for the password, or null.</summary>
    internal string? EnvironmentName(ParseResult parseResult) => parseResult.GetValue(_fromEnvironment);

    /// <summary>
    /// Returns the option that supplies the secret, or null when none was given, so an error
    /// about the secret's use names the option the caller actually passed.
    /// </summary>
    internal string? SelectedOption(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        return _literal is not null && parseResult.GetValue(_literal) is not null ? _prefix
            : parseResult.GetValue(_fromEnvironment) is not null ? $"{_prefix}-env"
            : parseResult.GetValue(_fromStandardInput) ? $"{_prefix}-stdin"
            : null;
    }

    /// <summary>
    /// Resolves the selected secret without serializing it. Standard input holds only the
    /// password when its stdin option is given (<see cref="StandardInputSources"/>).
    /// </summary>
    internal Secret? Resolve(
        ParseResult parseResult,
        InputSource inputs,
        Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(readEnvironment);
        string? literal = _literal is null ? null : parseResult.GetValue(_literal);
        string? environmentName = parseResult.GetValue(_fromEnvironment);
        bool fromStandardInput = parseResult.GetValue(_fromStandardInput);
        int sourceCount = (literal is null ? 0 : 1)
            + (environmentName is null ? 0 : 1)
            + (fromStandardInput ? 1 : 0);
        if (sourceCount > 1)
        {
            throw CliErrors.OptionInvalid(
                _prefix,
                "more than one password source was given",
                _required
                    ? $"Use only one of {_prefix}-env or {_prefix}-stdin."
                    : $"Use only one of {_prefix}, {_prefix}-env or {_prefix}-stdin.");
        }

        if (literal is not null)
        {
            return literal.Length > 0
                ? new Secret(literal)
                : throw CliErrors.OptionInvalid(
                    _prefix,
                    "the password is empty",
                    $"Pass a non-empty password, or drop {_prefix} entirely.");
        }

        if (environmentName is not null)
        {
            string? value = readEnvironment(environmentName);
            return !string.IsNullOrEmpty(value)
                ? new Secret(value)
                : throw CliErrors.SecretMissing($"{_prefix}-env", environmentName, $"{_prefix}-stdin");
        }

        if (!fromStandardInput)
        {
            return null;
        }

        string line = inputs.ReadSecretLine(Console.In);
        return !string.IsNullOrEmpty(line)
            ? new Secret(line)
            : throw CliErrors.OptionInvalid(
                $"{_prefix}-stdin",
                "no password was provided on stdin",
                "Provide the password as the first line of standard input.");
    }
}
