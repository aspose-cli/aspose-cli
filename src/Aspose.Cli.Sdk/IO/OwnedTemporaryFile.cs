using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Reserves a temporary path and binds cleanup to the created file object.
/// The Windows identity handle requests no data access and shares read, write,
/// and delete, so product engines can use their normal path-based save APIs.
/// </summary>
public sealed class OwnedTemporaryFile : IDisposable
{
    private FilePhysicalIdentity? _identity;
    private SafeFileHandle? _identityHandle;
    private bool _producedBound;
    private bool _published;
    private bool _disposed;

    private OwnedTemporaryFile(
        string path,
        FilePhysicalIdentity? identity,
        SafeFileHandle? identityHandle)
    {
        Path = path;
        _identity = identity;
        _identityHandle = identityHandle;
    }

    public string Path { get; private set; }

    public static OwnedTemporaryFile Create(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using (new FileStream(
                   path,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.ReadWrite | FileShare.Delete,
                   bufferSize: 1,
                   FileOptions.None))
        {
        }

        if (!OperatingSystem.IsWindows())
        {
            return new OwnedTemporaryFile(
                path,
                identity: null,
                identityHandle: null);
        }

        SafeFileHandle handle = FilePublicationOwnedDelete.OpenIdentityHandle(path);
        FilePhysicalIdentity? identity =
            FilePublicationOwnedDelete.TryGetIdentity(handle);
        if (handle.IsInvalid || identity is null)
        {
            handle.Dispose();
            File.Delete(path);
            throw new IOException(
                $"Could not bind temporary file '{path}' to a physical identity.");
        }

        return new OwnedTemporaryFile(
            path,
            identity,
            handle);
    }

    public void BindProducedFile()
    {
        if (!File.Exists(Path) || Directory.Exists(Path))
        {
            throw new IOException(
                $"The producer did not leave a regular temporary file at '{Path}'.");
        }
        if ((File.GetAttributes(Path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                $"The producer left a link or reparse point at '{Path}'.");
        }

        if (OperatingSystem.IsWindows())
        {
            SafeFileHandle current =
                FilePublicationOwnedDelete.OpenIdentityHandle(Path);
            FilePhysicalIdentity? identity =
                FilePublicationOwnedDelete.TryGetIdentity(current);
            if (current.IsInvalid || identity is null)
            {
                current.Dispose();
                throw new IOException(
                    $"The produced temporary file '{Path}' has no verifiable physical identity.");
            }

            if (_producedBound && identity != _identity)
            {
                current.Dispose();
                throw new IOException(
                    $"The produced temporary file '{Path}' changed after ownership was bound.");
            }

            _identityHandle?.Dispose();
            _identityHandle = current;
            _identity = identity;
        }
        _producedBound = true;
    }

    internal FileStream OpenBoundReadWrite() => OpenBound(
        FileAccess.ReadWrite,
        FileShare.Read);

    /// <summary>Reads only the physical file whose ownership was previously bound.</summary>
    public FileStream OpenBoundRead() => OpenBound(
        FileAccess.Read,
        FileShare.Read);

    internal void BindInspectAndVerify(
        Action<string, Stream>? inspect,
        Action<string>? verify)
    {
        BindProducedFile();
        if (inspect is not null)
        {
            using (Stream stream = OpenBoundReadWrite())
            {
                inspect(Path, stream);
                stream.Flush();
            }
            BindProducedFile();
        }
        if (verify is not null)
        {
            using (Stream stream = OpenBoundRead())
            {
                verify(Path);
            }
            BindProducedFile();
        }
    }

    internal void FlushBound()
    {
        using (FileStream stream = OpenBoundReadWrite())
        {
            stream.Flush(flushToDisk: true);
        }
        BindProducedFile();
    }

    internal FilePublicationSnapshot CaptureBoundSnapshot()
    {
        if (!_producedBound)
        {
            throw new InvalidOperationException(
                "The produced temporary file has not been bound.");
        }
        using (Stream stream = OpenBoundRead())
        {
            FilePublicationSnapshot snapshot =
                FilePublicationSnapshot.Capture(Path);
            if (OperatingSystem.IsWindows()
                && snapshot.PhysicalIdentity != _identity)
            {
                throw new IOException(
                    $"Temporary file '{Path}' changed before its verified snapshot was captured.");
            }
            return snapshot;
        }
    }

    private FileStream OpenBound(FileAccess access, FileShare share)
    {
        if (_disposed || _identityHandle is null && OperatingSystem.IsWindows())
        {
            throw new ObjectDisposedException(nameof(OwnedTemporaryFile));
        }

        var stream = new FileStream(
            Path,
            FileMode.Open,
            access,
            share,
            bufferSize: 4096,
            FileOptions.RandomAccess);
        if (!OperatingSystem.IsWindows())
        {
            return stream;
        }

        FilePhysicalIdentity? identity =
            FilePublicationOwnedDelete.TryGetIdentity(stream.SafeFileHandle);
        if (identity is null || identity != _identity)
        {
            stream.Dispose();
            throw new IOException(
                $"Temporary file '{Path}' changed before staged inspection.");
        }
        return stream;
    }

    /// <summary>Moves the bound file into another private location while retaining cleanup ownership.</summary>
    public void MoveTo(string destination)
    {
        BindProducedFile();
        string full = System.IO.Path.GetFullPath(destination);
        File.Move(Path, full);
        Path = full;
        BindProducedFile();
    }

    public void MarkPublished() => _published = true;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!_published && File.Exists(Path))
            {
                bool deleted = _identity is { } identity
                    ? FilePublicationOwnedDelete.TryDelete(Path, identity)
                    : TryDeletePortable();
                if (!deleted)
                {
                    Trace.TraceWarning(
                        "Temporary output cleanup preserved '{0}' because ownership changed.",
                        Path);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Temporary output cleanup failed for '{0}' ({1}).",
                Path,
                exception.GetType().Name);
        }
        finally
        {
            _identityHandle?.Dispose();
        }
    }

    private bool TryDeletePortable()
    {
        File.Delete(Path);
        return true;
    }
}
