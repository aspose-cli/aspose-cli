using System.Diagnostics;

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
    private static readonly long StartToleranceTicks =
        TimeSpan.FromSeconds(1).Ticks;

    public static LocalServiceProcessIdentity Current(string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        using Process process = Process.GetCurrentProcess();
        return new LocalServiceProcessIdentity(
            Environment.ProcessId,
            process.StartTime.ToUniversalTime().Ticks,
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

    public bool MatchesLiveProcess()
    {
        if (Pid <= 0 || string.IsNullOrWhiteSpace(Nonce))
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById(Pid);
            long observed = process.StartTime.ToUniversalTime().Ticks;
            return !process.HasExited
                && StartTicksUtc >= observed - StartToleranceTicks
                && StartTicksUtc <= observed + StartToleranceTicks;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
