using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Sdk.IO.Publication;

/// <summary>Shared ancestor and exclusive publication-directory leases, released by handle.</summary>
internal sealed class PublicationDirectoryLease : IDisposable
{
    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(30);
    private readonly List<FileStream> _handles;
    private readonly string[] _exclusive;
    private readonly FilePhysicalIdentity? _directoryIdentity;

    private PublicationDirectoryLease(string directory, string[] exclusive, List<FileStream> handles)
    {
        Directory = directory;
        _exclusive = exclusive;
        _handles = handles;
        _directoryIdentity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(directory);
    }

    public string Directory { get; }

    public static PublicationDirectoryLease Acquire(string directory) => Acquire(directory, [directory], deadline: null);

    internal static PublicationDirectoryLease Acquire(string directory, IEnumerable<string> outputDirectories, OperationDeadline? deadline)
    {
        string root = Normalize(directory);
        OutputPathValidator.EnsureSafeDirectory(root);
        string[] requested = outputDirectories.Select(Normalize).Distinct(FilePaths.Comparer).ToArray();
        if (requested.Length == 0) { requested = [root]; }
        string[] exclusive = requested.Where(path => !requested.Any(other => !FilePaths.Same(path, other) && FilePaths.IsWithin(other, path, allowEqual: true))).ToArray();
        var requests = new Dictionary<string, bool>(FilePaths.Comparer);
        var volumes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in exclusive)
        {
            OutputPathValidator.EnsureSafeDirectory(path);
            string existing = path;
            while (!System.IO.Directory.Exists(existing)) { existing = Path.GetDirectoryName(existing)!; }
            FilePhysicalIdentity? identity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(existing);
            if (identity is null) { throw CliErrors.OutputUnwritable(path, "the output volume identity could not be verified"); }
            volumes.Add($"volume:{identity.Value.VolumeSerialNumber:X8}");
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            {
                bool write = FilePaths.Same(current, path);
                requests[current] = write || requests.GetValueOrDefault(current);
            }
        }
        string lockRoot = System.IO.Directory.CreateDirectory(Path.Combine(UserStorage.PublicationLockRoot(), "publication-locks")).FullName;
        using OperationDeadline? fallback = deadline?.OriginalBudget is null
            ? OperationDeadline.Start(DefaultWait, deadline?.Token ?? CancellationToken.None) : null;
        OperationDeadline wait = fallback ?? deadline!;
        var handles = new List<FileStream>();
        try
        {
            // Every lease holds the volume key shared, so it waits for any publisher that locks
            // the whole volume exclusively.
            foreach (string volume in volumes) { handles.Add(AcquireFile(lockRoot, volume, exclusive: false, wait)); }
            foreach ((string path, bool write) in requests.OrderBy(item => item.Key, FilePaths.Comparer))
            {
                handles.Add(AcquireFile(lockRoot, "directory:" + FilePaths.LockKey(path), write, wait));
            }
            return new PublicationDirectoryLease(root, exclusive, handles);
        }
        catch
        {
            foreach (FileStream handle in handles.AsEnumerable().Reverse()) { handle.Dispose(); }
            throw;
        }
    }

    internal bool CoversDirectories(IEnumerable<string> directories) =>
        directories.All(path => _exclusive.Any(root => FilePaths.IsWithin(root, Normalize(path), allowEqual: true)));

    public void EnsureCovers(string path)
    {
        OutputPathValidator.EnsureSafeFile(path);
        EnsureDirectoryUnchanged();
        string parent = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (!FilePaths.IsWithin(Directory, parent, allowEqual: true) || !CoversDirectories([parent]))
        {
            throw new IOException($"Publication target '{path}' is outside the leased resources.");
        }
    }

    internal void EnsureDirectoryUnchanged()
    {
        OutputPathValidator.EnsureSafeDirectory(Directory);
        if (_directoryIdentity != FilePublicationOwnedDelete.TryGetDirectoryIdentity(Directory))
        {
            throw CliErrors.OutputUnwritable(Directory, "the leased output directory changed during publication", phase: "path");
        }
    }

    public void Dispose()
    {
        foreach (FileStream handle in _handles.AsEnumerable().Reverse()) { handle.Dispose(); }
        _handles.Clear();
    }

    private static FileStream AcquireFile(string root, string key, bool exclusive, OperationDeadline deadline)
    {
        string path = Path.Combine(root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".lock");
        if (!File.Exists(path))
        {
            try { using FileStream created = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (File.Exists(path)) { }
        }
        while (true)
        {
            deadline.ThrowIfExpired("publication-lock");
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read,
                    exclusive ? FileShare.None : FileShare.Read, bufferSize: 1, FileOptions.None);
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33)
            {
            }
            if (deadline.Token.WaitHandle.WaitOne(25)) { deadline.ThrowIfExpired("publication-lock"); }
        }
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}

internal sealed class PublicationLeaseExpansionException : Exception;
