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
/// token and keeps the process; a later press does what <see cref="SecondPress"/> says. A press
/// while no work runs ends the process, as it would without a handler.
/// </summary>
/// <remarks>
/// The handler is registered once and never removed. Removing the last <c>CancelKeyPress</c>
/// handler unregisters the console control handler, and .NET does that under the lock its
/// control-handler thread takes, so a second press arriving while the work ends deadlocked the
/// process. Cancellation callbacks run on the thread pool, not on the control-handler thread.
/// </remarks>
internal static class ConsoleInterrupt
{
    private static readonly object Gate = new();
    private static readonly List<Scope> Active = [];
    private static bool _registered;

    public static T Run<T>(Func<CancellationToken, T> work, SecondPress secondPress)
    {
        ArgumentNullException.ThrowIfNull(work);
        var scope = new Scope(secondPress);
        lock (Gate)
        {
            Active.Add(scope);
            if (!_registered)
            {
                Console.CancelKeyPress += static (_, e) => e.Cancel = OnPress();
                _registered = true;
            }
        }
        try
        {
            return work(scope.Cancellation.Token);
        }
        finally
        {
            lock (Gate)
            {
                Active.Remove(scope);
                scope.Cancellation.Dispose();
            }
        }
    }

    /// <summary>Whether a press keeps the process alive: the first always does.</summary>
    internal static bool KeepsProcess(SecondPress secondPress, int press) =>
        press <= 1 || secondPress == SecondPress.Ignore;

    /// <summary>Counts one press against every running work, cancels it, and says whether the process stays.</summary>
    private static bool OnPress()
    {
        lock (Gate)
        {
            bool keeps = Active.Count > 0;
            foreach (Scope scope in Active)
            {
                keeps &= KeepsProcess(scope.SecondPress, scope.Press());
            }
            return keeps;
        }
    }

    internal sealed class Scope(SecondPress secondPress)
    {
        private int _presses;

        public SecondPress SecondPress { get; } = secondPress;

        public CancellationTokenSource Cancellation { get; } = new();

        /// <summary>Counts one press, requests cancellation, and returns which press it was.</summary>
        public int Press()
        {
            _ = Cancellation.CancelAsync();
            return ++_presses;
        }
    }
}
