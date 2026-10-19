using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// The one frame format the Host's own processes exchange: a little-endian length, then that
/// many bytes of JSON written by a source-generated context. Lengths are bounded before a
/// payload is read, and payload buffers are cleared after use because frames carry secrets.
/// </summary>
internal static class ProcessFrames
{
    internal const int MaximumBytes = 64 * 1024;

    internal static async Task WriteAsync<T>(
        Stream stream, T value, JsonTypeInfo<T> type, CancellationToken cancellationToken, int maximumBytes = MaximumBytes)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, type);
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

    internal static async Task<T> ReadAsync<T>(Stream stream, JsonTypeInfo<T> type, CancellationToken cancellationToken,
        int maximumBytes = MaximumBytes, Action<int>? reserve = null) where T : class =>
        await ReadOrEndAsync(stream, type, cancellationToken, maximumBytes, reserve).ConfigureAwait(false)
            ?? throw new EndOfStreamException("The private process channel ended before its next message.");

    /// <summary>Reads the next frame, or returns null when the channel ends between frames.</summary>
    internal static async Task<T?> ReadOrEndAsync<T>(Stream stream, JsonTypeInfo<T> type, CancellationToken cancellationToken,
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
            return JsonSerializer.Deserialize(payload, type)
                ?? throw new InvalidDataException("The private process message is empty.");
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }
}
