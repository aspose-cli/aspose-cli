using System.Globalization;
using System.Net;
using System.Text;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Listener-independent preview admission, authorization, routing, and
/// response pipeline. A standalone server or another loopback host supplies
/// only the request context and its bound port.
/// </summary>
internal sealed class PreviewRequestPipeline
{
    private const string WaitingPage =
        "<html><head><meta charset=\"utf-8\"><title>Preview</title></head>"
        + "<body><p style=\"font-family: sans-serif;\">Waiting for the first render&hellip;</p></body></html>";

    private readonly PreviewRequestOptions _options;
    private readonly LocalHttpRequestSecurity _security;
    private readonly PreviewResponseWriter _responses;
    private readonly PreviewPageShell _shell;

    public PreviewRequestPipeline(PreviewRequestOptions options)
    {
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
        _security = new LocalHttpRequestSecurity(options.CsrfToken);
        _responses = new PreviewResponseWriter();
        _shell = new PreviewPageShell(
            options.View,
            options.DocumentName,
            options.EvalMode,
            StylesheetAvailable:
                options.ShellStylesheet is not null,
            ScriptNonce: options.ScriptNonce,
            DocumentPath: options.DocumentPath);
    }

    public void RejectUnavailable(HttpListenerResponse response)
    {
        _responses.ApplySecurityHeaders(
            response,
            _options.ScriptNonce);
        _responses.Text(
            response,
            503,
            "text/plain; charset=utf-8",
            "Service unavailable: the local request limit is active.");
    }

    public void Route(
        HttpListenerContext context,
        int boundPort)
    {
        HttpListenerResponse response = context.Response;
        _responses.ApplySecurityHeaders(
            response,
            _options.ScriptNonce);
        try
        {
            if (!LocalHttpRequestSecurity.IsRequestAllowed(context.Request, boundPort))
            {
                _responses.Text(
                    response,
                    403,
                    "text/plain; charset=utf-8",
                    "Forbidden: the preview accepts exact loopback requests only.");
                return;
            }

            Dispatch(
                context,
                context.Request.Url?.AbsolutePath ?? "/",
                boundPort);
        }
        catch (Exception)
        {
            PreviewResponseWriter.Fail(response);
        }
    }

    /// <summary>
    /// Routes a request already admitted by an embedding loopback host. The
    /// preview still applies its own response policy and independently checks
    /// Origin and CSRF for refresh mutations.
    /// </summary>
    /// <returns>True transfers response completion to Preview, including any live stream;
    /// false leaves the response untouched and owned by the caller.</returns>
    public bool RouteAuthorized(
        HttpListenerContext context,
        int boundPort,
        string path)
    {
        if (!OwnsPath(path))
        {
            return false;
        }

        try
        {
            _responses.ApplySecurityHeaders(
                context.Response,
                _options.ScriptNonce,
                _options.SameOriginMount);
            Dispatch(context, path, boundPort);
        }
        catch (Exception)
        {
            PreviewResponseWriter.Fail(context.Response);
        }
        return true;
    }

    public bool OwnsPath(string path) =>
        string.Equals(path, _options.DocumentPath, StringComparison.Ordinal)
        || path.StartsWith("/live/", StringComparison.Ordinal)
        || path.StartsWith("/asset/", StringComparison.Ordinal);

    private void Dispatch(
        HttpListenerContext context,
        string path,
        int boundPort)
    {
        if (string.Equals(
                path,
                "/live/refresh",
                StringComparison.Ordinal))
        {
            ServeRefresh(
                context,
                boundPort);
            return;
        }

        HttpListenerResponse response = context.Response;
        if (!string.Equals(
                context.Request.HttpMethod,
                "GET",
                StringComparison.OrdinalIgnoreCase))
        {
            NotFound(response);
            return;
        }

        if (string.Equals(
                path,
                _options.DocumentPath,
                StringComparison.Ordinal))
        {
            ServeDocument(response);
            return;
        }

        switch (path)
        {
            case "/live/client.js":
                _responses.Text(
                    response,
                    200,
                    "application/javascript; charset=utf-8",
                    SecuredClientScript());
                break;
            case "/live/shell.css"
                when _options.ShellStylesheet is { } stylesheet:
                _responses.Text(
                    response,
                    200,
                    "text/css; charset=utf-8",
                    stylesheet);
                break;
            case "/live/events":
                ServeEventStream(response);
                break;
            default:
                if (path.StartsWith(
                        "/asset/",
                        StringComparison.Ordinal))
                {
                    ServeAsset(response, path);
                }
                else
                {
                    NotFound(response);
                }
                break;
        }
    }

    private void ServeDocument(HttpListenerResponse response)
    {
        (string html, int revision) = ReadDocument(_options.CurrentSnapshot());
        _responses.Text(
            response,
            200,
            "text/html; charset=utf-8",
            InjectCsrf(
                PreviewPageComposer.InjectLiveClient(
                    html,
                    revision,
                    _shell)));
    }

    private static (string Html, int Revision) ReadDocument(
        PreviewSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return (WaitingPage, 0);
        }
        if (snapshot.InlineHtml is not null)
        {
            return (snapshot.InlineHtml, snapshot.Revision);
        }

        using Stream entry =
            TryManifest(snapshot)?.TryOpenRead(
                snapshot.EntryFileName)
            ?? throw new FileNotFoundException(
                "The preview entry is no longer available.");
        using var reader = new StreamReader(
            entry,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return (reader.ReadToEnd(), snapshot.Revision);
    }

    private void ServeEventStream(HttpListenerResponse response)
    {
        int revision = _options.CurrentSnapshot()?.Revision ?? 0;
        response.StatusCode = 200;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers["Cache-Control"] = "no-cache";
        response.SendChunked = true;
        string hello = string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"revision\":{revision},\"view\":\"{_options.View}\"}}");
        _options.Hub.Attach(response.OutputStream, hello);
    }

    private void ServeAsset(
        HttpListenerResponse response,
        string urlPath)
    {
        PreviewSnapshot? snapshot = _options.CurrentSnapshot();
        if (snapshot?.DirectoryPath is null)
        {
            NotFound(response);
            return;
        }

        string relative;
        try
        {
            relative = Uri.UnescapeDataString(
                urlPath["/asset/".Length..]);
        }
        catch (UriFormatException)
        {
            NotFound(response);
            return;
        }

        Stream? asset = TryManifest(snapshot)?.TryOpenRead(relative);
        if (asset is null)
        {
            NotFound(response);
            return;
        }

        using (asset)
        {
            _responses.Stream(
                response,
                200,
                ContentTypeFor(Path.GetExtension(relative)),
                asset);
        }
    }

    private void ServeRefresh(
        HttpListenerContext context,
        int boundPort)
    {
        HttpListenerResponse response = context.Response;
        if (_options.RefreshRequested is not { } refreshRequested)
        {
            NotFound(response);
            return;
        }
        if (!string.Equals(
                context.Request.HttpMethod,
                "POST",
                StringComparison.OrdinalIgnoreCase))
        {
            response.Headers["Allow"] = "POST";
            _responses.Text(
                response,
                405,
                "text/plain; charset=utf-8",
                "Method not allowed: trigger a refresh with POST.");
            return;
        }
        if (!_security.IsMutationAuthorized(
                context.Request,
                boundPort))
        {
            _responses.Text(
                response,
                403,
                "text/plain; charset=utf-8",
                "Forbidden: refresh requires this session's exact Origin and CSRF token.");
            return;
        }

        int bodyStatus = PreviewRequestBody.Drain(context.Request);
        if (bodyStatus != 0)
        {
            _responses.Text(
                response,
                bodyStatus,
                "text/plain; charset=utf-8",
                bodyStatus == 413
                    ? "Payload too large: the refresh body is capped at 64 KB."
                    : "Request timeout: the refresh body was not received in time.");
            return;
        }

        refreshRequested();
        int revision = _options.CurrentSnapshot()?.Revision ?? 0;
        _responses.Text(
            response,
            202,
            "application/json; charset=utf-8",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{{\"revision\":{revision}}}"));
    }

    private void NotFound(HttpListenerResponse response) =>
        _responses.Text(
            response,
            404,
            "text/plain; charset=utf-8",
            "Not found.");

    private static string ContentTypeFor(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".json" => "application/json; charset=utf-8",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };

    private static PreviewArtifactManifest? TryManifest(
        PreviewSnapshot snapshot)
    {
        if (snapshot.ArtifactManifest is { } manifest)
        {
            return manifest;
        }
        if (snapshot.DirectoryPath is not { } directory)
        {
            return null;
        }

        try
        {
            return PreviewArtifactManifest.Validate(
                directory,
                snapshot.EntryFileName,
                LocalServiceResourceLimits.Resolve());
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or CliException)
        {
            return null;
        }
    }

    private string SecuredClientScript()
    {
        const string wrapper =
            "(()=>{const f=window.fetch.bind(window);window.fetch=(i,o={})=>{"
            + "const u=new URL(typeof i==='string'?i:i.url,location.href);"
            + "if(u.origin===location.origin&&u.pathname==='/live/refresh'"
            + "&&(o.method||'GET').toUpperCase()==='POST'){"
            + "const h=new Headers(o.headers||{});"
            + "const m=document.querySelector('meta[name=\"aspose-csrf\"]');"
            + "if(m)h.set('X-CSRF-Token',m.content);o={...o,headers:h};}"
            + "return f(i,o);};})();\n";
        return wrapper
            + PreviewClientRuntime.Script
            + "\n"
            + _options.ClientScript;
    }

    private string InjectCsrf(string html)
    {
        string meta =
            $"<meta name=\"aspose-csrf\" content=\"{_options.CsrfToken}\">";
        int head = html.IndexOf(
            "</head>",
            StringComparison.OrdinalIgnoreCase);
        return head < 0
            ? meta + html
            : html.Insert(head, meta);
    }
}
