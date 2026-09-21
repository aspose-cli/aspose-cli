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
        foreach (string name in new[]
        {
            Aspose.Cli.Sdk.Execution.WorkerOutputSession.WorkerEnvironmentVariable,
            Aspose.Cli.Sdk.Execution.WorkerOutputSession.RootEnvironmentVariable,
            Aspose.Cli.Sdk.Execution.WorkerOutputSession.ManifestEnvironmentVariable,
            Aspose.Cli.Sdk.Execution.WorkerOutputSession.DeadlineEnvironmentVariable,
            Aspose.Cli.Sdk.Execution.WorkerOutputSession.BudgetEnvironmentVariable,
        }) { start.Environment.Remove(name); }
        if (OperatingSystem.IsWindows())
        {
            start.WindowStyle = ProcessWindowStyle.Hidden;
        }

        return start;
    }

    /// <summary>
    /// Where the managed entry assembly sits, for the development layout in
    /// which the host process is <c>dotnet</c> and the assembly has to be
    /// named on its command line. It is read from the base directory rather
    /// than the assembly's own location, because a single-file app reports no
    /// location for what is embedded in it.
    /// </summary>
    internal static string ResolveManagedEntryAssemblyPath(
        Assembly entryAssembly,
        string optionName,
        string missingExecutableHint)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        string path = Path.Combine(
            AppContext.BaseDirectory,
            entryAssembly.GetName().Name + ".dll");
        if (!File.Exists(path))
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
