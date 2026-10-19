using System.ComponentModel;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Updating;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.IO.Publication;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.IO;

/// <summary>
/// Each caller of <see cref="ProcessIdentity"/> keeps the liveness policy it had before the
/// checks were unified: recovery fails towards alive, a local service towards dead, preview
/// cleanup towards alive. The expected values are written out here, not read from the policy.
/// </summary>
public sealed class CallerLivenessPolicyTests
{
    private const long Started = 638_000_000_000_000_000;
    private const int Pid = 4242;

    public static TheoryData<string> Callers => ["publication owner", "publication creator", "local service", "preview owner"];

    private static (ProcessLivenessPolicy Policy, Expected Expected) Caller(string name) => name switch
    {
        "publication owner" => (AtomicPublicationRecovery.OwnerLiveness,
            new Expected(Denied: true, Vanishing: false, Exited: true, OneSecondOff: false, UnknownStart: true)),
        "publication creator" => (AtomicPublicationRecovery.CreatorLiveness,
            new Expected(Denied: true, Vanishing: false, Exited: true, OneSecondOff: false, UnknownStart: false)),
        "local service" => (LocalServiceProcessIdentity.Liveness,
            new Expected(Denied: false, Vanishing: false, Exited: false, OneSecondOff: true, UnknownStart: false)),
        "preview owner" => (ViewerStorage.OwnerLiveness,
            new Expected(Denied: true, Vanishing: true, Exited: false, OneSecondOff: false, UnknownStart: false)),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Fact]
    public void EachCallerStatesTodaysPolicy()
    {
        Assert.Equal(new ProcessLivenessPolicy
        {
            WhenDenied = ProcessLiveness.Alive, WhenVanishing = ProcessLiveness.Dead, ChecksExit = false,
            StartTolerance = TimeSpan.Zero, UnknownStartMatches = true,
        }, AtomicPublicationRecovery.OwnerLiveness);
        Assert.Equal(new ProcessLivenessPolicy
        {
            WhenDenied = ProcessLiveness.Alive, WhenVanishing = ProcessLiveness.Dead, ChecksExit = false,
            StartTolerance = TimeSpan.Zero, UnknownStartMatches = false,
        }, AtomicPublicationRecovery.CreatorLiveness);
        Assert.Equal(new ProcessLivenessPolicy
        {
            WhenDenied = ProcessLiveness.Dead, WhenVanishing = ProcessLiveness.Dead, ChecksExit = true,
            StartTolerance = TimeSpan.FromSeconds(1), UnknownStartMatches = false,
        }, LocalServiceProcessIdentity.Liveness);
        Assert.Equal(new ProcessLivenessPolicy
        {
            WhenDenied = ProcessLiveness.Alive, WhenVanishing = ProcessLiveness.Alive, ChecksExit = true,
            StartTolerance = TimeSpan.Zero, UnknownStartMatches = false,
        }, ViewerStorage.OwnerLiveness);
        Assert.Equal(ProcessLiveness.Dead, UpdateStatus.InstallerWhenUnknown);
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public void EachCaller_DecidesAnUninspectableProcessAsBefore(string caller)
    {
        (ProcessLivenessPolicy policy, Expected expected) = Caller(caller);

        Assert.Equal(expected.Denied, IsLive(policy, Started, new FakeProcess(Started) { Failure = new Win32Exception(5) }));
        Assert.Equal(expected.Vanishing, IsLive(policy, Started, new FakeProcess(Started) { Failure = new InvalidOperationException() }));
        Assert.Equal(expected.Exited, IsLive(policy, Started, new FakeProcess(Started) { HasExited = true }));
        Assert.Equal(expected.OneSecondOff, IsLive(policy, Started + TimeSpan.TicksPerSecond, new FakeProcess(Started)));
        Assert.Equal(expected.UnknownStart, IsLive(policy, 0, new FakeProcess(Started)));
        Assert.True(IsLive(policy, Started, new FakeProcess(Started)));
        Assert.False(ProcessIdentity.IsLive(Pid, Started, policy, static _ => throw new ArgumentException("No process.")));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(TimeSpan.TicksPerSecond, true)]
    [InlineData(-TimeSpan.TicksPerSecond, true)]
    [InlineData(TimeSpan.TicksPerSecond + 1, false)]
    [InlineData(-TimeSpan.TicksPerSecond - 1, false)]
    public void AServiceReadyMarker_MatchesItsLaunchWithinOneSecond(long offset, bool expected) =>
        Assert.Equal(expected, LocalServiceProcessIdentity.StartMatches(Started, Started + offset));

    [Fact]
    public void TheInstallerCheck_TreatsAnUninspectableOrExitedProcessAsEnded()
    {
        ProcessLiveness unknown = UpdateStatus.InstallerWhenUnknown;

        Assert.False(ProcessIdentity.IsRunningAs(Pid, "powershell", unknown, _ => new FakeProcess(Started) { Failure = new Win32Exception(5) }));
        Assert.False(ProcessIdentity.IsRunningAs(Pid, "powershell", unknown, _ => new FakeProcess(Started) { Failure = new InvalidOperationException() }));
        Assert.False(ProcessIdentity.IsRunningAs(Pid, "powershell", unknown, _ => new FakeProcess(Started) { HasExited = true }));
        Assert.True(ProcessIdentity.IsRunningAs(Pid, "powershell", unknown, _ => new FakeProcess(Started)));
    }

    private static bool IsLive(ProcessLivenessPolicy policy, long recorded, FakeProcess process) =>
        ProcessIdentity.IsLive(Pid, recorded, policy, _ => process);

    /// <summary>
    /// Whether the caller takes the process for alive when it may not be inspected, when it ends
    /// while inspected, when it has exited, when the recorded start is one second off, and when
    /// the recorded start is 0.
    /// </summary>
    private sealed record Expected(bool Denied, bool Vanishing, bool Exited, bool OneSecondOff, bool UnknownStart);
}
