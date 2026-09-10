using System.Diagnostics;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Completes an owned resource teardown now when quiescent, or on one bounded
/// background thread after active work leaves the resource boundary.
/// </summary>
internal static class DeferredResourceCleanup
{
    public static void CompleteOrDefer(
        bool stopped,
        Action waitUntilStopped,
        Action cleanup,
        string threadName,
        string component)
    {
        ArgumentNullException.ThrowIfNull(waitUntilStopped);
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentException.ThrowIfNullOrWhiteSpace(threadName);
        ArgumentException.ThrowIfNullOrWhiteSpace(component);
        if (stopped)
        {
            cleanup();
            return;
        }

        var thread = new Thread(() =>
        {
            try
            {
                waitUntilStopped();
                cleanup();
            }
            catch (Exception exception)
            {
                Trace.TraceWarning(
                    "Deferred cleanup failed for {0}: {1}",
                    component,
                    exception.GetType().Name);
            }
        })
        {
            IsBackground = true,
            Name = threadName,
        };
        thread.Start();
    }
}
