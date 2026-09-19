namespace Aspose.Cli.Host.Preview;

/// <summary>Owns a background preview's control channel and discovery marker.</summary>
internal sealed class PreviewServiceLifetime : IDisposable
{
    private readonly PreviewSessionStore _store;
    private readonly PreviewSessionMarker _marker;
    private readonly PreviewControlEndpoint _control;
    private int _disposed;

    public PreviewServiceLifetime(
        PreviewSessionStore store,
        PreviewSessionMarker marker,
        Action requestStop,
        Func<PreviewRevisionStatus> readStatus)
    {
        _store = store;
        _marker = marker;
        _control = new PreviewControlEndpoint(
            marker.Id,
            marker.Nonce,
            marker.Token,
            requestStop,
            readStatus);
        _control.Start();
        try
        {
            _store.Write(marker);
        }
        catch
        {
            _control.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _control.Dispose();
        _store.DeleteIfOwned(_marker.Id, _marker.Token);
    }
}
