using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Aspose.Cli.Sdk;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Versioned one-shot parent/child contract for service secrets and resolved
/// execution paths. Only an inheritable anonymous-pipe handle is placed in
/// the child environment; secret values never enter argv or environment
/// variables.
/// </summary>
internal static class ServiceStartSecretChannel
{
    private const string HandleVariable =
        DistributionInfo.EnvironmentVariablePrefix + "SERVICE_START_HANDLE";
    private static readonly AsyncLocal<ServiceStartSecrets?> CurrentValue =
        new();

    public static ServiceStartSecrets? Current => CurrentValue.Value;

    /// <summary>
    /// Starts the service without a kill-on-close job, since it outlives its launcher, and
    /// stops it again when its secrets cannot be handed over.
    /// </summary>
    public static OwnedProcess Start(
        ProcessStartInfo start,
        ServiceStartSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(secrets);
        start.UseShellExecute = false;
        using var server = new AnonymousPipeServerStream(
            PipeDirection.Out,
            HandleInheritability.Inheritable);
        start.Environment[HandleVariable] =
            server.GetClientHandleAsString();
        PreventStandardHandleInheritance();
        OwnedProcess? owned = null;
        try
        {
            owned = OwnedProcess.Start(start, killOnClose: false);
            server.DisposeLocalCopyOfClientHandle();
            ProcessFrames.WriteAsync(server, secrets, LocalServiceJsonContext.Default.ServiceStartSecrets, CancellationToken.None)
                .GetAwaiter().GetResult();

            return owned;
        }
        catch when (owned is not null)
        {
            using (owned) { owned.Stop(); }
            throw;
        }
        finally
        {
            start.Environment.Remove(HandleVariable);
        }
    }

    public static ServiceStartSecrets? TryReceive()
    {
        string? handle =
            Environment.GetEnvironmentVariable(HandleVariable);
        if (string.IsNullOrWhiteSpace(handle))
        {
            return null;
        }

        Environment.SetEnvironmentVariable(HandleVariable, null);
        using var client = new AnonymousPipeClientStream(
            PipeDirection.In,
            handle);
        ServiceStartSecrets secrets = ProcessFrames.ReadAsync(
            client, LocalServiceJsonContext.Default.ServiceStartSecrets, CancellationToken.None).GetAwaiter().GetResult();
        if (secrets.Version != 1)
        {
            throw new InvalidDataException($"Unsupported service-start contract version {secrets.Version}.");
        }
        return secrets;
    }

    public static IDisposable Push(ServiceStartSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        ServiceStartSecrets? prior = CurrentValue.Value;
        CurrentValue.Value = secrets;
        return new RestoreScope(prior);
    }

    private static void PreventStandardHandleInheritance()
    {
        foreach (int id in new[] { -10, -11, -12 })
        {
            nint handle = GetStdHandle(id);
            if (handle != 0 && handle != -1)
            {
                _ = SetHandleInformation(handle, 1, 0);
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(
        nint handle,
        uint mask,
        uint flags);

    private sealed class RestoreScope(
        ServiceStartSecrets? prior)
        : IDisposable
    {
        public void Dispose() => CurrentValue.Value = prior;
    }
}

internal sealed record ServiceStartSecrets
{
    public int Version { get; init; } = 1;

    public required string WorkDirectory { get; init; }

    public string? LicensePath { get; init; }

    public required WireSecret ServiceToken { get; init; }

    public required string ServiceNonce { get; init; }
}
