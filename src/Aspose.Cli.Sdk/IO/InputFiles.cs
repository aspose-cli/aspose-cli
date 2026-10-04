namespace Aspose.Cli.Sdk.IO;

/// <summary>The one open policy for reading an input file.</summary>
public static class InputFiles
{
    /// <summary>
    /// Opens a file for reading without blocking other readers or a writer that replaces or
    /// deletes it, as another command's in-place edit does; this read keeps the bytes it opened.
    /// </summary>
    public static FileStream OpenRead(string path, FileOptions options = FileOptions.SequentialScan) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024, options);
}
