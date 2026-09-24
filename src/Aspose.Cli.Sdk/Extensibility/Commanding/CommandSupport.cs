using System.CommandLine;
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

/// <summary>Factories for common output options.</summary>
internal static class OutputOptions
{
    /// <summary>Creates the standard non-destructive overwrite switch.</summary>
    public static Option<bool> Overwrite() => new(StandardOptionNames.Overwrite)
    {
        Description = "Replace the output file if it already exists.",
    };
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
            throw new CliException(
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
/// The standard output path and overwrite option pair, and the one rule for every output
/// file a caller names: it resolves against the working directory and never names an input.
/// </summary>
internal sealed class OutputFileOptions
{
    private const string OutOption = StandardOptionNames.Out;
    private readonly Option<string?> _out;
    private readonly Option<bool> _overwrite;

    /// <summary>Creates an output option pair with product-specific help.</summary>
    public OutputFileOptions(string description, bool required = false)
    {
        _out = new Option<string?>(OutOption, StandardOptionNames.OutAlias)
        {
            Description = description,
            Required = required,
        }.WithInput(InputKind.None);
        _overwrite = OutputOptions.Overwrite();
    }

    /// <summary>Whether the command cannot run without <c>--out</c>.</summary>
    public bool Required => _out.Required;

    /// <summary>Adds both options to a command.</summary>
    public void AddTo(Command command)
    {
        command.Options.Add(_out);
        command.Options.Add(_overwrite);
    }

    /// <summary>Returns whether replacement was explicitly allowed.</summary>
    public bool Overwrite(ParseResult parseResult) =>
        parseResult.GetValue(_overwrite);

    /// <summary>Returns an explicitly requested output extension, if any.</summary>
    public string? RequestedExtension(ParseResult parseResult) =>
        parseResult.GetValue(_out) is { } path
            && Path.GetExtension(path) is { Length: > 1 } extension
                ? extension
                : null;

    /// <summary>Resolves <c>--out</c>, which must name none of the inputs, or returns null when it was omitted.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> when the output resolves to an input.</exception>
    public string? Resolve(
        ParseResult parseResult,
        PathResolver paths,
        params IReadOnlyList<string?> inputPaths) =>
        parseResult.GetValue(_out) is { } explicitOut
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
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        ArgumentNullException.ThrowIfNull(inputPaths);
        string resolved = paths.ResolveOutput(output);
        return inputPaths.Any(input => input is not null && OutputPathValidator.IsSameFile(resolved, input))
            ? throw CliErrors.OutputIsInput(parameter, resolved, inPlaceAvailable)
            : resolved;
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

/// <summary>The resolved target of an atomic mutation.</summary>
public sealed record MutationTarget(
    string OutputPath,
    bool Overwrite,
    bool InPlace,
    string? BackupPath);

/// <summary>Standard output, in-place, overwrite, and backup mutation options.</summary>
internal sealed class MutationFileOptions
{
    private readonly Option<string?> _out;
    private readonly Option<bool> _inPlace;
    private readonly Option<bool> _overwrite;
    private readonly Option<bool> _backup;

    /// <summary>Creates the standard atomic mutation option set.</summary>
    public MutationFileOptions(
        string outputDescription =
            "Output path. Default: the input path with '.out' inserted before the extension.",
        string inPlaceDescription = "Modify the input file itself atomically.",
        string backupDescription =
            "Create a non-overwriting backup before an in-place write.")
    {
        _out = new Option<string?>(StandardOptionNames.Out, StandardOptionNames.OutAlias)
        {
            Description = outputDescription,
        }.WithInput(InputKind.None);
        _inPlace = new Option<bool>(StandardOptionNames.InPlace)
        {
            Description = inPlaceDescription,
        };
        _overwrite = OutputOptions.Overwrite();
        _backup = new Option<bool>(StandardOptionNames.Backup)
        {
            Description = backupDescription,
        };
        Options = [_out, _inPlace, _overwrite, _backup];
    }

    /// <summary>The mutation options in help order.</summary>
    internal IReadOnlyList<Option> Options { get; }

    /// <summary>
    /// Resolves and validates the mutation destination. A backup is made only when
    /// <c>--backup</c> is given.
    /// </summary>
    public MutationTarget Resolve(
        ParseResult parseResult,
        PathResolver paths,
        string inputPath)
    {
        string? explicitOut = parseResult.GetValue(_out);
        bool inPlace = parseResult.GetValue(_inPlace);
        bool overwrite = parseResult.GetValue(_overwrite);
        bool backup = parseResult.GetValue(_backup);

        if (explicitOut is not null && inPlace)
        {
            throw CliErrors.OptionInvalid(
                "--in-place",
                "cannot be combined with --out",
                "Choose --out or --in-place.");
        }

        if (backup && !inPlace)
        {
            throw CliErrors.OptionInvalid(
                "--backup",
                "a backup is only meaningful with --in-place",
                "Pass --in-place or omit --backup.");
        }

        if (inPlace)
        {
            return new MutationTarget(
                inputPath,
                true,
                true,
                backup ? DeriveBackupPath(inputPath) : null);
        }

        string output = explicitOut is not null
            ? OutputFileOptions.ResolveExplicit(paths, explicitOut, "--out", inPlaceAvailable: true, [inputPath])
            : OutputFileOptions.DerivePath(
                inputPath,
                Path.GetExtension(inputPath));
        return new MutationTarget(output, overwrite, false, null);
    }

    private static string DeriveBackupPath(string inputPath)
    {
        string? directory = Path.GetDirectoryName(inputPath);
        string extension = Path.GetExtension(inputPath);
        string stem = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory ?? string.Empty, $"{stem}.backup{extension}");
    }
}

/// <summary>Mutually exclusive literal, environment, and stdin password options.</summary>
public sealed class PasswordOptions
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

    /// <summary>
    /// Rejects a password given for an output format that cannot carry one, naming the option
    /// the caller actually passed. Nothing is checked when no password was given.
    /// </summary>
    /// <param name="parseResult">The parsed command line.</param>
    /// <param name="format">The output format id.</param>
    /// <param name="protectableFormats">The output format ids that can carry a password.</param>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a format outside <paramref name="protectableFormats"/>.</exception>
    internal void EnsureProtectable(ParseResult parseResult, string format, IReadOnlyList<string> protectableFormats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentNullException.ThrowIfNull(protectableFormats);
        if (SelectedOption(parseResult) is { } option
            && !protectableFormats.Contains(format, StringComparer.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                option,
                $"the '{format}' format cannot be password-protected",
                $"Protect only {string.Join(", ", protectableFormats)} outputs, or drop {option}.");
        }
    }

    /// <summary>Resolves the selected secret without serializing it.</summary>
    public string? Resolve(
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
                ? literal
                : throw CliErrors.OptionInvalid(
                    _prefix,
                    "the password is empty",
                    $"Pass a non-empty password, or drop {_prefix} entirely.");
        }

        if (environmentName is not null)
        {
            string? value = readEnvironment(environmentName);
            return !string.IsNullOrEmpty(value)
                ? value
                : throw CliErrors.OptionInvalid(
                    $"{_prefix}-env",
                    $"environment variable '{environmentName}' is not set or is empty",
                    "Set the variable to the password before running, or choose another source.");
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
            ? line
            : throw CliErrors.OptionInvalid(
                $"{_prefix}-stdin",
                "no password was provided on stdin",
                "Provide the password as the first line of standard input.");
    }
}
