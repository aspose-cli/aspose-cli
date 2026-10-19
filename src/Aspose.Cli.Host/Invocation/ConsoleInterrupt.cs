namespace Aspose.Cli.Host.Invocation;

/// <summary>What a second Ctrl+C does while the first one's cancellation is still running.</summary>
internal enum SecondPress
{
    /// <summary>Ignored: the work must reach its own end, such as a shutdown or a committed handoff.</summary>
    Ignore,

    /// <summary>Ends the process at once, as the user asked twice.</summary>
    EndProcess,
}

/// <summary>
/// The one way the host turns Ctrl+C into cancellation: the first press cancels the work's
/// token and keeps the process; a later press does what <see cref="SecondPress"/> says.
/// </summary>
internal static class ConsoleInterrupt
{
    public static T Run<T>(Func<CancellationToken, T> work, SecondPress secondPress)
    {
        ArgumentNullException.ThrowIfNull(work);
        using var cancellation = new CancellationTokenSource();
        int presses = 0;
        ConsoleCancelEventHandler onCancel = (_, e) =>
            e.Cancel = KeepsProcess(secondPress, Press(cancellation, ref presses));
        Console.CancelKeyPress += onCancel;
        try
        {
            return work(cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    /// <summary>Whether a press keeps the process alive: the first always does.</summary>
    internal static bool KeepsProcess(SecondPress secondPress, int press) =>
        press <= 1 || secondPress == SecondPress.Ignore;

    /// <summary>Counts one press, cancels the work and returns which press it was.</summary>
    internal static int Press(CancellationTokenSource cancellation, ref int presses)
    {
        int press = Interlocked.Increment(ref presses);
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The work already ended; the press only waits for the process to exit.
        }
        return press;
    }
}
