namespace Aspose.Cli.TestKit;

/// <summary>Outcome of one CLI invocation.</summary>
public sealed record CliResult(int ExitCode, string StdOut, string StdErr);
