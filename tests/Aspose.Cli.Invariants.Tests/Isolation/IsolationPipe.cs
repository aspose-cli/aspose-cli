using System.IO.Pipes;

namespace Aspose.Cli.Invariants.Tests.Isolation;

/// <summary>
/// The named pipe of one <see cref="ReferenceKind.Pipe"/> case: listening from construction to
/// disposal, with a new instance waiting before the last one is let go, so every connection to
/// its name is counted. Each connection is closed at once, so an engine that opened it reads
/// nothing and does not wait.
/// </summary>
internal sealed class IsolationPipe : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accepting;
    private int _connections;

    public IsolationPipe()
    {
        // The first instance exists before the command starts.
        NamedPipeServerStream first = Create();
        _accepting = Task.Run(() => AcceptAsync(first));
    }

    /// <summary>The pipe's name, unique to the case.</summary>
    // The name ends as an SVG file's does, so a reference to it also stands for a link to an
    // external .svg file, which a reader may treat apart from other images.
    public string Name { get; } = "aspose-cli-isolation-" + Guid.NewGuid().ToString("N") + ".svg";

    /// <summary>How many times anything connected to the pipe.</summary>
    public int Connections => Volatile.Read(ref _connections);

    /// <summary>The local device path of a pipe: <c>\\.\pipe\name</c>.</summary>
    public static string DevicePath(string name) => @"\\.\pipe\" + name;

    /// <summary>The same pipe through the local machine's name: <c>\\localhost\pipe\name</c>.</summary>
    public static string SharePath(string name) => @"\\localhost\pipe\" + name;

    /// <summary>The same pipe as a file URI: <c>file://localhost/pipe/name</c>.</summary>
    public static string FileUri(string name) => "file://localhost/pipe/" + name;

    /// <summary>
    /// The connections counted once the command has ended: a connection the engine made is
    /// complete by then, but its count may still be on its way.
    /// </summary>
    public async Task<int> SettledConnections()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        return Connections;
    }

    private NamedPipeServerStream Create() => new(
        Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    private async Task AcceptAsync(NamedPipeServerStream server)
    {
        try
        {
            while (true)
            {
                try
                {
                    await server.WaitForConnectionAsync(_stop.Token);
                }
                catch (IOException)
                {
                    // A client that went away between connecting and the accept still connected.
                }

                Interlocked.Increment(ref _connections);
                NamedPipeServerStream next = Create();
                await server.DisposeAsync();
                server = next;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _accepting;
        _stop.Dispose();
    }
}
