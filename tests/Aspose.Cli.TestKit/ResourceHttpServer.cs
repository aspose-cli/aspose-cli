using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aspose.Cli.TestKit;

/// <summary>A real HTTP endpoint that records any attempted external resource fetch.</summary>
public sealed class ResourceHttpServer : IAsyncDisposable
{
    public static byte[] Image { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _requests;
    private int _count;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _paths = new();
    public IReadOnlyList<string> Requests => _paths.ToArray();

    public ResourceHttpServer()
    {
        _listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        _requests = Serve();
    }

    public string Url { get; }
    public int RequestCount => Volatile.Read(ref _count);

    private async Task Serve()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                Interlocked.Increment(ref _count);
                using NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                _paths.Enqueue(await reader.ReadLineAsync(_stop.Token) ?? "empty request");
                while (await reader.ReadLineAsync(_stop.Token) is { Length: > 0 }) { }
                byte[] header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {Image.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, _stop.Token);
                await stream.WriteAsync(Image, _stop.Token);
            }
        }
        catch (Exception exception) when (
            _stop.IsCancellationRequested && exception is OperationCanceledException or SocketException or IOException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        await _requests;
        _stop.Dispose();
    }
}
