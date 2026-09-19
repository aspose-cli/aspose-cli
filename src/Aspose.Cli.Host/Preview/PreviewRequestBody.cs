using System.Net;

namespace Aspose.Cli.Host.Preview;

/// <summary>Consumes the bounded body of a manual preview refresh request.</summary>
internal static class PreviewRequestBody
{
    private const long MaximumBytes = 64 * 1024;
    private static readonly TimeSpan ReadTimeout =
        TimeSpan.FromSeconds(5);

    /// <summary>
    /// Discards a request body within the shared preview size and time limits.
    /// A zero status indicates success; otherwise the value is an HTTP status.
    /// </summary>
    public static int Drain(HttpListenerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength64 > MaximumBytes)
        {
            return 413;
        }

        long deadline = Environment.TickCount64
            + (long)ReadTimeout.TotalMilliseconds;
        byte[] buffer = new byte[8192];
        long total = 0;
        while (true)
        {
            long remaining = deadline - Environment.TickCount64;
            if (remaining <= 0)
            {
                return 408;
            }

            Task<int> read = request.InputStream.ReadAsync(
                buffer,
                0,
                buffer.Length);
            if (!read.Wait(TimeSpan.FromMilliseconds(remaining)))
            {
                return 408;
            }
            if (read.Result == 0)
            {
                return 0;
            }

            total += read.Result;
            if (total > MaximumBytes)
            {
                return 413;
            }
        }
    }
}
