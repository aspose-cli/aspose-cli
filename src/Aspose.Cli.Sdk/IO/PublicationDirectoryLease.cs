using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Shared ancestor and exclusive publication-directory leases, released by handle.</summary>
internal sealed class PublicationDirectoryLease : IDisposable
{
    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(30);
    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
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
        string[] requested = outputDirectories.Select(Normalize).Distinct(Comparer).ToArray();
        if (requested.Length == 0) { requested = [root]; }
        string[] exclusive = requested.Where(path => !requested.Any(other => !Comparer.Equals(path, other) && IsWithin(other, path))).ToArray();
        var requests = new Dictionary<string, bool>(Comparer);
        var volumes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in exclusive)
        {
            OutputPathValidator.EnsureSafeDirectory(path);
            string existing = path;
            while (!System.IO.Directory.Exists(existing)) { existing = Path.GetDirectoryName(existing)!; }
            FilePhysicalIdentity? identity = FilePublicationOwnedDelete.TryGetDirectoryIdentity(existing);
            if (OperatingSystem.IsWindows() && identity is null) { throw CliErrors.OutputUnwritable(path, "the output volume identity could not be verified"); }
            volumes.Add(OperatingSystem.IsWindows() ? $"volume:{identity!.Value.VolumeSerialNumber:X8}" : Path.GetPathRoot(path)!);
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            {
                bool write = Comparer.Equals(current, path);
                requests[current] = write || requests.GetValueOrDefault(current);
            }
        }
        string lockRoot = PrivateUserStorage.EnsureDirectory(Path.Combine(PrivateUserStorage.PublicationLockRoot(), "publication-locks"));
        using OperationDeadline? fallback = deadline?.OriginalBudget is null
            ? OperationDeadline.Start(DefaultWait, deadline?.Token ?? CancellationToken.None) : null;
        OperationDeadline wait = fallback ?? deadline!;
        var handles = new List<FileStream>();
        try
        {
            // The existing neutral volume key remains a shared compatibility barrier.
            foreach (string volume in volumes) { handles.Add(AcquireFile(lockRoot, volume, exclusive: false, wait)); }
            foreach ((string path, bool write) in requests.OrderBy(item => item.Key, Comparer))
            {
                handles.Add(AcquireFile(lockRoot, "directory:" + (OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path), write, wait));
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
        directories.All(path => _exclusive.Any(root => IsWithin(root, Normalize(path))));

    public void EnsureCovers(string path)
    {
        OutputPathValidator.EnsureSafeFile(path);
        EnsureDirectoryUnchanged();
        string parent = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (!IsWithin(Directory, parent) || !CoversDirectories([parent]))
        {
            throw new IOException($"Publication target '{path}' is outside the leased resources.");
        }
    }

    internal void EnsureDirectoryUnchanged()
    {
        OutputPathValidator.EnsureSafeDirectory(Directory);
        if (OperatingSystem.IsWindows() && _directoryIdentity != FilePublicationOwnedDelete.TryGetDirectoryIdentity(Directory))
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
            try { using FileStream created = PrivateUserStorage.CreateFile(path); }
            catch (IOException) when (File.Exists(path)) { }
        }
        PrivateUserStorage.ValidateFile(path);
        while (true)
        {
            deadline.ThrowIfExpired("publication-lock");
            FileStream? stream = null;
            try
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    OperatingSystem.IsWindows() ? (exclusive ? FileShare.None : FileShare.Read) : FileShare.ReadWrite,
                    bufferSize: 1, FileOptions.None);
                if (!OperatingSystem.IsWindows() && Flock(stream.SafeFileHandle.DangerousGetHandle().ToInt32(), (exclusive ? 2 : 1) | 4) != 0)
                {
                    int error = Marshal.GetLastPInvokeError();
                    stream.Dispose();
                    stream = null;
                    if (error is not (11 or 35)) { throw new IOException("The publication lock could not be acquired.", new Win32Exception(error)); }
                }
                if (stream is not null) { return stream; }
            }
            catch (IOException exception) when (OperatingSystem.IsWindows() && (exception.HResult & 0xffff) is 32 or 33)
            {
                stream?.Dispose();
            }
            if (deadline.Token.WaitHandle.WaitOne(25)) { deadline.ThrowIfExpired("publication-lock"); }
        }
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool IsWithin(string parent, string path) => Comparer.Equals(parent, path)
        || path.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(int descriptor, int operation);
}

internal sealed class PublicationLeaseExpansionException : Exception;
