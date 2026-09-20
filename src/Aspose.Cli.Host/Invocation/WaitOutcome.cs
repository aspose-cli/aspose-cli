namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Why a hosted service stopped waiting. The command layer maps each
/// outcome to its exit path: an orderly shutdown for everything except
/// <see cref="DeadlineExpired"/>, which surfaces as the timeout error.
/// </summary>
public enum WaitOutcome
{
    /// <summary>The session ended on its own — it was disposed while waiting.</summary>
    Completed,

    /// <summary>The caller's cancellation token was signalled (e.g. Ctrl+C).</summary>
    CancelRequested,

    /// <summary>
    /// No clients were connected and no render activity happened for the
    /// configured idle window.
    /// </summary>
    IdleExpired,

    /// <summary>The overall wall-clock deadline elapsed.</summary>
    DeadlineExpired,
}
