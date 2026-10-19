using System.ComponentModel;
using System.Diagnostics;

namespace Aspose.Cli.Sdk.IO;

/// <summary>What a liveness check concludes about a process it cannot fully inspect.</summary>
public enum ProcessLiveness
{
    /// <summary>The process is taken to have ended.</summary>
    Dead,

    /// <summary>The process is taken to still run.</summary>
    Alive,
}

/// <summary>
/// How one caller decides whether a recorded process still runs. Callers differ on purpose: a
/// recovery that would roll back another process's work fails towards alive, a lock that would
/// otherwise block forever fails towards dead. Each caller states every choice.
/// </summary>
public readonly record struct ProcessLivenessPolicy
{
    /// <summary>The conclusion when the process exists but this user may not inspect it.</summary>
    public required ProcessLiveness WhenDenied { get; init; }

    /// <summary>The conclusion when the process went away while it was being inspected.</summary>
    public required ProcessLiveness WhenVanishing { get; init; }

    /// <summary>Whether a process that has exited, but whose id is still held open, counts as ended.</summary>
    public required bool ChecksExit { get; init; }

    /// <summary>The largest difference between the recorded and the observed start time that still matches.</summary>
    public TimeSpan StartTolerance { get; init; }

    /// <summary>Whether a recorded start time of 0, meaning unknown, matches any process with the id.</summary>
    public bool UnknownStartMatches { get; init; }
}

/// <summary>
/// The one owner of process identity: a process id is reused by the operating system, so a
/// process is identified by its id and its start time, read here and compared here.
/// </summary>
public static class ProcessIdentity
{
    /// <summary>The start time of this process, in UTC ticks.</summary>
    public static long CurrentStartUtcTicks { get; } = ReadCurrentStartUtcTicks();

    /// <summary>The start time of a process, in UTC ticks.</summary>
    public static long StartUtcTicksOf(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.StartTime.ToUniversalTime().Ticks;
    }

    /// <summary>Whether the process with this id and start time still runs, decided by the caller's policy.</summary>
    public static bool IsLive(int processId, long startUtcTicks, ProcessLivenessPolicy policy) =>
        IsLive(processId, startUtcTicks, policy, Open);

    /// <summary>
    /// Whether the process with this id still runs under this executable name. Without a start
    /// time a reused id can match another process with the same name.
    /// </summary>
    public static bool IsRunningAs(int processId, string processName, ProcessLiveness whenUnknown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        return IsRunningAs(processId, processName, whenUnknown, Open);
    }

    internal static bool IsLive(
        int processId,
        long startUtcTicks,
        ProcessLivenessPolicy policy,
        Func<int, IProcessView> open) =>
        Inspect(processId, policy, open, process =>
            (!policy.ChecksExit || !process.HasExited)
            && ((policy.UnknownStartMatches && startUtcTicks == 0)
                || StartMatches(process.StartUtcTicks, startUtcTicks, policy.StartTolerance)));

    /// <summary>Whether a recorded start time, in UTC ticks, is within the tolerance of an observed one.</summary>
    public static bool StartMatches(long observedUtcTicks, long recordedUtcTicks, TimeSpan tolerance) =>
        recordedUtcTicks >= observedUtcTicks - tolerance.Ticks && recordedUtcTicks <= observedUtcTicks + tolerance.Ticks;

    internal static bool IsRunningAs(
        int processId,
        string processName,
        ProcessLiveness whenUnknown,
        Func<int, IProcessView> open) =>
        Inspect(
            processId,
            new ProcessLivenessPolicy { WhenDenied = whenUnknown, WhenVanishing = whenUnknown, ChecksExit = true },
            open,
            process => !process.HasExited
                && string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

    private static bool Inspect(
        int processId,
        ProcessLivenessPolicy policy,
        Func<int, IProcessView> open,
        Func<IProcessView, bool> matches)
    {
        try
        {
            using IProcessView process = open(processId);
            return matches(process);
        }
        catch (ArgumentException)
        {
            // No process has this id.
            return false;
        }
        catch (InvalidOperationException)
        {
            return policy.WhenVanishing == ProcessLiveness.Alive;
        }
        catch (Exception exception) when (exception is Win32Exception or NotSupportedException)
        {
            return policy.WhenDenied == ProcessLiveness.Alive;
        }
    }

    private static long ReadCurrentStartUtcTicks()
    {
        using Process process = Process.GetCurrentProcess();
        return StartUtcTicksOf(process);
    }

    private static IProcessView Open(int processId) => new SystemProcess(Process.GetProcessById(processId));

    private sealed class SystemProcess(Process process) : IProcessView
    {
        public bool HasExited => process.HasExited;

        public long StartUtcTicks => StartUtcTicksOf(process);

        public string ProcessName => process.ProcessName;

        public void Dispose() => process.Dispose();
    }
}

/// <summary>The facts about one open process that identity and liveness read; a test supplies its own.</summary>
internal interface IProcessView : IDisposable
{
    bool HasExited { get; }

    long StartUtcTicks { get; }

    string ProcessName { get; }
}
