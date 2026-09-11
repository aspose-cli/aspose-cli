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

    internal static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken, int maximumBytes = MaximumBytes)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        try
        {
            if (payload.Length <= 0 || payload.Length > maximumBytes)
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

    internal static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellationToken,
        int maximumBytes = MaximumBytes, Action<int>? reserve = null) where T : class =>
        await ReadOrEndAsync<T>(stream, cancellationToken, maximumBytes, reserve).ConfigureAwait(false)
            ?? throw new EndOfStreamException("The private process channel ended before its next message.");

    internal static async Task<T?> ReadOrEndAsync<T>(Stream stream, CancellationToken cancellationToken,
        int maximumBytes = MaximumBytes, Action<int>? reserve = null) where T : class
    {
        byte[] header = new byte[sizeof(int)];
        if (await stream.ReadAsync(header.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) == 0) { return null; }
        await stream.ReadExactlyAsync(header.AsMemory(1), cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > maximumBytes)
        {
            throw new InvalidDataException("The private process message length is invalid.");
        }
        reserve?.Invoke(length);
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
