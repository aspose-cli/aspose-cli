using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Adds a filesystem permission boundary to the runtime's Unix named-pipe
/// socket. <see cref="System.IO.Pipes.PipeOptions.CurrentUserOnly"/> still
/// supplies peer-credential authorization; this makes the socket itself
/// inaccessible to group and other users before the first accept.
/// </summary>
internal static class CurrentUserPipeSecurity
{
    private const UnixFileMode PrivateSocketMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static void HardenPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new IOException(
                "The current-user pipe socket was not created at the expected runtime path.");
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException(
                "The current-user pipe socket is a linked filesystem entry.");
        }

        File.SetUnixFileMode(path, PrivateSocketMode);
        UnixFileMode actual = File.GetUnixFileMode(path);
        if (actual != PrivateSocketMode)
        {
            throw new UnauthorizedAccessException(
                $"The current-user pipe socket permissions are not 0600 (actual: {actual}).");
        }

        PrivateUserStorage.ValidateUnixOwner(path);
    }
}
