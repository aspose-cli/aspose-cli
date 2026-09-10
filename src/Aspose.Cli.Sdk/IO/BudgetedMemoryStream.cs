namespace Aspose.Cli.Sdk.IO;

/// <summary>Seekable memory stream that charges capacity growth before allocation.</summary>
public sealed class BudgetedMemoryStream : MemoryStream
{
    private readonly ResourceBudgetLedger _budgets;
    private readonly string _kind;
    private readonly string _phase;
    private bool _insideWrite;

    public BudgetedMemoryStream(
        ResourceBudgetLedger budgets,
        string kind,
        string phase)
    {
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        _kind = kind;
        _phase = phase;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count)
        {
            throw new ArgumentException("The buffer range is invalid.", nameof(count));
        }
        if (_insideWrite)
        {
            base.Write(buffer, offset, count);
            return;
        }
        Admit(checked(Position + count));
        _insideWrite = true;
        try
        {
            base.Write(buffer, offset, count);
        }
        finally
        {
            _insideWrite = false;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_insideWrite)
        {
            base.Write(buffer);
            return;
        }
        Admit(checked(Position + buffer.Length));
        _insideWrite = true;
        try
        {
            base.Write(buffer);
        }
        finally
        {
            _insideWrite = false;
        }
    }

    public override void WriteByte(byte value)
    {
        if (_insideWrite)
        {
            base.WriteByte(value);
            return;
        }
        Admit(checked(Position + 1));
        _insideWrite = true;
        try
        {
            base.WriteByte(value);
        }
        finally
        {
            _insideWrite = false;
        }
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Admit(value);
        base.SetLength(value);
    }

    private void Admit(long requiredLength)
    {
        if (requiredLength <= Capacity)
        {
            return;
        }
        long available = _budgets.Remaining(_kind);
        long requiredGrowth = requiredLength - Capacity;
        if (requiredGrowth > available)
        {
            _budgets.Consume(_kind, requiredGrowth, "bytes", _phase);
        }
        long geometric = Math.Max(256L, (long)Capacity * 2);
        long desired = Math.Max(
            requiredLength,
            Math.Min(checked(Capacity + available), geometric));
        long growth = desired - Capacity;
        _budgets.Consume(_kind, growth, "bytes", _phase);
        Capacity = checked((int)desired);
    }
}
