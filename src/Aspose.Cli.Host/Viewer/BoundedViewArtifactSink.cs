using System.Runtime.ExceptionServices;
using System.Text;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// Materializes one immutable preview view without exposing its owned
/// directory. Every artifact is bounded while bytes are written, so a product
/// renderer cannot create an oversized transient publication before the
/// manifest validation boundary runs.
/// </summary>
internal sealed class BoundedViewArtifactSink : IViewArtifactSink
{
    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly object _gate = new();
    private readonly string _root;
    private readonly LocalServiceResourceLimits _limits;
    private readonly HashSet<string> _paths = new(
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
    private ExceptionDispatchInfo? _failure;
    private long _totalBytes;

    public BoundedViewArtifactSink(
        string root,
        LocalServiceResourceLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        PrivateUserStorage.ValidateDirectory(_root);
    }

    public void Write(
        string relativePath,
        Action<Stream> contentWriter)
    {
        ArgumentNullException.ThrowIfNull(contentWriter);
        lock (_gate)
        {
            ThrowIfFailed();
            try
            {
                WriteCore(relativePath, contentWriter);
            }
            catch (Exception exception)
            {
                _failure = ExceptionDispatchInfo.Capture(exception);
                throw;
            }
        }
    }

    public void WriteText(
        string relativePath,
        string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Write(relativePath, stream =>
        {
            using var writer = new StreamWriter(
                stream,
                Utf8,
                bufferSize: 4096,
                leaveOpen: true);
            writer.Write(content);
        });
    }

    /// <summary>
    /// Fails a publication whose renderer caught a sink exception. Product
    /// code may add context, but it cannot turn a breached hard limit into a
    /// successful view.
    /// </summary>
    public void EnsureComplete()
    {
        lock (_gate)
        {
            ThrowIfFailed();
        }
    }

    private void WriteCore(
        string relativePath,
        Action<Stream> contentWriter)
    {
        string normalized = ViewBundleManifest.NormalizeRelative(
            relativePath);
        if (!_paths.Add(normalized))
        {
            throw new InvalidDataException(
                $"Preview artifact '{normalized}' was written more than once.");
        }
        if (_paths.Count > _limits.MaximumSnapshotFiles)
        {
            throw CliErrors.PreviewBudgetExceeded(
                "view artifact files",
                _paths.Count,
                _limits.MaximumSnapshotFiles);
        }

        RegisterDirectories(normalized);

        long remaining = _limits.MaximumSnapshotBytes - _totalBytes;
        long fileLimit = Math.Min(
            _limits.MaximumSnapshotFileBytes,
            remaining);
        if (fileLimit < 0)
        {
            throw CliErrors.PreviewBudgetExceeded(
                "view snapshot bytes",
                _totalBytes,
                _limits.MaximumSnapshotBytes);
        }

        string fullPath = Path.Combine(
            _root,
            normalized.Replace('/', Path.DirectorySeparatorChar));
        PrivateUserStorage.EnsureDirectory(
            Path.GetDirectoryName(fullPath)!);
        try
        {
            using FileStream file = PrivateUserStorage.CreateFile(fullPath);
            using var bounded = new BoundedWriteStream(file, fileLimit);
            contentWriter(bounded);
            bounded.Flush();
            long bytes = file.Length;
            _totalBytes = checked(_totalBytes + bytes);
        }
        catch
        {
            LocalFileCleanup.DeleteFile(fullPath);
            throw;
        }
    }

    private void ThrowIfFailed() => _failure?.Throw();

    private void RegisterDirectories(string relativePath)
    {
        string[] segments = relativePath.Split('/');
        for (int length = 1; length < segments.Length; length++)
        {
            string directory = string.Join('/', segments, 0, length);
            if (_directories.Add(directory)
                && _directories.Count > _limits.MaximumSnapshotFiles)
            {
                throw CliErrors.PreviewBudgetExceeded(
                    "view artifact directories",
                    _directories.Count,
                    _limits.MaximumSnapshotFiles);
            }
        }
    }

    /// <summary>
    /// Seekable wrapper required by image and document encoders. The bound is
    /// the resulting file length, not cumulative write calls, so legitimate
    /// header rewrites do not consume the budget twice.
    /// </summary>
    private sealed class BoundedWriteStream(
        Stream inner,
        long maximumLength) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set
            {
                EnsureLength(value);
                inner.Position = value;
            }
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            throw AsyncWritesNotSupported();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(Position + offset),
                SeekOrigin.End => checked(Length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            EnsureLength(target);
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            EnsureLength(value);
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (offset > buffer.Length - count)
            {
                throw new ArgumentException(
                    "The buffer range is outside the supplied array.",
                    nameof(count));
            }
            EnsureWrite(count);
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWrite(buffer.Length);
            inner.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureWrite(1);
            inner.WriteByte(value);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            throw AsyncWritesNotSupported();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            throw AsyncWritesNotSupported();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Flush();
            }
            base.Dispose(disposing);
        }

        private void EnsureWrite(int count) =>
            EnsureLength(checked(Position + count));

        private void EnsureLength(long length)
        {
            if (length < 0)
            {
                throw new IOException("A preview artifact cannot seek before its start.");
            }
            if (length > maximumLength)
            {
                throw CliErrors.PreviewBudgetExceeded(
                    "view artifact bytes",
                    length,
                    maximumLength);
            }
        }

        private static NotSupportedException AsyncWritesNotSupported() => new(
            "Preview artifacts must be written synchronously before the publication callback returns.");
    }
}
