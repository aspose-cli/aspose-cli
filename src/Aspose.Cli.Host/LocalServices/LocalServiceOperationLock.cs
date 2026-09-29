using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.LocalServices;

/// <summary>
/// Serializes one local service's cross-process start and stop decisions.
/// </summary>
internal sealed class LocalServiceOperationLock : IDisposable
{
    private readonly FileStream _stream;
    private int _disposed;

    private LocalServiceOperationLock(FileStream stream) =>
        _stream = stream;

    public static LocalServiceOperationLock Acquire(
        string service,
        string key,
        TimeSpan timeout)
    {
        ValidateSegment(service, nameof(service));
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        string digest = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(key))
                .AsSpan(0, 16))
            .ToLowerInvariant();
        string directory = Directory.CreateDirectory(
            Path.Combine(
                UserStorage.TemporaryRoot(),
                "services",
                service,
                "locks")).FullName;
        string path = Path.Combine(directory, digest + ".lock");
        EnsureFile(path);

        Stopwatch watch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new LocalServiceOperationLock(
                    new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1,
                        FileOptions.WriteThrough));
            }
            catch (IOException)
            {
                // Another process holds the lock; waiting ends only in the
                // lock or in a timeout, never in the last sharing violation.
                if (watch.Elapsed >= timeout)
                {
                    throw new TimeoutException(
                        $"Timed out acquiring the {service} service operation lock.");
                }
                Thread.Sleep(25);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _stream.Dispose();
        }
    }

    private static void EnsureFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        try
        {
            using FileStream _ = new(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Another process created the lock file first; Acquire opens it.
        }
    }

    private static void ValidateSegment(
        string value,
        string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(static character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Service names use letters, digits, '-' or '_'.",
                parameter);
        }
    }
}
