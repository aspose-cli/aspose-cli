namespace Aspose.Cli.Sdk.IO;

/// <summary>Accounts extraction items and actual bytes, including streaming overruns.</summary>
internal sealed class ExtractionBudgetLedger
{
    private readonly int _maxItems;
    private readonly long _maxBytes;
    private int _items;

    public ExtractionBudgetLedger(int maxItems, long maxBytes)
    {
        _maxItems = maxItems;
        _maxBytes = maxBytes;
    }

    public long Bytes { get; private set; }

    public long ReserveFile(long sizeBytes)
    {
        long reserved = Math.Max(0, sizeBytes);
        _items++;
        Bytes += reserved;
        EnsureWithinBudget();
        return reserved;
    }

    public void ReserveDirectory()
    {
        _items++;
        EnsureWithinBudget();
    }

    public long WriteLimit(long reservedBytes) =>
        _maxBytes - (Bytes - reservedBytes);

    public void Reconcile(long reservedBytes, long actualBytes) =>
        Bytes = Bytes - reservedBytes + actualBytes;

    public Stream Bound(Stream stream, long limit, string name) =>
        new BudgetWriteStream(stream, limit, name);

    private void EnsureWithinBudget()
    {
        if (_items > _maxItems || Bytes > _maxBytes)
        {
            throw ExtractionPathValidator.Refused(
                $"extraction exceeds {_maxItems} items or {_maxBytes} bytes");
        }
    }

    private sealed class BudgetWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _limit;
        private readonly string _name;
        private long _length;

        public BudgetWriteStream(Stream inner, long limit, string name)
        {
            _inner = inner;
            _limit = limit;
            _name = name;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            Reserve(count);
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Reserve(buffer.Length);
            _inner.Write(buffer);
        }

        private void Reserve(int count)
        {
            if (count < 0 || _length > _limit - count)
            {
                throw ExtractionPathValidator.Refused(
                    $"entry '{_name}' exceeds the remaining {_limit}-byte output budget");
            }
            _length += count;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flush();
            }
            base.Dispose(disposing);
        }
    }
}
