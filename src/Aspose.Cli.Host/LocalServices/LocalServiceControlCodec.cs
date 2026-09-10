using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Encodes, bounds, and decodes local-service protocol frames.</summary>
internal static class LocalServiceControlCodec
{
    internal const int ProtocolVersion = 1;
    internal const int MaximumPayloadBytes = 64 * 1024;
    internal static readonly TimeSpan DefaultStageTimeout =
        TimeSpan.FromSeconds(5);

    public static byte[] Serialize(
        LocalServiceControlRequest request) =>
        JsonSerializer.SerializeToUtf8Bytes(
            request,
            LocalServiceJsonContext.Default
                .LocalServiceControlRequest);

    public static byte[] Serialize(
        LocalServiceControlResponse response) =>
        JsonSerializer.SerializeToUtf8Bytes(
            response,
            LocalServiceJsonContext.Default
                .LocalServiceControlResponse);

    public static LocalServiceControlRequest? DeserializeRequest(
        byte[] payload) =>
        JsonSerializer.Deserialize(
            payload,
            LocalServiceJsonContext.Default
                .LocalServiceControlRequest);

    public static LocalServiceControlResponse DeserializeResponse(
        byte[] payload) =>
        JsonSerializer.Deserialize(
            payload,
            LocalServiceJsonContext.Default
                .LocalServiceControlResponse)
        ?? throw new InvalidDataException(
            "The local service returned an empty control response.");

    public static async Task<byte[]> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(
            header,
            cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaximumPayloadBytes)
        {
            throw new InvalidDataException(
                "The local-service control frame length is invalid.");
        }

        byte[] payload =
            GC.AllocateUninitializedArray<byte>(length);
        try
        {
            await stream.ReadExactlyAsync(
                payload,
                cancellationToken).ConfigureAwait(false);
            return payload;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(payload);
            throw;
        }
    }

    public static async Task WriteFrameAsync(
        Stream stream,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (payload.Length is <= 0 or > MaximumPayloadBytes)
        {
            throw new InvalidDataException(
                "The local-service control frame payload is invalid.");
        }

        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(
            header,
            payload.Length);
        await stream.WriteAsync(
            header,
            cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(
            payload,
            cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(
            cancellationToken).ConfigureAwait(false);
    }

}
