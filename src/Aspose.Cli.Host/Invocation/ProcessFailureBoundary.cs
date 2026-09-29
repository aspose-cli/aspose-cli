using System.Globalization;
using Aspose.Cli.Host.Output;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Final process boundary for failures that occur outside a command execution
/// pipeline, including background App startup and late worker-thread crashes.
/// </summary>
internal static class ProcessFailureBoundary
{
    private const string BootstrapFailureEnvelope = $$"""
        {
          "schema": "{{CommonSchemaIds.Error}}",
          "schemaVersion": 2,
          "error": {
            "code": "INTERNAL_ERROR",
            "message": "The CLI could not complete process startup.",
            "details": {
              "diagnosticId": "HOST-PROCESS-0003"
            },
            "hint": "Retry once; if startup still fails, report the diagnostic id and command without including secrets."
          }
        }
        """;
    private static int _unhandledHandlerInstalled;

    private static (OutputMode Output, bool Quiet) ResolveErrorOutput(
        HostContext host, IReadOnlyList<string> args)
    {
        try { return host.Parser.ResolveErrorOutput(args); }
        catch
        {
            // Only bootstrap error presentation may use the emergency formatter.
            return GlobalOptions.ResolveForProcessFailure(args);
        }
    }

    public static int Run(
        HostContext host,
        string[] args,
        Func<string[], int> execute)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(execute);

        WindowsProcessErrorMode.SuppressNativeErrorUi();
        InstallUnhandledExceptionLog();
        return Execute(
            () => execute(args),
            exception => ReportCaughtFailure(host, args, exception));
    }

    internal static int Execute(
        Func<int> execute,
        Func<Exception, int> report)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            return execute();
        }
        catch (Exception exception)
        {
            return report(exception);
        }
    }

    internal static int RenderBootstrapFailure(
        Exception exception,
        TextWriter errorOutput)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(errorOutput);
        ProcessFailureLog.Write("bootstrap", exception);
        try
        {
            errorOutput.WriteLine(BootstrapFailureEnvelope);
            errorOutput.Flush();
        }
        catch (Exception consoleFailure) when (
            consoleFailure is IOException
                or ObjectDisposedException
                or InvalidOperationException)
        {
            ProcessFailureLog.Write("bootstrap-console", consoleFailure);
        }

        return (int)ExitCode.Internal;
    }

    private static int ReportCaughtFailure(
        HostContext host,
        string[] args,
        Exception exception)
    {
        ProcessFailureLog.Write("caught", exception);
        return RenderCaughtFailure(host, args, exception, Console.Error);
    }

    internal static int RenderCaughtFailure(
        HostContext host,
        IReadOnlyList<string> args,
        Exception exception,
        TextWriter errorOutput)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(errorOutput);
        CliException error = exception as CliException
            ?? CliErrors.Internal(
                exception,
                HostDiagnosticIds.ProcessUnhandled);
        try
        {
            (OutputMode output, bool quiet) =
                ResolveErrorOutput(host, args);
            IOutputWriter writer = OutputWriterFactory.Create(
                output,
                quiet,
                host.Catalog,
                host.ContractJson.Serializer,
                error: errorOutput);
            writer.WriteError(error.ToEnvelope());
            errorOutput.Flush();
        }
        catch (Exception reportingFailure)
        {
            ProcessFailureLog.Write("reporting", reportingFailure);
            try
            {
                errorOutput.WriteLine(
                    $"error INTERNAL_ERROR: Unexpected internal error. " +
                    $"Diagnostic id: {HostDiagnosticIds.ReportingFailure}.");
            }
            catch (Exception consoleFailure) when (
                consoleFailure is IOException
                    or ObjectDisposedException
                    or InvalidOperationException)
            {
                ProcessFailureLog.Write("console", consoleFailure);
            }
        }

        return (int)error.ExitCode;
    }

    private static void InstallUnhandledExceptionLog()
    {
        if (Interlocked.Exchange(
                ref _unhandledHandlerInstalled,
                1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException +=
            static (_, eventArgs) =>
            {
                if (eventArgs.ExceptionObject is Exception exception)
                {
                    ProcessFailureLog.Write("unhandled", exception);
                }
                else
                {
                    ProcessFailureLog.Write(
                        "unhandled",
                        new InvalidOperationException(
                            "The runtime reported a non-Exception failure."));
                }
            };
    }

}

/// <summary>Best-effort bounded diagnostics for process and control-request failures.</summary>
internal static class ProcessFailureLog
{
    private const long MaximumBytes = 512 * 1024;
    private const int MaximumEntryCharacters = 32 * 1024;
    private static readonly object Gate = new();

    public static void Write(
        string phase,
        Exception exception)
    {
        try
        {
            string directory =
                Aspose.Cli.Sdk.Configuration.ConfigurationPaths.EnsureUserDirectory();
            string path = Path.Combine(
                directory,
                "process-errors.log");
            string details = Format(exception);
            if (details.Length > MaximumEntryCharacters)
            {
                details = details[..MaximumEntryCharacters] + "...";
            }

            lock (Gate)
            {
                string timestamp = DateTimeOffset.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture);
                Aspose.Cli.Host.LocalServices.UserTextFile.AppendLine(
                    path,
                    $"{timestamp} pid={Environment.ProcessId} " +
                    $"phase={phase} hresult=0x{exception.HResult:X8} " +
                    details,
                    MaximumBytes);
            }
        }
        catch (Exception)
        {
        }
    }

    internal static string Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var summaries = new List<string>();
        for (Exception? current = exception;
             current is not null && summaries.Count < 8;
             current = current.InnerException)
        {
            string target = current.TargetSite is null
                ? "(unknown)"
                : $"{current.TargetSite.DeclaringType?.FullName}.{current.TargetSite.Name}";
            string stack = current.StackTrace?
                .ReplaceLineEndings(" ")
                .Trim() ?? "(unavailable)";
            summaries.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"type={current.GetType().FullName} " +
                    $"hresult=0x{current.HResult:X8} target={target} stack={stack}"));
        }

        string details = DiagnosticRedactor.Redact(
            string.Join(" inner=", summaries));
        return details.Length <= MaximumEntryCharacters
            ? details
            : details[..MaximumEntryCharacters] + "...";
    }
}
