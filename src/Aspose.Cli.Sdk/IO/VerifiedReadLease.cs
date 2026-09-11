using Microsoft.Win32.SafeHandles;

namespace Aspose.Cli.Sdk.IO;

/// <summary>A read-only stream that owns the verified file and its pinned directory handles.</summary>
public sealed class VerifiedReadLease : Stream
{
    private readonly FileStream _stream;
    private readonly SafeFileHandle[] _directories;

    internal VerifiedReadLease(FileStream stream, SafeFileHandle[] directories)
    {
        _stream = stream;
        _directories = directories;
    }

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _stream.Length;
    public override long Position { get => _stream.Position; set => _stream.Position = value; }
    public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _stream.Read(buffer);
    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _stream.Dispose(); }
            finally
            {
                for (int index = _directories.Length - 1; index >= 0; index--)
                {
                    _directories[index].Dispose();
                }
            }
        }
        base.Dispose(disposing);
    }
}
