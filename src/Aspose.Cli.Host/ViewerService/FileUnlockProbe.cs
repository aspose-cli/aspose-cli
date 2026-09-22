
namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// Polls a file until it can be opened for shared reading. Right after a
/// change event the writer often still holds the file (Excel keeps it open
/// for a moment after the replace dance); probing with exponential backoff
/// bridges the gap between the event and readability. The probe is advisory:
/// <c>false</c> means "still not readable within the budget", and the caller
/// decides whether to attempt the operation anyway.
/// </summary>
internal static class FileUnlockProbe
{
    /// <summary>
    /// Returns true as soon as <paramref name="path"/> opens for shared
    /// reading, probing up to <paramref name="attempts"/> times with delays of
    /// <paramref name="initialDelay"/> × 2^n between attempts. A missing file
    /// counts as unreadable.
    /// </summary>
    /// <param name="path">The file to probe.</param>
    /// <param name="attempts">Maximum number of open attempts; at least 1.</param>
    /// <param name="initialDelay">Delay after the first failed attempt; doubles each retry.</param>
    public static bool WaitReadable(string path, int attempts, TimeSpan initialDelay, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDelay, TimeSpan.Zero);

        TimeSpan delay = initialDelay;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // The most permissive share mode: writers holding read/write
                // shares or a pending delete do not fail the probe — only an
                // exclusive lock (or a missing file) does.
                using var probe = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == attempts)
                {
                    return false;
                }

                if (cancellationToken.WaitHandle.WaitOne(delay)) { cancellationToken.ThrowIfCancellationRequested(); }
                delay += delay;
            }
        }

        return false; // Unreachable; the last failed attempt returns above.
    }
}
