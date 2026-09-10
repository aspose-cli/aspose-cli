using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Serialization;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// Listener-independent preview admission, authorization, routing, and
/// response pipeline. A standalone server or another loopback host supplies
/// only the request context and its bound port.
/// </summary>
internal sealed class PreviewRequestPipeline
{
    private const string RevisionHeader = "X-Aspose-Preview-Revision";
    private const string WaitingPage =
        "<html><head><meta charset=\"utf-8\"><title>Preview</title></head>"
        + "<body><p style=\"font-family: sans-serif;\">Waiting for the first render&hellip;</p></body></html>";

    private readonly PreviewRequestOptions _options;
    private readonly LocalHttpRequestSecurity _security;
    private readonly PreviewResponseWriter _responses;
    private readonly PreviewPageShell _shell;
    private readonly string _viewRoutePrefix;

    public PreviewRequestPipeline(PreviewRequestOptions options)
    {
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
        _security = new LocalHttpRequestSecurity(options.CsrfToken);
        _responses = new PreviewResponseWriter();
        _viewRoutePrefix = options.DocumentPath == "/"
            ? "/view/"
            : options.DocumentPath + "/view/";
        _shell = new PreviewPageShell(
            options.View,
            options.DocumentName,
            options.EvalMode,
            StylesheetAvailable:
                options.ShellStylesheet is not null,
            ScriptNonce: options.ScriptNonce,
            DocumentPath: options.DocumentPath,
            StateStorageKey: options.StateStorageKey);
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
    public bool RouteAuthorized(
        HttpListenerContext context,
        int boundPort,
        string path)
    {
        if (!OwnsPath(path))
        {
            return false;
        }

        _responses.ApplySecurityHeaders(
            context.Response,
            _options.ScriptNonce,
            _options.SameOriginMount);
        try
        {
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
        || string.Equals(path, "/frame.png", StringComparison.Ordinal)
        || path.StartsWith("/live/", StringComparison.Ordinal)
        || path.StartsWith("/asset/", StringComparison.Ordinal)
        || path.StartsWith(_viewRoutePrefix, StringComparison.Ordinal);

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
        if (string.Equals(
                path,
                "/live/state",
                StringComparison.Ordinal))
        {
            ServeState(context, boundPort);
            return;
        }

        if (path.StartsWith(
                _viewRoutePrefix,
                StringComparison.Ordinal))
        {
            ServeViewRoute(context, path);
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
            case "/frame.png" when IsImageView:
                ServeFrame(response);
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

    private bool IsImageView =>
        string.Equals(
            _options.View,
            "image",
            StringComparison.Ordinal);

    private void ServeDocument(HttpListenerResponse response)
    {
        PreviewSnapshot? snapshot = _options.CurrentSnapshot();
        if (IsImageView)
        {
            _responses.Text(
                response,
                200,
                "text/html; charset=utf-8",
                InjectCsrf(
                    PreviewPageComposer.ImageShell(
                        snapshot?.Revision ?? 0,
                        _shell)));
            return;
        }

        (string html, int revision) = ReadDocument(snapshot);
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

        using FileStream entry =
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

        ServeSnapshotAsset(response, snapshot, relative);
    }

    private void ServeSnapshotAsset(
        HttpListenerResponse response,
        PreviewSnapshot snapshot,
        string relative)
    {
        FileStream? asset = TryManifest(snapshot)?.TryOpenRead(relative);
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

    private void ServeFrame(HttpListenerResponse response)
    {
        PreviewSnapshot? snapshot = _options.CurrentSnapshot();
        if (snapshot?.DirectoryPath is null)
        {
            NotFound(response);
            return;
        }

        FileStream? frame = TryManifest(snapshot)?.TryOpenRead(
            snapshot.EntryFileName);
        if (frame is null)
        {
            NotFound(response);
            return;
        }

        using (frame)
        {
            _responses.Stream(response, 200, "image/png", frame);
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
            WriteRefreshBodyError(response, bodyStatus);
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

    private void WriteRefreshBodyError(
        HttpListenerResponse response,
        int status)
    {
        WriteRequestBodyError(response, status, "refresh");
    }

    private void ServeState(
        HttpListenerContext context,
        int boundPort)
    {
        HttpListenerResponse response = context.Response;
        if (_options.StateEndpoint is not { } stateEndpoint)
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
                "Method not allowed: update preview state with POST.");
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
                "Forbidden: preview state requires this session's exact Origin and CSRF token.");
            return;
        }

        int currentRevision = _options.CurrentSnapshot()?.Revision ?? 0;
        string? revisionHeader = context.Request.Headers[RevisionHeader];
        if (string.IsNullOrEmpty(revisionHeader))
        {
            WriteRevisionRequired(response, currentRevision);
            return;
        }
        if (!TryReadRevision(revisionHeader, out int sourceRevision))
        {
            _responses.Text(
                response,
                400,
                "text/plain; charset=utf-8",
                $"Invalid preview state: {RevisionHeader} must be a non-negative integer.");
            return;
        }

        int bodyStatus = PreviewRequestBody.Read(
            context.Request,
            out byte[] body);
        if (bodyStatus != 0)
        {
            WriteRequestBodyError(response, bodyStatus, "state");
            return;
        }
        if (sourceRevision != currentRevision)
        {
            WriteStateStale(response, sourceRevision, currentRevision);
            return;
        }

        ProductPreviewPayload state;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            BoundedJsonValidation.ValidateNoDuplicateProperties(
                document.RootElement,
                static reason => new JsonException(reason));
            ProductPreviewPayload? parsed = document.RootElement.Deserialize(
                SdkJsonContext.Default.ProductPreviewPayload);
            state = parsed
                ?? throw new JsonException("The preview state is null.");
            stateEndpoint.Validate(state);
        }
        catch (Exception exception) when (
            exception is JsonException or CliException)
        {
            _responses.Text(
                response,
                400,
                "text/plain; charset=utf-8",
                $"Invalid preview state: {exception.Message}");
            return;
        }

        PreviewViewPublicationStore.PreviewViewLease? publication = null;
        try
        {
            publication = stateEndpoint.Publish(state, sourceRevision);
        }
        catch (CliException exception) when (
            exception.Code == ErrorCodes.PreviewStateStale
            || exception.Code == ErrorCodes.PreviewTargetStale)
        {
            WriteCliError(response, 409, exception);
            return;
        }
        catch (Exception exception)
        {
            string code = exception is CliException cli
                ? cli.Code.Name
                : ErrorCodes.Internal.Name;
            _responses.Text(
                response,
                422,
                "text/plain; charset=utf-8",
                $"Preview state render failed ({code}); the current view remains available.");
            return;
        }

        using (publication)
        {
            string viewUrl = _viewRoutePrefix
                + publication.Token
                + "/";
            string encodedViewUrl = JsonEncodedText.Encode(viewUrl).ToString();
            _responses.Text(
                response,
                201,
                "application/json; charset=utf-8",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{{\"revision\":{publication.Snapshot.Revision},\"url\":\"{encodedViewUrl}\"}}"));
        }
    }

    private static bool TryReadRevision(
        string value,
        out int revision) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out revision)
            && revision >= 0;

    private void WriteRevisionRequired(
        HttpListenerResponse response,
        int currentRevision) =>
        _responses.Text(
            response,
            428,
            "application/json; charset=utf-8",
            JsonSerializer.Serialize(
                new ErrorEnvelope
                {
                    Error = new ErrorPayload
                    {
                        Code = ErrorCodes.PreviewRevisionRequired.Name,
                        Message = $"Preview state requires the {RevisionHeader} header.",
                        Hint = "Retry the navigation intent with the current preview revision.",
                        Details = new JsonObject
                        {
                            ["currentRevision"] = currentRevision,
                        },
                    },
                },
                SdkJsonContext.Default.ErrorEnvelope));

    private void WriteStateStale(
        HttpListenerResponse response,
        int requestedRevision,
        int currentRevision) =>
        WriteCliError(
            response,
            409,
            new CliException(
                ErrorCodes.PreviewStateStale,
                $"Preview state revision {requestedRevision} is stale; the current revision is {currentRevision}.",
                hint: "Retry the same navigation intent against the current preview revision.",
                details: new JsonObject
                {
                    ["requestedRevision"] = requestedRevision,
                    ["currentRevision"] = currentRevision,
                }));

    private void WriteCliError(
        HttpListenerResponse response,
        int status,
        CliException exception) =>
        _responses.Text(
            response,
            status,
            "application/json; charset=utf-8",
            JsonSerializer.Serialize(
                exception.ToEnvelope(),
                SdkJsonContext.Default.ErrorEnvelope));

    private void ServeViewRoute(
        HttpListenerContext context,
        string path)
    {
        HttpListenerResponse response = context.Response;
        if (!string.Equals(
                context.Request.HttpMethod,
                "GET",
                StringComparison.OrdinalIgnoreCase))
        {
            response.Headers["Allow"] = "GET";
            _responses.Text(
                response,
                405,
                "text/plain; charset=utf-8",
                "Method not allowed: immutable preview views are read-only.");
            return;
        }

        if (_options.StateEndpoint is not { } stateEndpoint
            || !TryParseViewRoute(
                path,
                out string token,
                out string? assetPath)
            || !stateEndpoint.Publications.TryAcquire(
                token,
                out PreviewViewPublicationStore.PreviewViewLease? lease)
            || lease is null)
        {
            NotFound(response);
            return;
        }

        using (lease)
        {
            PreviewSnapshot snapshot = lease.Snapshot;
            string viewUrl = _viewRoutePrefix + token + "/";
            if (assetPath is not null)
            {
                string relative;
                try
                {
                    relative = Uri.UnescapeDataString(assetPath);
                }
                catch (UriFormatException)
                {
                    NotFound(response);
                    return;
                }

                ServeSnapshotAsset(
                    response,
                    snapshot,
                    relative);
                return;
            }

            if (IsImageView)
            {
                string frameUrl = viewUrl
                    + "asset/"
                    + Uri.EscapeDataString(snapshot.EntryFileName);
                _responses.Text(
                    response,
                    200,
                    "text/html; charset=utf-8",
                    InjectCsrf(
                        PreviewPageComposer.ImageShell(
                            snapshot.Revision,
                            _shell,
                            frameUrl)));
                return;
            }

            (string html, int revision) = ReadDocument(snapshot);
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
    }

    private bool TryParseViewRoute(
        string path,
        out string token,
        out string? assetPath)
    {
        token = string.Empty;
        assetPath = null;
        string remainder = path[_viewRoutePrefix.Length..];
        int separator = remainder.IndexOf('/');
        if (separator < 0)
        {
            return false;
        }

        token = remainder[..separator];
        if (!PreviewViewPublicationStore.IsToken(token))
        {
            return false;
        }

        string suffix = remainder[(separator + 1)..];
        if (suffix.Length == 0)
        {
            return true;
        }
        if (!suffix.StartsWith("asset/", StringComparison.Ordinal)
            || suffix.Length == "asset/".Length)
        {
            return false;
        }

        assetPath = suffix["asset/".Length..];
        return true;
    }

    private void WriteRequestBodyError(
        HttpListenerResponse response,
        int status,
        string operation)
    {
        string message = status == 413
            ? $"Payload too large: the {operation} body is capped at 64 KB."
            : $"Request timeout: the {operation} body was not received in time.";
        _responses.Text(
            response,
            status,
            "text/plain; charset=utf-8",
            message);
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
            + "if(u.origin===location.origin&&(u.pathname==='/live/refresh'||u.pathname==='/live/state')"
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
