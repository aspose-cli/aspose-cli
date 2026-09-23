namespace Aspose.Cli.Sdk.IO;

/// <summary>Provides a non-mutating probe for shared-read file access.</summary>
public static class FileAccessProbe
{
    /// <summary>Recognizes native sharing and lock violations without treating parser IO failures as locks.</summary>
    public static bool IsSharingViolation(IOException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        // HRESULT_FROM_WIN32: ERROR_SHARING_VIOLATION and ERROR_LOCK_VIOLATION.
        return exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021);
    }

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
