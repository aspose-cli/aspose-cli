using Aspose.Cli.Host.Invocation;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>How the host turns Ctrl+C into cancellation.</summary>
public sealed class ConsoleInterruptTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void ConsoleInterrupt_KeepsTheProcessOnlyForTheFirstPress(int press, bool keeps) =>
        Assert.Equal(keeps, ConsoleInterrupt.KeepsProcess(press));

    [Fact]
    public void ConsoleInterrupt_Press_CancelsTheWorkAndCountsThePresses()
    {
        var scope = new ConsoleInterrupt.Scope();
        Assert.Equal(1, scope.Press());
        Assert.True(scope.Cancellation.IsCancellationRequested);
        Assert.Equal(2, scope.Press());
        scope.Cancellation.Dispose();
    }
}
