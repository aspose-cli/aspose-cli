using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Serializes publication and recovery on one filesystem volume while keeping
/// the requested directory as the transaction's path boundary.
/// </summary>
internal sealed class PublicationDirectoryLease : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);
    private readonly FileStream _lock;
    private readonly FilePhysicalIdentity? _directoryIdentity;

    private PublicationDirectoryLease(
        string directory,
        FileStream @lock,
        FilePhysicalIdentity? directoryIdentity)
    {
        Directory = directory;
        _lock = @lock;
        _directoryIdentity = directoryIdentity;
    }

    public string Directory { get; }

    public static PublicationDirectoryLease Acquire(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string root = Path.GetFullPath(directory);
        OutputPathValidator.EnsureSafeDirectory(root);
        System.IO.Directory.CreateDirectory(root);
        OutputPathValidator.EnsureSafeDirectory(root);
        FilePhysicalIdentity? directoryIdentity =
            FilePublicationOwnedDelete.TryGetDirectoryIdentity(root);
        if (OperatingSystem.IsWindows() && directoryIdentity is null)
        {
            throw CliErrors.OutputUnwritable(
                root,
                "the output directory identity could not be verified",
                phase: "path");
        }
        string lockDirectory = PrivateUserStorage.EnsureDirectory(Path.Combine(
            PrivateUserStorage.PublicationLockRoot(),
            "publication-locks"));
        string canonical = OperatingSystem.IsWindows()
            ? $"volume:{directoryIdentity!.Value.VolumeSerialNumber:X8}"
            : Path.GetPathRoot(root) ?? root;
        string lockPath = Path.Combine(
            lockDirectory,
            Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical))) + ".lock");
        EnsureLockFile(lockPath);
        PrivateUserStorage.ValidateFile(lockPath);

        DateTime deadline = DateTime.UtcNow + WaitTimeout;
        while (true)
        {
            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
                var lease = new PublicationDirectoryLease(
                    root,
                    stream,
                    directoryIdentity);
                try
                {
                    lease.EnsureDirectoryUnchanged();
                    return lease;
                }
                catch
                {
                    lease.Dispose();
                    throw;
                }
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(25);
            }
            catch (IOException)
            {
                throw CliErrors.OperationTimeout(
                    (int)WaitTimeout.TotalSeconds,
                    "publication-lock");
            }
        }
    }

    public void EnsureCovers(string path)
    {
        OutputPathValidator.EnsureSafeFile(path);
        EnsureDirectoryUnchanged(path);
        string parent = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new IOException($"Publication path '{path}' has no parent directory.");
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string prefix = Directory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!string.Equals(parent, Directory, comparison)
            && !parent.StartsWith(prefix, comparison))
        {
            throw new IOException(
                $"Publication target '{path}' is outside the leased directory '{Directory}'.");
        }
    }

    internal void EnsureDirectoryUnchanged() =>
        EnsureDirectoryUnchanged(Directory);

    private void EnsureDirectoryUnchanged(string contextPath)
    {
        FilePhysicalIdentity? currentIdentity =
            FilePublicationOwnedDelete.TryGetDirectoryIdentity(Directory);
        if (OperatingSystem.IsWindows()
            && currentIdentity != _directoryIdentity)
        {
            throw CliErrors.OutputUnwritable(
                contextPath,
                "the leased output directory changed during publication",
                phase: "path");
        }
    }

    public void Dispose() => _lock.Dispose();

    private static void EnsureLockFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        try
        {
            using FileStream _ = PrivateUserStorage.CreateFile(path);
        }
        catch (IOException) when (File.Exists(path))
        {
        }
    }
}
