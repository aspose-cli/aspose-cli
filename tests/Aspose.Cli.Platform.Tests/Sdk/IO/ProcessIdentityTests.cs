using System.ComponentModel;
using Aspose.Cli.Sdk.IO;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

public sealed class ProcessIdentityTests
{
    private const long Started = 638_000_000_000_000_000;
    private const int Pid = 4242;

    private static readonly ProcessLivenessPolicy Exact = new()
    {
        WhenDenied = ProcessLiveness.Dead,
        WhenVanishing = ProcessLiveness.Dead,
        ChecksExit = false,
    };

    [Fact]
    public void ThisProcess_IsLiveUnderItsOwnStartTime()
    {
        Assert.True(ProcessIdentity.IsLive(Environment.ProcessId, ProcessIdentity.CurrentStartUtcTicks, Exact));
        Assert.False(ProcessIdentity.IsLive(Environment.ProcessId, ProcessIdentity.CurrentStartUtcTicks + 1, Exact));
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        Assert.Equal(ProcessIdentity.CurrentStartUtcTicks, ProcessIdentity.StartUtcTicksOf(self));
        Assert.True(ProcessIdentity.IsRunningAs(Environment.ProcessId, self.ProcessName.ToUpperInvariant(), ProcessLiveness.Dead));
        Assert.False(ProcessIdentity.IsRunningAs(Environment.ProcessId, "powershell-not-this", ProcessLiveness.Dead));
    }

    [Theory]
    [InlineData(ProcessLiveness.Alive)]
    [InlineData(ProcessLiveness.Dead)]
    public void AMissingProcess_IsDeadUnderEveryPolicy(ProcessLiveness unknown)
    {
        ProcessLivenessPolicy policy = Exact with { WhenDenied = unknown, WhenVanishing = unknown, UnknownStartMatches = true };
        Func<int, IProcessView> missing = static _ => throw new ArgumentException("No process.");

        Assert.False(ProcessIdentity.IsLive(Pid, Started, policy, missing));
        Assert.False(ProcessIdentity.IsLive(Pid, 0, policy, missing));
        Assert.False(ProcessIdentity.IsRunningAs(Pid, "powershell", unknown, missing));
    }

    [Theory]
    [InlineData(ProcessLiveness.Alive, true)]
    [InlineData(ProcessLiveness.Dead, false)]
    public void AProcessThisUserMayNotInspect_FollowsTheDeniedPolicy(ProcessLiveness denied, bool expected)
    {
        ProcessLivenessPolicy policy = Exact with { WhenDenied = denied, WhenVanishing = Opposite(denied) };

        Assert.Equal(expected, ProcessIdentity.IsLive(Pid, Started, policy, Throwing(new Win32Exception(5))));
        Assert.Equal(expected, ProcessIdentity.IsLive(Pid, Started, policy, Throwing(new NotSupportedException())));
        Assert.Equal(expected, ProcessIdentity.IsLive(Pid, Started, policy,
            static _ => throw new Win32Exception(5)));
        Assert.Equal(expected, ProcessIdentity.IsLive(Pid, Started, policy with { ChecksExit = true },
            Throwing(new Win32Exception(5), onExit: true)));
        Assert.Equal(expected, ProcessIdentity.IsRunningAs(Pid, "powershell", denied, Throwing(new Win32Exception(5))));
    }

    [Theory]
    [InlineData(ProcessLiveness.Alive, true)]
    [InlineData(ProcessLiveness.Dead, false)]
    public void AProcessThatEndsWhileInspected_FollowsTheVanishingPolicy(ProcessLiveness vanishing, bool expected)
    {
        ProcessLivenessPolicy policy = Exact with { WhenVanishing = vanishing, WhenDenied = Opposite(vanishing) };

        Assert.Equal(expected, ProcessIdentity.IsLive(Pid, Started, policy, Throwing(new InvalidOperationException())));
        Assert.Equal(expected, ProcessIdentity.IsLive(Pid, Started, policy with { ChecksExit = true },
            Throwing(new InvalidOperationException(), onExit: true)));
        Assert.Equal(expected, ProcessIdentity.IsRunningAs(Pid, "powershell", vanishing,
            Throwing(new InvalidOperationException(), onExit: true)));
    }

    [Fact]
    public void AnExitedProcess_IsDeadOnlyWhenThePolicyChecksExit()
    {
        var exited = new FakeProcess(Started) { HasExited = true };

        Assert.True(ProcessIdentity.IsLive(Pid, Started, Exact, _ => exited));
        Assert.False(ProcessIdentity.IsLive(Pid, Started, Exact with { ChecksExit = true }, _ => exited));
        Assert.False(ProcessIdentity.IsRunningAs(Pid, "powershell", ProcessLiveness.Alive, _ => exited));
        Assert.True(ProcessIdentity.IsRunningAs(Pid, "PowerShell", ProcessLiveness.Dead, _ => new FakeProcess(Started)));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(0, -1, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, TimeSpan.TicksPerSecond, true)]
    [InlineData(1, -TimeSpan.TicksPerSecond, true)]
    [InlineData(1, TimeSpan.TicksPerSecond + 1, false)]
    [InlineData(1, -TimeSpan.TicksPerSecond - 1, false)]
    public void TheRecordedStartTime_MatchesWithinTheTolerance(int toleranceSeconds, long offset, bool expected)
    {
        ProcessLivenessPolicy policy = Exact with { StartTolerance = TimeSpan.FromSeconds(toleranceSeconds) };

        Assert.Equal(expected, ProcessIdentity.IsLive(Pid, Started + offset, policy, _ => new FakeProcess(Started)));
    }

    [Fact]
    public void AnUnknownStartTime_MatchesAnyProcessOnlyWhenThePolicySaysSo()
    {
        // The start time is not read, so a process that ends meanwhile still matches.
        ProcessLivenessPolicy unknownMatches = Exact with { UnknownStartMatches = true };

        Assert.True(ProcessIdentity.IsLive(Pid, 0, unknownMatches, Throwing(new InvalidOperationException())));
        Assert.False(ProcessIdentity.IsLive(Pid, 0, Exact, _ => new FakeProcess(Started)));
        Assert.False(ProcessIdentity.IsLive(Pid, Started + 1, unknownMatches, _ => new FakeProcess(Started)));
    }

    private static ProcessLiveness Opposite(ProcessLiveness liveness) =>
        liveness == ProcessLiveness.Alive ? ProcessLiveness.Dead : ProcessLiveness.Alive;

    private static Func<int, IProcessView> Throwing(Exception exception, bool onExit = false) =>
        _ => new FakeProcess(Started) { Failure = exception, FailsOnExit = onExit };
}

/// <summary>A process view whose facts, or failures, a test sets.</summary>
internal sealed class FakeProcess(long startUtcTicks) : IProcessView
{
    public Exception? Failure { get; init; }

    public bool FailsOnExit { get; init; }

    public bool HasExited
    {
        get => FailsOnExit && Failure is not null ? throw Failure : field;
        init;
    }

    public long StartUtcTicks => Failure is not null && !FailsOnExit ? throw Failure : startUtcTicks;

    public string ProcessName => Failure is not null && !FailsOnExit ? throw Failure : "powershell";

    public void Dispose()
    {
    }
}
