using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Locates the current user's Aspose CLI temporary storage and removes bounded trees from it.
/// Roots live below the user's temporary directory and carry a hash of the user's SID, which
/// keeps users who share one temporary directory apart. Access control is the one Windows
/// gives the temporary directory; this class sets and validates none.
/// </summary>
public static class UserStorage
{
    // A worker can retain a bounded directory tree plus scratch and file transactions.
    internal const int MaximumCleanupEntries = 2 * (PublicationLimits.MaximumDirectoryFiles + PublicationLimits.MaximumDirectories);

    /// <summary>
    /// Creates a fresh, uniquely named directory in <paramref name="category"/> below
    /// <see cref="TemporaryRoot"/>.
    /// </summary>
    public static string CreateTemporaryDirectory(
        string category,
        string? namePrefix = null)
    {
        if (string.IsNullOrWhiteSpace(category)
            || category.Any(static character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Temporary storage categories use letters, digits, '-' or '_'.",
                nameof(category));
        }

        string prefix = string.IsNullOrWhiteSpace(namePrefix)
            ? string.Empty
            : namePrefix + "-";
        return Directory.CreateDirectory(Path.Combine(
            TemporaryRoot(),
            category,
            prefix + Guid.NewGuid().ToString("N"))).FullName;
    }

    /// <summary>Returns the current user's Aspose CLI temporary root, creating it when absent.</summary>
    public static string TemporaryRoot() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), DistributionInfo.Id + "-" + CurrentUserSuffix())).FullName;

    /// <summary>Neutral per-user namespace for cross-application document publication locks.</summary>
    internal static string PublicationLockRoot() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aspose-document-publication-" + CurrentUserSuffix())).FullName;

    private static string CurrentUserSuffix()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Aspose CLI storage requires Windows.");
        }

        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        string identity = current.User!.Value;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 8)).ToLowerInvariant();
    }

    /// <summary>
    /// Deletes a bounded tree without following links. Every file is removed only while it
    /// still matches the physical object that was admitted for cleanup; unknown or
    /// concurrently changed content is kept.
    /// </summary>
    public static bool TryDeleteTree(string root)
    {
        string full = Path.GetFullPath(root);
        FileAttributes? rootAttributes =
            FilePublicationOwnedDelete.TryGetAttributesNoFollow(full);
        if (rootAttributes is null)
        {
            return true;
        }
        if (!rootAttributes.Value.HasFlag(FileAttributes.Directory)
            || rootAttributes.Value.HasFlag(FileAttributes.ReparsePoint)
            || !Directory.Exists(full))
        {
            return false;
        }

        try
        {
            var pending = new Stack<string>();
            var directories = new List<(
                string Path,
                FilePhysicalIdentity? Identity)>();
            pending.Push(full);
            int entries = 0;
            while (pending.TryPop(out string? directory))
            {
                FilePhysicalIdentity? identity =
                    FilePublicationOwnedDelete.TryGetDirectoryIdentity(
                        directory);
                if (OperatingSystem.IsWindows()
                    && (identity is null
                        || FilePublicationOwnedDelete.TryGetDirectoryIdentity(
                            directory) != identity))
                {
                    return false;
                }
                directories.Add((directory, identity));
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entries > MaximumCleanupEntries)
                    {
                        return false;
                    }
                    if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
                    }
                    if (Directory.Exists(entry))
                    {
                        pending.Push(entry);
                        continue;
                    }

                    FilePublicationSnapshot snapshot =
                        FilePublicationSnapshot.Capture(entry);
                    if (!FilePublicationOwnedDelete.TryDelete(entry, snapshot))
                    {
                        return false;
                    }
                }
            }

            foreach ((string directory, FilePhysicalIdentity? identity)
                     in directories.OrderByDescending(
                         static item => item.Path.Length))
            {
                if (!FilePublicationOwnedDelete.TryDeleteDirectory(
                        directory,
                        identity))
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or DirectoryNotFoundException)
        {
            return FilePublicationOwnedDelete.TryGetAttributesNoFollow(full)
                is null;
        }
    }
}
