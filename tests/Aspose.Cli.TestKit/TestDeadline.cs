namespace Aspose.Cli.TestKit;

/// <summary>
/// The wall-clock bounds a test puts on work that finishes in seconds, so that a loaded machine
/// never turns a passing test into a failure: a bound here only turns a hang into a named failure.
/// A test that checks a timeout itself states its own short value next to the behavior it pins.
/// </summary>
public static class TestDeadline
{
    /// <summary>
    /// The bound on a child process or wait that a test drives itself (a script, an installer):
    /// far above any loaded run, and below the 15-minute hang timeout of scripts/test.ps1,
    /// which reports a test that outlives it with a dump instead.
    /// </summary>
    public static readonly TimeSpan Hang = TimeSpan.FromMinutes(10);
}
