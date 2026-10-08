using System.CommandLine;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>Reusable validation for numeric product-command options.</summary>
public static class OptionGuards
{
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

    /// <summary>Rejects a 64-bit integer outside the inclusive range.</summary>
    public static void EnsureInRange(
        string option,
        long value,
        long min,
        long max,
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

    /// <summary>Returns an explicitly requested output extension, if any.</summary>
    public string? RequestedExtension(ParseResult parseResult) =>
        parseResult.GetValue(Option) is { } path
            && Path.GetExtension(path) is { Length: > 1 } extension
                ? extension
                : null;

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
        string resolved = paths.ResolveOutput(output);
        EnsureNotInput(resolved, parameter, inPlaceAvailable, inputPaths);
        return resolved;
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
        if (!string.Equals(candidate, inputPath, StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        string directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, stem + ".out" + targetExtension);
    }
}

/// <summary>Mutually exclusive literal, environment, and stdin password options.</summary>
internal sealed class PasswordOptions
{
    private readonly string _prefix;
    private readonly Option<string?> _literal;
    private readonly Option<string?> _fromEnvironment;
    private readonly Option<bool>? _fromStandardInput;

    /// <summary>Creates a password source for one protected input or output.</summary>
    public PasswordOptions(
        string prefix,
        string subject,
        bool allowStdin = true)
    {
        _prefix = prefix;
        _literal = new Option<string?>(prefix)
        {
            Description =
                $"Password for {subject}. Discouraged: visible in the process list; prefer {prefix}-env.",
        }.WithInput(InputKind.None, secret: true);
        _fromEnvironment = new Option<string?>(prefix + StandardOptionNames.EnvironmentSuffix)
        {
            Description =
                $"Name of an environment variable holding the password for {subject}.",
        }.WithInput(InputKind.None, ParameterValueSource.EnvironmentVariableName, secret: true);
        _fromStandardInput = allowStdin
            ? new Option<bool>(prefix + StandardOptionNames.StandardInputSuffix)
            {
                Description =
                    $"Read the password for {subject} from the first line of stdin.",
            }.WithInput(InputKind.None, ParameterValueSource.StandardInput, secret: true)
            : null;
    }

    /// <summary>Adds the supported secret sources to a command.</summary>
    public void AddTo(Command command)
    {
        command.Options.Add(_literal);
        command.Options.Add(_fromEnvironment);
        if (_fromStandardInput is not null)
        {
            command.Options.Add(_fromStandardInput);
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
        return parseResult.GetValue(_literal) is not null ? _prefix
            : parseResult.GetValue(_fromEnvironment) is not null ? $"{_prefix}-env"
            : _fromStandardInput is not null && parseResult.GetValue(_fromStandardInput) ? $"{_prefix}-stdin"
            : null;
    }

    /// <summary>Resolves the selected secret without serializing it.</summary>
    public Secret? Resolve(
        ParseResult parseResult,
        InputSource inputs,
        Func<string, string?> readEnvironment,
        bool stdinAvailable = true)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(readEnvironment);
        string? literal = parseResult.GetValue(_literal);
        string? environmentName = parseResult.GetValue(_fromEnvironment);
        bool fromStandardInput = _fromStandardInput is not null
            && parseResult.GetValue(_fromStandardInput);
        int sourceCount = (literal is null ? 0 : 1)
            + (environmentName is null ? 0 : 1)
            + (fromStandardInput ? 1 : 0);
        if (sourceCount > 1)
        {
            throw CliErrors.OptionInvalid(
                _prefix,
                "more than one password source was given",
                $"Use only one of {_prefix}, {_prefix}-env or {_prefix}-stdin.");
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
                : throw CliErrors.SecretMissing($"{_prefix}-env", environmentName);
        }

        if (!fromStandardInput)
        {
            return null;
        }

        if (!stdinAvailable)
        {
            throw CliErrors.OptionInvalid(
                $"{_prefix}-stdin",
                "stdin is already being read for the document",
                $"Pass the password via {_prefix}-env instead when the document comes from stdin.");
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
