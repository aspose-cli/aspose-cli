using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>Repeatable explicit font-root option for visual commands.</summary>
public sealed class FontDirectoryOptions
{
    private const int MaximumDirectories = 16;
    private readonly Option<string[]> _directories = new Option<string[]>("--font-dir")
    {
        Description = "Local font directory; repeat to define an explicit-only deterministic font profile.",
        Arity = new ArgumentArity(1, MaximumDirectories),
        AllowMultipleArgumentsPerToken = false,
    }.WithInput(InputKind.None);

    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(_directories);
    }

    public FontSearchProfile Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        string[] values = parse.GetValue(_directories) ?? [];
        if (values.Length == 0)
        {
            return FontSearchProfile.Ambient;
        }
        if (values.Length > MaximumDirectories)
        {
            throw CliErrors.OptionInvalid(
                "--font-dir",
                $"at most {MaximumDirectories} directories are allowed",
                "Remove duplicate or unnecessary font directories.");
        }

        var directories = new List<string>(values.Length);
        var seen = new HashSet<string>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
        foreach (string value in values)
        {
            string fullPath = Validate(value);
            if (seen.Add(fullPath))
            {
                directories.Add(fullPath);
            }
        }
        try
        {
            return FontSearchProfile.Explicit(directories);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw CliErrors.OptionInvalid(
                "--font-dir",
                "the explicit font profile could not be read safely",
                "Use stable local font directories within the documented file and byte budgets.");
        }
    }

    private static string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Path.IsPathFullyQualified(value)
            || value.StartsWith("\\\\", StringComparison.Ordinal)
            || value.StartsWith("\\\\?\\", StringComparison.Ordinal)
            || value.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            throw Invalid(value, "must be an absolute local path");
        }

        string fullPath;
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Invalid(value, "is not a valid local path");
        }
        if (!Directory.Exists(fullPath))
        {
            throw Invalid(value, "does not exist or is not a directory");
        }
        try
        {
            PrivateUserStorage.RejectLinkedComponents(fullPath, includeLeaf: true);
        }
        catch (UnauthorizedAccessException)
        {
            throw Invalid(value, "must not traverse a reparse point");
        }
        if (OperatingSystem.IsWindows())
        {
            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)
                || new DriveInfo(root).DriveType != DriveType.Fixed)
            {
                throw Invalid(value, "must be on a fixed local drive");
            }
        }
        return fullPath;
    }

    private static CliException Invalid(string value, string reason) =>
        CliErrors.OptionInvalid(
            "--font-dir",
            $"'{value}' {reason}",
            "Use an existing absolute directory on a fixed local disk; UNC, device and relative paths are rejected.");
}
