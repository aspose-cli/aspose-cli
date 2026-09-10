namespace Aspose.Cli.Sdk.IO;

/// <summary>Provides a non-mutating probe for shared-read file access.</summary>
public static class FileAccessProbe
{
    /// <summary>
    /// Returns whether <paramref name="path"/> can be opened for reading while
    /// allowing concurrent readers, writers, and deletion.
    /// </summary>
    public static bool CanOpenForRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using FileStream stream = File.Open(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return stream.CanRead;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
