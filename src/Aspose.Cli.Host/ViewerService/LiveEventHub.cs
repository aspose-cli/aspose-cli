using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Aspose.Cli.Host.LocalServices;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// Bounded, per-client SSE fan-out. Network writes never occur under the
/// global membership lock, so one slow client cannot stall another client or
/// the renderer publishing an event.
/// </summary>
internal sealed class LiveEventHub : IDisposable
{
    private static readonly byte[] HeartbeatFrame =
        Encoding.UTF8.GetBytes(": ping\n\n");
    private static readonly TimeSpan DefaultHeartbeatInterval =
        TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Dictionary<long, Client> _clients = [];
    private readonly Timer _heartbeat;
    private readonly int _maximumClients;
    private readonly int _queueCapacity;
    private readonly TimeSpan _writeTimeout;
    private long _nextClientId;
    private bool _disposed;

    /// <summary>Creates a bounded hub and starts its heartbeat.</summary>
    public LiveEventHub(TimeSpan? heartbeatInterval = null)
        : this(
            heartbeatInterval,
            LocalServiceResourceLimits.Resolve())
    {
    }

    internal LiveEventHub(
        TimeSpan? heartbeatInterval,
        LocalServiceResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        TimeSpan interval =
            heartbeatInterval ?? DefaultHeartbeatInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            interval,
            TimeSpan.Zero,
            nameof(heartbeatInterval));
        _maximumClients = limits.MaximumSseClients;
        _queueCapacity = limits.SseQueueCapacity;
        _writeTimeout = limits.SseWriteTimeout;
        _heartbeat = new Timer(
            static state =>
                ((LiveEventHub)state!).Publish(HeartbeatFrame),
            this,
            interval,
            interval);
    }

    /// <summary>Number of currently attached clients.</summary>
    public int ClientCount
    {
        get
        {
            lock (_gate)
            {
                return _clients.Count;
            }
        }
    }

    /// <summary>
    /// Queues the hello frame before admitting the client. The hub owns and
    /// disposes <paramref name="stream"/> after this call.
    /// </summary>
    public void Attach(Stream stream, string helloJson)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(helloJson);
        byte[] hello;
        try
        {
            hello = EncodeFrame("hello", helloJson);
        }
        catch
        {
            DisposeQuietly(stream);
            throw;
        }

        lock (_gate)
        {
            if (_disposed || _clients.Count >= _maximumClients)
            {
                DisposeQuietly(stream);
                return;
            }

            long id = checked(++_nextClientId);
            var client = new Client(
                id,
                stream,
                _queueCapacity,
                _writeTimeout,
                Remove);
            if (!client.TryWrite(hello))
            {
                client.Dispose();
                return;
            }

            _clients.Add(id, client);
            client.Start();
        }
    }

    /// <summary>
    /// Serializes and queues one event independently for every client. An
    /// invalid event name or invalid JSON payload is rejected. A full client
    /// queue disconnects only that slow client.
    /// </summary>
    public void Broadcast(string eventName, string json)
    {
        byte[] frame = EncodeFrame(eventName, json);
        Publish(frame);
    }

    /// <summary>Stops heartbeat and all client writers. Idempotent.</summary>
    public void Dispose()
    {
        Client[] clients;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            clients = _clients.Values.ToArray();
            _clients.Clear();
        }

        _heartbeat.Dispose();
        foreach (Client client in clients)
        {
            client.Dispose();
        }

        try
        {
            Task.WhenAll(
                    clients.Select(
                        static client => client.Completion))
                .Wait(_writeTimeout + TimeSpan.FromSeconds(1));
        }
        catch (Exception exception) when (
            exception is AggregateException
                or OperationCanceledException)
        {
        }
    }

    private void Publish(byte[] frame)
    {
        List<Client>? overflowed = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (Client client in _clients.Values)
            {
                if (!client.TryWrite(frame))
                {
                    (overflowed ??= []).Add(client);
                }
            }

            if (overflowed is not null)
            {
                foreach (Client client in overflowed)
                {
                    _clients.Remove(client.Id);
                }
            }
        }

        if (overflowed is not null)
        {
            foreach (Client client in overflowed)
            {
                client.Dispose();
            }
        }
    }

    private void Remove(Client client)
    {
        lock (_gate)
        {
            _clients.Remove(client.Id);
        }

        client.Dispose();
    }

    private static byte[] EncodeFrame(
        string eventName,
        string json)
    {
        if (string.IsNullOrEmpty(eventName)
            || eventName.Length > 64
            || eventName.Any(static character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "SSE event names use at most 64 ASCII letters, digits, '-' or '_'.",
                nameof(eventName));
        }

        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument document = JsonDocument.Parse(json);
        string normalized = JsonSerializer.Serialize(
            document.RootElement);
        return Encoding.UTF8.GetBytes(
            $"event: {eventName}\ndata: {normalized}\n\n");
    }

    private static void DisposeQuietly(Stream stream)
    {
        try
        {
            stream.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private sealed class Client : IDisposable
    {
        private readonly Stream _stream;
        private readonly Channel<byte[]> _frames;
        private readonly TimeSpan _writeTimeout;
        private readonly Action<Client> _failed;
        private readonly CancellationTokenSource _shutdown = new();
        private Task? _writer;
        private int _disposed;

        public Client(
            long id,
            Stream stream,
            int queueCapacity,
            TimeSpan writeTimeout,
            Action<Client> failed)
        {
            Id = id;
            _stream = stream;
            _writeTimeout = writeTimeout;
            _failed = failed;
            _frames = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(queueCapacity)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false,
                });
        }

        public long Id { get; }

        public Task Completion =>
            Volatile.Read(ref _writer) ?? Task.CompletedTask;

        public bool TryWrite(byte[] frame) =>
            Volatile.Read(ref _disposed) == 0
            && _frames.Writer.TryWrite(frame);

        public void Start() =>
            _writer = Task.Run(WriteLoop);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _frames.Writer.TryComplete();
            _shutdown.Cancel();
            DisposeQuietly(_stream);
        }

        private async Task WriteLoop()
        {
            try
            {
                await foreach (byte[] frame in _frames.Reader.ReadAllAsync(
                    _shutdown.Token).ConfigureAwait(false))
                {
                    using var deadline =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            _shutdown.Token);
                    deadline.CancelAfter(_writeTimeout);
                    await _stream.WriteAsync(
                        frame,
                        deadline.Token).ConfigureAwait(false);
                    await _stream.FlushAsync(
                        deadline.Token).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (
                exception is IOException
                    or OperationCanceledException
                    or ObjectDisposedException
                    or NotSupportedException)
            {
                if (Volatile.Read(ref _disposed) == 0)
                {
                    _failed(this);
                }
            }
            finally
            {
                DisposeQuietly(_stream);
            }
        }
    }
}
