namespace Aspose.Cli.Sdk.IO.Output;

/// <summary>
/// The directory a command publishes a set of files in, resolved once per invocation by the
/// command template: its absolute path, which may not exist yet, and whether files the command
/// writes there may replace existing ones.
/// </summary>
public sealed record ResolvedDirectory
{
    /// <summary>Describes one resolved output directory.</summary>
    /// <param name="path">The directory; it is made absolute.</param>
    /// <param name="overwrite">Whether a file the command writes may replace an existing one.</param>
    public ResolvedDirectory(string path, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        Overwrite = overwrite;
    }

    /// <summary>The absolute directory path.</summary>
    public string Path { get; }

    /// <summary>Whether a file the command writes may replace an existing one.</summary>
    public bool Overwrite { get; }

    /// <summary>
    /// The paths of the parts numbered <paramref name="numbers"/>, such as the parts a split
    /// writes: <c>&lt;stem&gt;.&lt;n&gt;.&lt;extension&gt;</c> in the directory, with <c>n</c> padded to
    /// the digits of the highest number, as <c>report.01.pdf</c> through <c>report.12.pdf</c>.
    /// </summary>
    /// <param name="stem">The file name stem, usually the input's; characters no file name takes become <c>-</c>.</param>
    /// <param name="extension">The extension, with or without its dot.</param>
    /// <param name="numbers">The part numbers, each at least 1.</param>
    public IReadOnlyList<string> Parts(string stem, string extension, IReadOnlyList<int> numbers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        string name = PartNames.SafeStem(stem);
        string dotted = extension.StartsWith('.') ? extension : "." + extension;
        return [.. PartNames.Numbered(numbers).Select(label => System.IO.Path.Combine(Path, $"{name}.{label}{dotted}"))];
    }
}
