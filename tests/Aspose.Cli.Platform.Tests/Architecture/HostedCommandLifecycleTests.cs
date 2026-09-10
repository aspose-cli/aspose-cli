using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Preview;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class HostedCommandLifecycleTests
{
    [Fact]
    public void Shutdown_InvokesOwnerExactlyOnce()
    {
        int shutdowns = 0;
        var lifecycle = Lifecycle(shutdown: () => shutdowns++);

        lifecycle.Shutdown();
        lifecycle.Shutdown();
        lifecycle.Shutdown();

        Assert.Equal(1, shutdowns);
    }

    [Fact]
    public void Shutdown_DoesNotRetryFailingOwner()
    {
        int shutdowns = 0;
        var lifecycle = Lifecycle(shutdown: () =>
        {
            shutdowns++;
            throw new InvalidOperationException("shutdown failed");
        });

        Assert.Throws<InvalidOperationException>(lifecycle.Shutdown);
        lifecycle.Shutdown();

        Assert.Equal(1, shutdowns);
    }

    [Fact]
    public void Wait_ForwardsDeadlineCancellationAndOutcome()
    {
        TimeSpan? observedDeadline = null;
        CancellationToken observedToken = default;
        using var cancellation = new CancellationTokenSource();
        var lifecycle = Lifecycle(wait: (deadline, token) =>
        {
            observedDeadline = deadline;
            observedToken = token;
            return WaitOutcome.IdleExpired;
        });

        WaitOutcome outcome = lifecycle.Wait(
            TimeSpan.FromSeconds(12),
            cancellation.Token);

        Assert.Equal(WaitOutcome.IdleExpired, outcome);
        Assert.Equal(TimeSpan.FromSeconds(12), observedDeadline);
        Assert.Equal(cancellation.Token, observedToken);
    }

    private static HostedCommandLifecycle Lifecycle(
        Func<TimeSpan?, CancellationToken, WaitOutcome>? wait = null,
        Action? shutdown = null) =>
        new(
            new TestResult(),
            once: false,
            TimeSpan.FromMinutes(1),
            wait ?? (static (_, _) => WaitOutcome.Completed),
            shutdown ?? (static () => { }));

    private sealed record TestResult()
        : ResultEnvelope("test/hosted-lifecycle", 1);
}
