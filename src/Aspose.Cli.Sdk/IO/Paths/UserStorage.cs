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
    /// creating it when absent, as the path the file system reaches what is created in it by.
    /// Scratch directories, worker outputs, local service locks and update staging all live
    /// below it.
    /// </summary>
    /// <remarks>
    /// The CLI compares a file it opened with the path it named (<c>VerifiedFileBoundary</c>,
    /// the publication anchors), so every path below the root must be one no link or redirection
    /// changes. Two do: a junction or link in the folder's own path, and the AppData redirection
    /// of an MSIX-packaged host (such as a desktop application that runs the CLI), which creates
    /// a new directory under <c>Packages\&lt;package&gt;\LocalCache\Local</c> even inside a root
    /// that exists where the variable names. A fresh directory created in the root and removed
    /// again shows where new content lands, and the root is reported there.
    /// </remarks>
    public static string TemporaryRoot() =>
        WhereContentLands(Directory.CreateDirectory(Path.Combine(LocalApplicationData(), DistributionInfo.Id)).FullName);

    // The directory a new directory created in root is reached by; root itself when the file
    // system reports no drive path for it.
    private static string WhereContentLands(string root)
    {
        RemoveAbandonedProbes(root);
        string probe = Directory.CreateDirectory(Path.Combine(root, ProbePrefix + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            using Microsoft.Win32.SafeHandles.SafeFileHandle handle = OpenedFileBoundary.OpenNoFollow(probe, directory: true);
            return !handle.IsInvalid && OpenedFileBoundary.FinalPath(handle) is { } landed && Path.GetDirectoryName(landed) is { } parent
                ? parent
                : root;
        }
        finally
        {
            TryRemoveProbe(probe);
        }
    }

    private const string ProbePrefix = ".location-";

    // A probe another process is using is younger than this; an older one was left by a process
    // that ended before removing it.
    private static readonly TimeSpan AbandonedProbeAge = TimeSpan.FromHours(1);

    private static void RemoveAbandonedProbes(string root)
    {
        try
        {
            foreach (DirectoryInfo probe in new DirectoryInfo(root).EnumerateDirectories(ProbePrefix + "*"))
            {
                if (DateTime.UtcNow - probe.CreationTimeUtc > AbandonedProbeAge)
                {
                    TryRemoveProbe(probe.FullName);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Leftover probes are empty directories; they never fail a command.
        }
    }

    // A probe is an empty directory: one that cannot be removed now stays for a later run.
    private static void TryRemoveProbe(string probe)
    {
        try
        {
            Directory.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

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
