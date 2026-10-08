
namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Renames a file the CLI owns while another process briefly holds it open.
/// On Windows an antivirus scanner or search indexer that opens a freshly written file
/// without delete sharing makes a rename fail with an access or sharing violation. A
/// same-volume rename either happens completely or not at all, so repeating it is
/// idempotent. The wait is bounded and never outlives the caller's deadline.
/// </summary>
/// <remarks>
/// Only renames of files the CLI itself writes, such as staging, manifest and state files,
/// use this. A user-visible target swap reports a lock instead of waiting it out: a lock on a user document usually means an application
/// holds it, and a replace that fails part-way is recovered, not repeated.
/// </remarks>
public static class AtomicFileRename
{
    internal static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(2);
    private const int InitialDelayMilliseconds = 10;
    private const int MaximumDelayMilliseconds = 250;
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;

    /// <summary>Renames <paramref name="source"/> to <paramref name="destination"/>, retrying a transient lock within the bounded wait.</summary>
    public static void Move(string source, string destination, bool overwrite, OperationDeadline? deadline = null)
    {
        long stopAt = Environment.TickCount64 + (long)MaximumWait.TotalMilliseconds;
        for (int delay = InitialDelayMilliseconds; ; delay = Math.Min(delay * 2, MaximumDelayMilliseconds))
        {
            try
            {
                File.Move(source, destination, overwrite);
                return;
            }
            catch (Exception error) when (IsTransientLock(error) && CanWait(delay, stopAt, deadline) && File.Exists(source))
            {
                // The rename did not happen; the source is still ours to rename.
            }
            if (deadline is null) { Thread.Sleep(delay); }
            else { deadline.Token.WaitHandle.WaitOne(delay); }
        }
    }

    private static bool IsTransientLock(Exception error) =>
        OperatingSystem.IsWindows()
        && (error is UnauthorizedAccessException
            || error is IOException io && (io.HResult & 0xFFFF) is ErrorSharingViolation or ErrorLockViolation);

    private static bool CanWait(int delay, long stopAt, OperationDeadline? deadline) =>
        Environment.TickCount64 + delay < stopAt
        && (deadline is null
            || !deadline.Token.IsCancellationRequested
                && (deadline.Remaining is not { } remaining || remaining.TotalMilliseconds > delay));
}
