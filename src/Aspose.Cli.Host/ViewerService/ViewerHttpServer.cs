using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Viewer;

namespace Aspose.Cli.Host.ViewerService;

/// <summary>
/// The loopback surface of the viewer service. It accepts exact loopback
/// requests only and serves three things per open document: the viewer page,
/// its event stream, and the immutable files of a rendered revision. Those
/// never mutate state, so they carry no body and answer GET alone. The App,
/// when one is mounted, answers everything outside <c>/d/</c> on the same
/// origin, including its own mutations.
/// </summary>
internal sealed class ViewerHttpServer : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly ViewerDocuments _documents;
    private readonly HttpRequestGate _requests;
    private Func<HttpListenerContext, int, bool>? _home;
    private readonly string _nonce = LocalHttpRequestSecurity.RandomToken();
    private readonly int _requestedPort;
    private HttpListener? _listener;
    private Task? _acceptLoop;
    private volatile bool _stopping;
    private int _port;
    private int _disposed;

    public ViewerHttpServer(
        ViewerDocuments documents,
        int requestedPort,
        LocalServiceResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(limits);
        _documents = documents;
        _requestedPort = requestedPort;
        _requests = new HttpRequestGate(limits.MaximumConcurrentRequests);
    }

    /// <summary>Bound loopback port; 0 until the server is started.</summary>
    public int Port => _port;

    /// <summary>
    /// Mounts the App on this origin. It is attached after the port is bound,
    /// because the App addresses itself by it.
    /// </summary>
    public void Mount(Func<HttpListenerContext, int, bool> home) => _home = home;

    /// <summary>The address a person opens for one document.</summary>
    public string Url(string documentId) =>
        string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{_port}/d/{documentId}/");

    /// <summary>Binds loopback and starts accepting; returns the bound port.</summary>
    public int Start()
    {
        LoopbackHttpListenerBinding binding = LoopbackHttpListenerBinder.Start(_requestedPort);
        _listener = binding.Listener;
        _port = binding.Port;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(binding.Listener));
        return _port;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _stopping = true;
        _requests.StopAccepting();
        try { _listener?.Close(); } catch (ObjectDisposedException) { }
        try { _acceptLoop?.Wait(StopTimeout); } catch (AggregateException) { }
        bool drained = _requests.WaitForDrain(StopTimeout);
        DeferredResourceCleanup.CompleteOrDefer(drained && (_acceptLoop?.IsCompleted ?? true),
            () =>
            {
                try { _acceptLoop?.GetAwaiter().GetResult(); } catch (Exception) { }
                _requests.WaitForDrain(Timeout.InfiniteTimeSpan);
            }, _requests.Dispose, "aspose-viewer-http-cleanup", "viewer HTTP requests");
    }

    private async Task AcceptLoopAsync(HttpListener listener)
    {
        while (true)
        {
            HttpListenerContext? context = await AcceptAsync(listener).ConfigureAwait(false);
            if (context is null)
            {
                return;
            }
            if (!_requests.TryEnter(out IDisposable? lease))
            {
                Reject(context.Response);
                continue;
            }
            _ = Task.Run(() =>
            {
                using (lease)
                {
                    Route(context);
                }
            });
        }
    }

    private async Task<HttpListenerContext?> AcceptAsync(HttpListener listener)
    {
        while (true)
        {
            try
            {
                return await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                if (_stopping || !listener.IsListening)
                {
                    return null;
                }
            }
        }
    }

    private void Route(HttpListenerContext context)
    {
        HttpListenerResponse response = context.Response;
        try
        {
            Headers(response);
            if (!LocalHttpRequestSecurity.IsRequestAllowed(context.Request, _port))
            {
                Text(response, 403, "text/plain; charset=utf-8", "Forbidden: the viewer accepts exact loopback requests only.");
                return;
            }
            string path = context.Request.Url?.AbsolutePath ?? "/";
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments is not ["d", ..] && _home is { } home && home(context, _port))
            {
                return;
            }
            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                response.Headers["Allow"] = "GET";
                Text(response, 405, "text/plain; charset=utf-8", "Method not allowed: the viewer only serves GET.");
                return;
            }
            if (segments is not ["d", { Length: > 0 } id, ..]
                || _documents.Find(id) is not { } document)
            {
                Text(response, 404, "text/plain; charset=utf-8", "Not found.");
                return;
            }

            document.RecordActivity();
            if (segments.Length == 2)
            {
                // Relative asset and event URLs need the trailing slash.
                if (!path.EndsWith('/'))
                {
                    response.Redirect(path + "/");
                    response.Close();
                    return;
                }
                Page(response, document);
                return;
            }
            if (segments is [_, _, "events"])
            {
                Events(response, document);
                return;
            }
            if (segments is [_, _, "r", { } number, ..]
                && int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int revision))
            {
                Serve(response, document.Find(revision), string.Join('/', segments.Skip(4)));
                return;
            }
            if (segments is [_, _, "p", { Length: > 0 } address])
            {
                LiveRevision? revisionOf = document.Current is { } current && current.Addressed.ContainsKey(address)
                    ? current
                    : document.Find((document.Current?.Number ?? 1) - 1);
                Serve(response, revisionOf, revisionOf?.Addressed.GetValueOrDefault(address));
                return;
            }
            Text(response, 404, "text/plain; charset=utf-8", "Not found.");
        }
        catch (Exception)
        {
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch (Exception) { }
        }
    }

    private void Page(HttpListenerResponse response, LiveDocument document)
    {
        if (document.Presentation is not { } presentation || document.Current is null)
        {
            Text(response, 503, "text/plain; charset=utf-8", "The first revision is still rendering.");
            return;
        }
        Text(
            response,
            200,
            "text/html; charset=utf-8",
            ViewerPage.Live(document.FileName, presentation, document.Hello(), _nonce));
    }

    private static void Events(HttpListenerResponse response, LiveDocument document)
    {
        response.StatusCode = 200;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers["Cache-Control"] = "no-store";
        response.SendChunked = true;
        document.Events.Attach(response.OutputStream, document.Hello());
    }

    /// <summary>
    /// Serves one file of a rendered revision. Revisions and parts are
    /// immutable, so their files are cached forever and only what an edit
    /// changed is ever fetched again.
    /// </summary>
    private void Serve(HttpListenerResponse response, LiveRevision? revision, string? file)
    {
        using Stream? content = file is null ? null : revision?.Files.TryOpenRead(Uri.UnescapeDataString(file));
        if (content is null)
        {
            Text(response, 404, "text/plain; charset=utf-8", "Not found.");
            return;
        }
        ContentHeaders(response);
        response.StatusCode = 200;
        response.ContentType = ContentType(Path.GetExtension(file!));
        response.Headers["Cache-Control"] = "private, max-age=31536000, immutable";
        response.ContentLength64 = content.Length;
        content.CopyTo(response.OutputStream);
        response.Close();
    }

    private void Reject(HttpListenerResponse response)
    {
        Headers(response);
        Text(response, 503, "text/plain; charset=utf-8", "Service unavailable: the local request limit is active.");
    }

    private void Headers(HttpListenerResponse response) =>
        LocalServiceSecurityHeaders.Apply(response, LocalServicePageKind.ProductPreview, _nonce);

    /// <summary>
    /// A rendered part is document content, whatever the document contained:
    /// it may paint inside the viewer and nothing else. Scripts a product
    /// exported with its HTML never run, because the presenter drives it,
    /// and no part is ever served as a script. The App and the viewer share
    /// this origin, so every ancestor that frames a part is the origin itself.
    /// </summary>
    private static void ContentHeaders(HttpListenerResponse response)
    {
        response.Headers["Content-Security-Policy"] =
            "default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; "
            + "font-src data:; base-uri 'none'; form-action 'none'; "
            + "frame-ancestors 'self'";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    }

    private static void Text(HttpListenerResponse response, int status, string contentType, string body)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
        response.Close();
    }

    private static string ContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
}
