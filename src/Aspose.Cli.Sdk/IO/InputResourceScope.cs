namespace Aspose.Cli.Sdk.IO;

/// <summary>Keeps bounded input streams alive until a consuming document engine finishes saving.</summary>
public sealed class InputResourceScope : IDisposable
{
    private readonly InputSource _inputs;
    private readonly List<Stream> _streams = [];
    private bool _disposed;

    internal InputResourceScope(InputSource inputs) => _inputs = inputs;

    public Stream OpenFile(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Stream stream = _inputs.OpenFile(path);
        _streams.Add(stream);
        return stream;
    }

    public void ThrowIfFailed() => _inputs.ThrowIfFailed();

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        foreach (Stream stream in _streams.AsEnumerable().Reverse()) { stream.Dispose(); }
        _streams.Clear();
    }
}
