using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Rendering;

/// <summary>
/// Font directories a visual command adds to the system fonts. The ambient
/// profile adds none. An explicit profile holds validated absolute local
/// directories whose font files stay within a bounded count and size, so an
/// engine never scans an unbounded tree on the caller's behalf.
/// </summary>
public sealed class FontSearchProfile
{
    private const int MaximumCandidates = 4096;
    private const long MaximumFontBytes = 64L * 1024 * 1024;
    private const long MaximumProfileBytes = 1024L * 1024 * 1024;

    private FontSearchProfile(IReadOnlyList<string> directories)
    {
        Directories = directories;
    }

    /// <summary>The system fonts alone.</summary>
    public static FontSearchProfile Ambient { get; } = new([]);

    /// <summary>Absolute directories searched in addition to the system fonts.</summary>
    public IReadOnlyList<string> Directories { get; }

    /// <summary>Whether the profile adds no directory.</summary>
    public bool IsAmbient => Directories.Count == 0;

    /// <summary>
    /// Creates a profile of absolute directories after checking the font files
    /// directly inside them against the candidate and byte budgets.
    /// </summary>
    /// <exception cref="CliException">
    /// <c>OPTION_INVALID</c> for <c>--font-dir</c> when a directory cannot be
    /// read, a budget is exceeded or a font file is a reparse point.
    /// </exception>
    public static FontSearchProfile Explicit(IReadOnlyList<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        if (directories.Count == 0)
        {
            return Ambient;
        }

        long totalBytes = 0;
        int candidates = 0;
        try
        {
            foreach (string root in directories)
            {
                if (!Path.IsPathFullyQualified(root))
                {
                    throw new ArgumentException($"Font directory '{root}' is not absolute.", nameof(directories));
                }
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                    .Where(IsFontFile))
                {
                    if (++candidates > MaximumCandidates)
                    {
                        throw Invalid($"the font directories hold more than {MaximumCandidates} font files");
                    }
                    var info = new FileInfo(file);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw Invalid($"font file '{file}' is a reparse point");
                    }
                    if (info.Length > MaximumFontBytes
                        || totalBytes > MaximumProfileBytes - info.Length)
                    {
                        throw Invalid("the font files exceed the byte budget");
                    }
                    totalBytes += info.Length;
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw Invalid("the font directories could not be read");
        }
        return new FontSearchProfile(Array.AsReadOnly(directories.ToArray()));
    }

    private static CliException Invalid(string reason) =>
        CliErrors.OptionInvalid(
            "--font-dir",
            reason,
            $"Use readable local font directories with at most {MaximumCandidates} font files, "
                + $"each at most {MaximumFontBytes / (1024 * 1024)} MiB and "
                + $"{MaximumProfileBytes / (1024 * 1024)} MiB in total.");

    private static bool IsFontFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otc", StringComparison.OrdinalIgnoreCase);
    }
}
