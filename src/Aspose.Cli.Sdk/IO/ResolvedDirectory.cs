namespace Aspose.Cli.Sdk.IO;

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
}
