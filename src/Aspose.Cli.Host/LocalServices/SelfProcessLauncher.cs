using System.Diagnostics;
using System.Reflection;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.LocalServices;

internal sealed record LocalServiceChild(
    Process Process,
    string Nonce,
    long StartTicksUtc);

/// <summary>Launches this executable as a silent background child.</summary>
internal static class SelfProcessLauncher
{
    public static ProcessStartInfo CreateBackground(
        string optionName,
        string missingExecutableHint)
    {
        string? processPath = Environment.ProcessPath;
        if (processPath is null)
        {
            throw CliErrors.OptionInvalid(
                optionName,
                "the current executable path is unavailable",
                missingExecutableHint);
        }

        var start = new ProcessStartInfo(processPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly is null)
            {
                throw CliErrors.OptionInvalid(
                    optionName,
                    "the managed entry assembly is unavailable",
                    missingExecutableHint);
            }

            start.ArgumentList.Add(ResolveManagedEntryAssemblyPath(
                entryAssembly,
                optionName,
                missingExecutableHint));
        }
        if (OperatingSystem.IsWindows())
        {
            start.WindowStyle = ProcessWindowStyle.Hidden;
        }

        return start;
    }

    internal static string ResolveManagedEntryAssemblyPath(
        Assembly entryAssembly,
        string optionName,
        string missingExecutableHint)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        string path = entryAssembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw CliErrors.OptionInvalid(
                optionName,
                "the managed entry assembly path is unavailable",
                missingExecutableHint);
        }

        return Path.GetFullPath(path);
    }

    public static LocalServiceChild Start(
        ProcessStartInfo start,
        ServiceStartSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(secrets);
        Process process = ServiceStartSecretChannel.Start(
            start,
            secrets);
        try
        {
            return new LocalServiceChild(
                process,
                secrets.ServiceNonce,
                process.StartTime.ToUniversalTime().Ticks);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
            throw;
        }
    }
}
