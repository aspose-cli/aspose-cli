using System.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Accepts a ready marker only when it identifies the child just launched by
/// this process. A failed or timed-out handshake always reaps that child.
/// </summary>
internal static class LocalServiceStartHandshake
{
    public static TMarker WaitForReady<TMarker>(
        LocalServiceChild child,
        TimeSpan timeout,
        string operation,
        Func<TMarker?> readReady,
        Func<string, string> redactDiagnostic,
        Func<int, string, Exception>? processExited = null,
        OperationDeadline? deadline = null)
        where TMarker : class
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(readReady);
        ArgumentNullException.ThrowIfNull(redactDiagnostic);
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            while (watch.Elapsed < timeout)
            {
                deadline?.ThrowIfExpired("local-service-startup");
                if (child.Process.HasExited)
                {
                    string error = child.Process.StandardError
                        .ReadToEnd();
                    CliException? structured =
                        LocalServiceChildError.TryRead(
                            error,
                            child.Process.ExitCode);
                    if (structured is not null)
                    {
                        throw structured;
                    }
                    throw processExited?.Invoke(
                        child.Process.ExitCode,
                        error)
                        ?? CliErrors.OptionInvalid(
                            operation,
                            $"the background process exited with code "
                                + child.Process.ExitCode
                                + SafeSuffix(
                                    error,
                                    redactDiagnostic),
                            "Run the equivalent foreground command to inspect diagnostics.");
                }

                TMarker? marker = readReady();
                if (marker is not null)
                {
                    deadline?.ThrowIfExpired("local-service-startup");
                    return marker;
                }

                if (deadline is null) { Thread.Sleep(50); }
                else if (deadline.Token.WaitHandle.WaitOne(50)) { deadline.ThrowIfExpired("local-service-startup"); }
            }

            child.Owned.Stop();
            throw CliErrors.OperationTimeout(
                checked((int)Math.Ceiling(
                    timeout.TotalSeconds)),
                "local-service-startup");
        }
        catch
        {
            if (!child.Process.HasExited)
            {
                child.Owned.Stop();
            }

            throw;
        }
        finally
        {
            child.Owned.Dispose();
        }
    }

    public static bool Matches(
        int markerVersion,
        int markerPid,
        long markerStartTicksUtc,
        string markerNonce,
        LocalServiceChild child) =>
        markerVersion == 1
        && markerPid == child.Process.Id
        && LocalServiceProcessIdentity.StartMatches(
            child.StartTicksUtc,
            markerStartTicksUtc)
        && string.Equals(
            markerNonce,
            child.Nonce,
            StringComparison.Ordinal);

    private static string SafeSuffix(
        string error,
        Func<string, string> redactDiagnostic)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return string.Empty;
        }

        string oneLine = redactDiagnostic(
            error.Replace('\r', ' ').Replace('\n', ' ').Trim());
        return " (" + (oneLine.Length <= 240
            ? oneLine
            : oneLine[..240] + "...") + ")";
    }
}
