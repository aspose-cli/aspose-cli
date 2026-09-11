using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>Length-prefixed, bounded private process messages. Payload buffers are cleared after use.</summary>
internal static class ProcessPipeMessages
{
    internal const int MaximumBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        try
        {
            if (payload.Length is <= 0 or > MaximumBytes)
            {
                throw new InvalidDataException("The private process message is too large.");
            }
            byte[] header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    internal static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaximumBytes)
        {
            throw new InvalidDataException("The private process message length is invalid.");
        }
        byte[] payload = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(payload, JsonOptions)
                ?? throw new InvalidDataException("The private process message is empty.");
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }
}
