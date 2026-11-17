namespace Aspose.Cli.Host.LocalServices;

/// <summary>Best-effort cleanup for current-user temporary service files.</summary>
internal static class LocalFileCleanup
{
    public static bool DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
