namespace Aspose.Cli.Host.LocalServices;

/// <summary>Copies a stream without allowing an unbounded read or write.</summary>
internal static class BoundedStreamCopy
{
    private const int BufferSize = 81920;

    public static long Copy(
        Stream input,
        Stream output,
        long limit,
        Func<long, Exception> limitExceeded)
    {
        Validate(input, output, limit, limitExceeded);
        byte[] buffer = new byte[BufferSize];
        long total = 0;
        while (true)
        {
            int read = input.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                return total;
            }

            total = checked(total + read);
            if (total > limit)
            {
                throw limitExceeded(total);
            }

            output.Write(buffer, 0, read);
        }
    }

    public static async Task<long> CopyAsync(
        Stream input,
        Stream output,
        long limit,
        Func<long, Exception> limitExceeded,
        CancellationToken cancellationToken = default)
    {
        Validate(input, output, limit, limitExceeded);
        byte[] buffer = new byte[BufferSize];
        long total = 0;
        while (true)
        {
            int read = await input.ReadAsync(
                buffer,
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return total;
            }

            total = checked(total + read);
            if (total > limit)
            {
                throw limitExceeded(total);
            }

            await output.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Validate(
        Stream input,
        Stream output,
        long limit,
        Func<long, Exception> limitExceeded)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(limitExceeded);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
    }
}
