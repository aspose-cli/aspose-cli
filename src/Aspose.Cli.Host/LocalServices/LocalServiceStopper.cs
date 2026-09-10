using System.Diagnostics;
using System.Net.Sockets;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Requests and confirms shutdown of an identity-checked service.</summary>
internal static class LocalServiceStopper
{
    public static bool TryStop(
        LocalServiceProcessIdentity identity,
        TimeSpan timeout,
        string operation,
        Action requestStop)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(requestStop);
        try
        {
            requestStop();
            WaitForExit(identity, timeout, operation);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or TimeoutException
                or InvalidDataException
                or SocketException)
        {
            return !identity.MatchesLiveProcess();
        }
    }

    private static void WaitForExit(
        LocalServiceProcessIdentity identity,
        TimeSpan timeout,
        string operation)
    {
        if (!identity.MatchesLiveProcess())
        {
            return;
        }

        try
        {
            using Process process =
                Process.GetProcessById(identity.Pid);
            if (!process.WaitForExit(
                    checked((int)Math.Ceiling(
                        timeout.TotalMilliseconds))))
            {
                throw CliErrors.OperationTimeout(
                    checked((int)Math.Ceiling(
                        timeout.TotalSeconds)),
                    operation);
            }
            if (process.ExitCode != 0)
            {
                throw CliErrors.OptionInvalid(
                    operation,
                    $"the service exited with code {process.ExitCode} during shutdown",
                    "The protected marker remains available for status and recovery.");
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            // Disappearance after identity validation is a successful stop.
        }
    }
}
