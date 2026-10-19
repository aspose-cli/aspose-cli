using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Stable identity of one service process. PID alone is never sufficient
/// because operating systems reuse it.
/// </summary>
internal sealed record LocalServiceProcessIdentity(
    int Pid,
    long StartTicksUtc,
    string Nonce)
{
    // A service whose process cannot be inspected or has exited is gone: a stale lock must not
    // block the next start.
    private static readonly ProcessLivenessPolicy Liveness = new()
    {
        WhenDenied = ProcessLiveness.Dead,
        WhenVanishing = ProcessLiveness.Dead,
        ChecksExit = true,
        StartTolerance = TimeSpan.FromSeconds(1),
    };

    public static LocalServiceProcessIdentity Current(string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        return new LocalServiceProcessIdentity(
            Environment.ProcessId,
            ProcessIdentity.CurrentStartUtcTicks,
            nonce);
    }

    public static bool IsLive(
        int version,
        int pid,
        long startTicksUtc,
        string nonce) =>
        version == 1
        && new LocalServiceProcessIdentity(
            pid,
            startTicksUtc,
            nonce)
        .MatchesLiveProcess();

    public bool MatchesLiveProcess() =>
        Pid > 0
        && !string.IsNullOrWhiteSpace(Nonce)
        && ProcessIdentity.IsLive(Pid, StartTicksUtc, Liveness);
}
