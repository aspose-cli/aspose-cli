using System.Globalization;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// The one file a command publishes, resolved once per invocation by the command template:
/// its format, its absolute path, whether an existing file may be replaced and, for an edit, the
/// in-place mode and its backup. The path always carries one of the format's extensions. A
/// product writes the output as given and names the files of an output written in parts with
/// <see cref="Part(string, int, int)"/> or <see cref="Part(string)"/>.
/// </summary>
public sealed record ResolvedOutput
{
    private readonly IReadOnlyList<FormatDescriptor> _alternatives;

    /// <summary>Describes one resolved output.</summary>
    /// <param name="format">The format the output is written in.</param>
    /// <param name="path">The output path; it is made absolute.</param>
    /// <param name="overwrite">Whether an existing file may be replaced.</param>
    /// <param name="inPlace">Whether the output replaces the edited input itself.</param>
    /// <param name="backupPath">The backup an in-place edit writes first, or null.</param>
    public ResolvedOutput(FormatDescriptor format, string path, bool overwrite = false, bool inPlace = false, string? backupPath = null)
        : this(format, path, overwrite, inPlace, backupPath, [])
    {
    }

    internal ResolvedOutput(
        FormatDescriptor format,
        string path,
        bool overwrite,
        bool inPlace,
        string? backupPath,
        IReadOnlyList<FormatDescriptor> alternatives)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Format = format;
        Path = System.IO.Path.GetFullPath(path);
        Overwrite = overwrite;
        InPlace = inPlace;
        BackupPath = backupPath;
        _alternatives = alternatives;
    }

    /// <summary>The format the output is written in.</summary>
    public FormatDescriptor Format { get; }

    /// <summary>The absolute output path.</summary>
    public string Path { get; }

    /// <summary>Whether an existing file at <see cref="Path"/> may be replaced.</summary>
    public bool Overwrite { get; }

    /// <summary>Whether the output replaces the edited input itself.</summary>
    public bool InPlace { get; }

    /// <summary>The backup an in-place edit writes before replacing its input, or null.</summary>
    public string? BackupPath { get; }

    /// <summary>The directory the output is published in.</summary>
    public string Directory => System.IO.Path.GetDirectoryName(Path)!;

    /// <summary>
    /// The format to write a document whose own format is <paramref name="sourceFormatId"/>:
    /// that format when the output's extension names it too and the command writes it, so an
    /// edited WordML <c>.xml</c> file stays WordML; otherwise <see cref="Format"/>. Only an
    /// output whose extension chose its format has such alternatives.
    /// </summary>
    public FormatDescriptor Keeping(string? sourceFormatId) =>
        _alternatives.FirstOrDefault(format => string.Equals(format.Id, sourceFormatId, StringComparison.Ordinal)) ?? Format;

    /// <summary>The formats the output's extension names, in declared order; empty when only one does.</summary>
    internal IReadOnlyList<FormatDescriptor> Alternatives => _alternatives;

    /// <summary>
    /// The path of the part numbered <paramref name="number"/> of an output written in
    /// <paramref name="parts"/> parts: the output itself for a single part, and otherwise a file
    /// beside it with the product's part marker and the number before the extension, as
    /// <c>report.p3.png</c> for marker <c>p</c>.
    /// </summary>
    public string Part(string marker, int number, int parts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);
        return parts == 1 ? Path : Part(string.Create(CultureInfo.InvariantCulture, $"{marker}{number}"));
    }

    /// <summary>
    /// The path of a part named <paramref name="label"/>, such as a sheet name the product has
    /// made safe for a file name: <c>report.png</c> becomes <c>report.Summary.png</c>.
    /// </summary>
    public string Part(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return System.IO.Path.Combine(
            Directory,
            $"{System.IO.Path.GetFileNameWithoutExtension(Path)}.{label}{System.IO.Path.GetExtension(Path)}");
    }
}
