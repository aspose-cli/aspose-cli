namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Stable public identifiers for unexpected Host failure boundaries. These
/// identify where a failure escaped without disclosing exception messages,
/// stack traces, paths, or secret-bearing values.
/// </summary>
internal static class HostDiagnosticIds
{
    public const string CommandUnhandled = "HOST-COMMAND-0001";
    public const string ProcessUnhandled = "HOST-PROCESS-0001";
    public const string ReportingFailure = "HOST-PROCESS-0002";
    public const string BootstrapFailure = "HOST-PROCESS-0003";
    public const string WorkerUnhandled = "HOST-WORKER-0001";
}
