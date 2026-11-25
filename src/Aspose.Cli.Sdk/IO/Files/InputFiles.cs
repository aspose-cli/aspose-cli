namespace Aspose.Cli.Sdk.IO.Files;

/// <summary>The one open policy for reading an input file.</summary>
public static class InputFiles
{
    /// <summary>
    /// The size of the buffer <see cref="OpenRead"/> gives its stream. The stream serves a read
    /// that asks for fewer bytes through that buffer, which keeps them after the stream is
    /// closed; a read that asks for at least this many goes straight into the caller's memory.
    /// </summary>
    internal const int BufferSize = 64 * 1024;

    /// <summary>
    /// Opens a file for reading without blocking other readers or a writer that replaces or
    /// deletes it, as another command's in-place edit does; this read keeps the bytes it opened.
    /// </summary>
    public static FileStream OpenRead(string path, FileOptions options = FileOptions.SequentialScan) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, options);
}
