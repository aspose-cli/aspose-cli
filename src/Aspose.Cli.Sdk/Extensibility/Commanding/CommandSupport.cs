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
public static class OutputOptions
{
    /// <summary>Creates the standard non-destructive overwrite switch.</summary>
    public static Option<bool> Overwrite() => new("--overwrite")
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
    public static string Read(
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

/// <summary>The standard output path and overwrite option pair.</summary>
public sealed class OutputFileOptions
{
    private readonly Option<string?> _out;
    private readonly Option<bool> _overwrite;

    /// <summary>Creates an output option pair with product-specific help.</summary>
    public OutputFileOptions(string description)
    {
        _out = new Option<string?>("--out", "-o")
        {
            Description = description,
        }.WithInput(InputKind.None);
        _overwrite = OutputOptions.Overwrite();
    }

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

    /// <summary>
    /// Rejects an explicit output extension that names a different format.
    /// This prevents successfully writing content whose filename advertises
    /// an incompatible decoder.
    /// </summary>
    public void EnsureExtension(
        ParseResult parseResult,
        IEnumerable<FormatDescriptor> formats,
        string formatId)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentException.ThrowIfNullOrWhiteSpace(formatId);
        string? requested = RequestedExtension(parseResult);
        if (requested is null)
        {
            return;
        }

        FormatDescriptor format = formats.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, formatId, StringComparison.Ordinal))
            ?? throw new ArgumentException(
                $"Format '{formatId}' is not declared.",
                nameof(formatId));
        IReadOnlyList<string> extensions = format.Extensions
            .Concat(format.OutputExtension is null
                ? []
                : [format.OutputExtension])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (extensions.Contains(requested, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw CliErrors.OptionInvalid(
            "--out",
            $"extension '{requested}' does not match format '{formatId}'",
            $"Use one of: {string.Join(", ", extensions)}.");
    }

    /// <summary>Resolves an explicit path or derives a sibling output path.</summary>
    public string ResolvePath(
        ParseResult parseResult,
        PathResolver paths,
        string inputPath,
        string targetExtension) =>
        parseResult.GetValue(_out) is { } explicitOut
            ? paths.ResolveOutput(explicitOut)
            : DerivePath(inputPath, targetExtension);

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
public sealed class MutationFileOptions
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
        _out = new Option<string?>("--out", "-o")
        {
            Description = outputDescription,
        }.WithInput(InputKind.None);
        _inPlace = new Option<bool>("--in-place")
        {
            Description = inPlaceDescription,
        };
        _overwrite = OutputOptions.Overwrite();
        _backup = new Option<bool>("--backup")
        {
            Description = backupDescription,
        };
    }

    /// <summary>Adds all mutation options to a command.</summary>
    public void AddTo(Command command)
    {
        command.Options.Add(_out);
        command.Options.Add(_inPlace);
        command.Options.Add(_overwrite);
        command.Options.Add(_backup);
    }

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
            ? paths.ResolveOutput(explicitOut)
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
        _fromEnvironment = new Option<string?>($"{prefix}-env")
        {
            Description =
                $"Name of an environment variable holding the password for {subject}.",
        }.WithInput(InputKind.None, ParameterValueSource.EnvironmentVariableName, secret: true);
        _fromStandardInput = allowStdin
            ? new Option<bool>($"{prefix}-stdin")
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

    /// <summary>Resolves the selected secret without serializing it.</summary>
    public string? Resolve(
        ParseResult parseResult,
        InputSource inputs,
        Func<string, string?> readEnvironment,
        bool stdinAvailable = true) =>
        Resolve(
            parseResult,
            inputs,
            stdinAvailable,
            readEnvironment,
            Console.In);

    /// <summary>Injected resolution core used by host and product tests.</summary>
    public string? Resolve(
        ParseResult parseResult,
        InputSource inputs,
        bool stdinAvailable,
        Func<string, string?> readEnvironment,
        TextReader standardInput)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(standardInput);
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

        string line = inputs.ReadSecretLine(standardInput);
        return !string.IsNullOrEmpty(line)
            ? line
            : throw CliErrors.OptionInvalid(
                $"{_prefix}-stdin",
                "no password was provided on stdin",
                "Provide the password as the first line of standard input.");
    }
}
