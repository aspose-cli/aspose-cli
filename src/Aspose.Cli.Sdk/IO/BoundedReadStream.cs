namespace Aspose.Cli.Sdk.IO;

/// <summary>Read-only stream that charges every byte to one shared ledger.</summary>
public sealed class BoundedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly ResourceBudgetLedger _budgets;
    private readonly string _kind;
    private readonly string _phase;
    private readonly bool _leaveOpen;
    private bool _innerDisposed;

    public BoundedReadStream(
        Stream inner,
        ResourceBudgetLedger budgets,
        string kind,
        string phase,
        bool leaveOpen = false)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        _kind = kind;
        _phase = phase;
        _leaveOpen = leaveOpen;
        if (!inner.CanRead)
        {
            throw new ArgumentException("The wrapped stream must be readable.", nameof(inner));
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        Charge(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = _inner.Read(buffer);
        Charge(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(
            buffer,
            cancellationToken).ConfigureAwait(false);
        Charge(read);
        return read;
    }

    public override int ReadByte()
    {
        int value = _inner.ReadByte();
        if (value >= 0)
        {
            Charge(1);
        }
        return value;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        _inner.Seek(offset, origin);

    public override void Flush() =>
        throw new NotSupportedException("The stream is read-only.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("The stream is read-only.");

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is read-only.");

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen && !_innerDisposed)
        {
            _innerDisposed = true;
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_leaveOpen && !_innerDisposed)
        {
            _innerDisposed = true;
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        // The base runs Dispose(true), which now leaves the inner stream alone.
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private void Charge(int read)
    {
        if (read > 0)
        {
            _budgets.Consume(_kind, read, "bytes", _phase);
        }
    }
}
