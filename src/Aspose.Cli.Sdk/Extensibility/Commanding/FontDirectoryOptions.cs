using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// The one repeatable <c>--font-dir</c> option of every command whose result depends on
/// the fonts available to the engine: rendering, conversion, layout and font checks.
/// </summary>
public sealed class FontDirectoryOptions
{
    private const int MaximumDirectories = 16;
    private readonly Option<string[]> _directories = new Option<string[]>(StandardOptionNames.FontDir)
    {
        Description = "Local font directory searched in addition to the system fonts; repeat for more.",
        Arity = new ArgumentArity(1, MaximumDirectories),
        AllowMultipleArgumentsPerToken = false,
    }.WithInput(InputKind.None);

    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(_directories);
    }

    /// <summary>
    /// Reads the directories and applies them to the command's product engine until the
    /// returned scope is disposed. A product command enters the scope before its port opens
    /// the document, so layout, rendering and save all see the same fonts.
    /// </summary>
    internal IDisposable Use<TPort>(ParseResult parse, ProductCommandContext<TPort> context)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Binding.UseFonts(Read(parse, context.Paths));
    }

    /// <summary>Reads the directories, resolving relative paths against <paramref name="paths"/>.</summary>
    public FontSearchProfile Read(ParseResult parse, PathResolver paths)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(paths);
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
            string fullPath = Validate(value, paths.BaseDirectory);
            if (seen.Add(fullPath))
            {
                directories.Add(fullPath);
            }
        }
        return FontSearchProfile.Explicit(directories);
    }

    private static string Validate(string value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(value, "is empty");
        }

        string fullPath;
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value, baseDirectory));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Invalid(value, "is not a valid local path");
        }
        if (IsNetworkOrDevicePath(value) || IsNetworkOrDevicePath(fullPath))
        {
            throw Invalid(value, "must be a local path");
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

    private static bool IsNetworkOrDevicePath(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal)
        || (OperatingSystem.IsWindows() && path.StartsWith("//", StringComparison.Ordinal));

    private static CliException Invalid(string value, string reason) =>
        CliErrors.OptionInvalid(
            "--font-dir",
            $"'{value}' {reason}",
            "Use an existing directory on a fixed local disk; relative paths resolve against --workdir, and UNC and device paths are rejected.");
}
