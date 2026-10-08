using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class BoundedReadStreamTests
{
    [Fact]
    public void DisposeTwice_DisposesOwnedInnerStreamOnce()
    {
        using var deadline = OperationDeadline.Start(null);
        var inner = new CountingStream();
        var stream = Wrap(inner, deadline, leaveOpen: false);

        stream.Dispose();
        stream.Dispose();

        Assert.Equal(1, inner.SyncDisposals);
        Assert.Equal(0, inner.AsyncDisposals);
    }

    [Fact]
    public async Task DisposeAsyncTwice_DisposesOwnedInnerStreamOnceAsynchronously()
    {
        using var deadline = OperationDeadline.Start(null);
        var inner = new CountingStream();
        var stream = Wrap(inner, deadline, leaveOpen: false);

        await stream.DisposeAsync();
        await stream.DisposeAsync();

        Assert.Equal(0, inner.SyncDisposals);
        Assert.Equal(1, inner.AsyncDisposals);
    }

    [Fact]
    public async Task DisposeThenDisposeAsync_DisposesOwnedInnerStreamOnce()
    {
        using var deadline = OperationDeadline.Start(null);
        var inner = new CountingStream();
        var stream = Wrap(inner, deadline, leaveOpen: false);

        stream.Dispose();
        await stream.DisposeAsync();

        Assert.Equal(1, inner.SyncDisposals);
        Assert.Equal(0, inner.AsyncDisposals);
    }

    [Fact]
    public async Task DisposeAsyncThenDispose_DisposesOwnedInnerStreamOnce()
    {
        using var deadline = OperationDeadline.Start(null);
        var inner = new CountingStream();
        var stream = Wrap(inner, deadline, leaveOpen: false);

        await stream.DisposeAsync();
        stream.Dispose();

        Assert.Equal(0, inner.SyncDisposals);
        Assert.Equal(1, inner.AsyncDisposals);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task LeaveOpen_NeverDisposesInnerStream(bool firstAsync, bool secondAsync)
    {
        using var deadline = OperationDeadline.Start(null);
        var inner = new CountingStream();
        var stream = Wrap(inner, deadline, leaveOpen: true);

        await DisposeAsync(stream, firstAsync);
        await DisposeAsync(stream, secondAsync);

        Assert.Equal(0, inner.SyncDisposals);
        Assert.Equal(0, inner.AsyncDisposals);
        Assert.Equal(1, inner.ReadByte());
    }

    private static BoundedReadStream Wrap(CountingStream inner, OperationDeadline deadline, bool leaveOpen) =>
        new(inner, new ResourceBudgetLedger(deadline), ResourceBudgetKinds.InputBytes, "test", leaveOpen);

    private static async Task DisposeAsync(Stream stream, bool useAsync)
    {
        if (useAsync)
        {
            await stream.DisposeAsync();
        }
        else
        {
            stream.Dispose();
        }
    }

    /// <summary>Counts sync and async disposals separately; the async path never falls back to Dispose.</summary>
    private sealed class CountingStream() : MemoryStream([1, 2, 3])
    {
        public int SyncDisposals { get; private set; }
        public int AsyncDisposals { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SyncDisposals++;
            }
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            return ValueTask.CompletedTask;
        }
    }
}
