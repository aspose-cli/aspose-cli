using System.Diagnostics;
using System.Globalization;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// Owns one preview session root. Version and interactive-view stores own only
/// their children; this component performs final cleanup and retries stale
/// roots during the next preview startup after a process interruption.
/// </summary>
internal sealed class ViewerStorage : IDisposable
{
    internal const string SourceDirectory = "source";
    internal const string RevisionsDirectory = "revisions";
    private const int DeleteAttempts = 4;
    private static readonly object Gate = new();
    private static readonly HashSet<string> ActiveRoots = new(
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
    private static readonly long CurrentProcessStart = ReadCurrentProcessStart();

    private readonly Func<string, bool> _deleteDirectory;
    private readonly string _categoryRoot;
    private bool _disposed;

    private ViewerStorage(
        string categoryRoot,
        string root,
        Func<string, bool> deleteDirectory)
    {
        _categoryRoot = categoryRoot;
        Root = root;
        _deleteDirectory = deleteDirectory;
    }

    public string Root { get; }

    public string CreateDocumentRoot(string id)
    {
        if (!IsDocumentId(id)) { throw new ArgumentException("Invalid document id.", nameof(id)); }
        string root = Directory.CreateDirectory(Path.Combine(Root, id)).FullName;
        Directory.CreateDirectory(Path.Combine(root, SourceDirectory));
        Directory.CreateDirectory(Path.Combine(root, RevisionsDirectory));
        return root;
    }

    private static bool IsDocumentId(string name) => name.Length == 32 && name.All(Uri.IsHexDigit);

    private static bool HasExpectedChildren(string root)
    {
        try
        {
            foreach (FileSystemInfo document in new DirectoryInfo(root).EnumerateFileSystemInfos())
            {
                if (document is not DirectoryInfo directory || !IsDocumentId(directory.Name)) { return false; }
                foreach (FileSystemInfo child in directory.EnumerateFileSystemInfos())
                {
                    if (child is not DirectoryInfo content) { return false; }
                    FileSystemInfo[] entries = content.GetFileSystemInfos();
                    if (child.Name == SourceDirectory)
                    {
                        if (entries.Length > 1 || entries.Any(item => item is not FileInfo)) { return false; }
                    }
                    else if (child.Name != RevisionsDirectory || entries.Any(item =>
                        item is not DirectoryInfo || !RevisionStore.TryParseRevision(item.Name, out _)))
                    { return false; }
                }
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }

    public static ViewerStorage Create()
    {
        string categoryRoot = Path.Combine(
            UserStorage.TemporaryRoot(),
            "preview");
        return Create(
            categoryRoot,
            IsOwnerAlive,
            static path => LocalFileCleanup.DeleteDirectory(path));
    }

    internal static ViewerStorage Create(
        string categoryRoot,
        Func<int, long, bool> isOwnerAlive,
        Func<string, bool> deleteDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryRoot);
        ArgumentNullException.ThrowIfNull(isOwnerAlive);
        ArgumentNullException.ThrowIfNull(deleteDirectory);
        string category = Directory.CreateDirectory(Path.GetFullPath(categoryRoot)).FullName;
        lock (Gate)
        {
            SweepStaleRoots(category, isOwnerAlive, deleteDirectory);
            string name = string.Create(
                CultureInfo.InvariantCulture,
                $"{Environment.ProcessId}-{CurrentProcessStart}-{Guid.NewGuid():N}");
            string root = Directory.CreateDirectory(
                Path.Combine(category, name)).FullName;
            ActiveRoots.Add(root);
            return new ViewerStorage(
                category,
                root,
                deleteDirectory);
        }
    }

    public void Dispose()
    {
        lock (Gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ActiveRoots.Remove(Root);
        }

        TimeSpan delay = TimeSpan.FromMilliseconds(25);
        for (int attempt = 1; attempt <= DeleteAttempts; attempt++)
        {
            if (!IsOwnedRoot(_categoryRoot, Root)
                || !OwnedDirectory.CanDelete(
                    _categoryRoot,
                    Root,
                    static name => TryParseOwner(name, out _, out _))
                || !HasExpectedChildren(Root)
                || _deleteDirectory(Root))
            {
                return;
            }
            if (attempt < DeleteAttempts)
            {
                Thread.Sleep(delay);
                delay += delay;
            }
        }
        // A later Create call recognizes this exact inactive owned root and
        // retries it. Unknown names and roots owned by live processes are
        // always preserved.
    }

    private static void SweepStaleRoots(
        string categoryRoot,
        Func<int, long, bool> isOwnerAlive,
        Func<string, bool> deleteDirectory)
    {
        string[] directories;
        try
        {
            directories = Directory.EnumerateDirectories(categoryRoot).ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (string directory in directories)
        {
            string full = Path.GetFullPath(directory);
            if (ActiveRoots.Contains(full)
                || IsReparsePoint(full)
                || !IsOwnedRoot(categoryRoot, full)
                || !TryParseOwner(
                    Path.GetFileName(full),
                    out int processId,
                    out long processStart)
                || isOwnerAlive(processId, processStart)
                || !OwnedDirectory.CanDelete(
                    categoryRoot,
                    full,
                    static name => TryParseOwner(name, out _, out _))
                || !HasExpectedChildren(full))
            {
                continue;
            }

            deleteDirectory(full);
        }
    }

    private static bool TryParseOwner(
        string name,
        out int processId,
        out long processStart)
    {
        processId = 0;
        processStart = 0;
        string[] parts = name.Split('-', 3);
        return parts.Length == 3
            && parts[2].Length == 32
            && parts[2].All(Uri.IsHexDigit)
            && int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processId)
            && processId > 0
            && long.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processStart)
            && processStart > 0;
    }

    private static bool IsOwnerAlive(
        int processId,
        long processStart)
    {
        if (processId == Environment.ProcessId)
        {
            // Roots of this process that are still live are already present
            // in ActiveRoots. An unregistered root belongs to a failed or
            // disposed session and is safe to retry.
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited
                && process.StartTime.ToUniversalTime().Ticks == processStart;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or NotSupportedException)
        {
            // An inaccessible process is treated as live. Cleanup must fail
            // safe and may leave a stale session directory rather than delete
            // another process's active session.
            return true;
        }
    }

    private static long ReadCurrentProcessStart()
    {
        using Process process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime().Ticks;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return Directory.Exists(path)
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsOwnedRoot(
        string categoryRoot,
        string candidate)
    {
        try
        {
            string full = Path.GetFullPath(candidate);
            string? parent = Path.GetDirectoryName(full);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return parent is not null
                && string.Equals(
                    Path.TrimEndingDirectorySeparator(parent),
                    Path.TrimEndingDirectorySeparator(categoryRoot),
                    comparison)
                && TryParseOwner(
                    Path.GetFileName(full),
                    out _,
                    out _)
                && !IsReparsePoint(full);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return false;
        }
    }
}
