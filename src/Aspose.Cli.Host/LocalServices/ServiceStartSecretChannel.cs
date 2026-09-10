using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        "ASPOSE_CLI_SERVICE_START_HANDLE";
    private const int MaximumPayloadBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull,
        };
    private static readonly AsyncLocal<ServiceStartSecrets?> CurrentValue =
        new();

    public static ServiceStartSecrets? Current => CurrentValue.Value;

    public static Process Start(
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
        Process? process = null;
        try
        {
            process = Process.Start(start)
                ?? throw new InvalidOperationException(
                    "The service process could not be started.");
            server.DisposeLocalCopyOfClientHandle();
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                secrets,
                JsonOptions);
            try
            {
                if (payload.Length > MaximumPayloadBytes)
                {
                    throw new InvalidOperationException(
                        "The service-start secret payload is too large.");
                }

                Span<byte> length = stackalloc byte[sizeof(int)];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                    length,
                    payload.Length);
                server.Write(length);
                server.Write(payload);
                server.Flush();
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(
                    payload);
            }

            return process;
        }
        catch
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }

            process?.Dispose();
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
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        ReadExactly(client, lengthBytes);
        int length =
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(
                lengthBytes);
        if (length is <= 0 or > MaximumPayloadBytes)
        {
            throw new InvalidDataException(
                "The service-start secret payload length is invalid.");
        }

        byte[] payload = new byte[length];
        try
        {
            ReadExactly(client, payload);
            ServiceStartSecrets secrets =
                JsonSerializer.Deserialize<ServiceStartSecrets>(
                    payload,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "The service-start secret payload is empty.");
            if (secrets.Version != 1)
            {
                throw new InvalidDataException(
                    $"Unsupported service-start contract version {secrets.Version}.");
            }

            return secrets;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(
                payload);
        }
    }

    public static IDisposable Push(ServiceStartSecrets secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        ServiceStartSecrets? prior = CurrentValue.Value;
        CurrentValue.Value = secrets;
        return new RestoreScope(prior);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    "The service-start secret channel ended early.");
            }

            total += read;
        }
    }

    private static void PreventStandardHandleInheritance()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

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

    public string? Password { get; init; }

    public required string ServiceToken { get; init; }

    public required string ServiceNonce { get; init; }

    public Aspose.Cli.Sdk.Preview.ProductPreviewPayload? PreviewSelector
    {
        get;
        init;
    }

    public Aspose.Cli.Sdk.Rendering.FontSearchProfile? FontProfile
    {
        get;
        init;
    }
}
