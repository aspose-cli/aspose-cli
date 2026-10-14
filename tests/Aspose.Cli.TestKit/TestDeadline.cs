using System.Globalization;

namespace Aspose.Cli.TestKit;

/// <summary>
/// The wall-clock bounds a test puts on work that finishes in seconds, so that a loaded machine
/// never turns a passing test into a failure: a bound here only turns a hang into a named failure.
/// A test that checks a timeout itself states its own short value next to the behavior it pins.
/// A wait around a supervised command uses <see cref="Hang"/>, which outlasts <see cref="CliTimeoutSeconds"/>.
/// </summary>
public static class TestDeadline
{
    /// <summary>
    /// The timeout a test gives a command run under the timeout supervisor (<c>--timeout</c>, or
    /// the MCP <c>timeoutSeconds</c>): far above any loaded run, and below the 120-second bound of
    /// <see cref="CliProcess"/>, so a hang still ends as the CLI's own timeout error.
    /// </summary>
    public const int CliTimeoutSeconds = 100;

    /// <summary><see cref="CliTimeoutSeconds"/> as the value of <c>--timeout</c>.</summary>
    public static readonly string CliTimeout = CliTimeoutSeconds.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The bound on a child process or wait that a test drives itself (a script, an installer, a
    /// request to a server that runs a supervised command): far above any loaded run and above
    /// <see cref="CliTimeoutSeconds"/>, and below the 15-minute hang timeout of scripts/test.ps1,
    /// which reports a test that outlives it with a dump instead.
    /// </summary>
    public static readonly TimeSpan Hang = TimeSpan.FromMinutes(10);
}
