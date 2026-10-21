using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;

namespace Aspose.Cli.Sdk.IO.Paths;

/// <summary>
/// Locates the current user's Aspose CLI local state and removes bounded trees from it. The
/// root is the CLI's directory in the user's local application data folder
/// (<c>%LOCALAPPDATA%</c>), inside the user profile, whose Windows permissions admit no other
/// standard account; a shared temporary directory, where another account could create the
/// root first, holds none of it. This class sets and validates no access control of its own.
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

    /// <summary>
    /// Returns the current user's Aspose CLI local state root, <c>%LOCALAPPDATA%\aspose-cli</c>,
    /// creating it when absent. Scratch directories, worker outputs, local service locks and
    /// update staging all live below it.
    /// </summary>
    public static string TemporaryRoot() =>
        Directory.CreateDirectory(Path.Combine(LocalApplicationData(), DistributionInfo.Id)).FullName;

    // The variable Windows sets for the user, as install.ps1 reads it; the known folder when it
    // is unset or not an absolute path.
    private static string LocalApplicationData()
    {
        string? variable = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        string folder = !string.IsNullOrWhiteSpace(variable) && Path.IsPathFullyQualified(variable)
            ? variable
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (!Path.IsPathFullyQualified(folder))
        {
            throw new IOException("The current user has no local application data folder.");
        }
        return Path.GetFullPath(folder);
    }

    /// <summary>
    /// Neutral per-user namespace for cross-application document publication locks. Other Aspose
    /// CLI distributions take the same lock files, so this root keeps its temporary-directory
    /// location and name.
    /// </summary>
    // Not in %LOCALAPPDATA% like the rest: moving it would split the locks from those of the
    // other distributions. The lock files are empty and only held open, so another account
    // that shares a temporary directory and creates this root first can at worst delay or fail
    // a publication; it never reads or changes a document.
    internal static string PublicationLockRoot() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "aspose-document-publication-" + CurrentUserSuffix())).FullName;

    private static string CurrentUserSuffix()
    {
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
                if (identity is null
                    || FilePublicationOwnedDelete.TryGetDirectoryIdentity(
                        directory) != identity)
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
