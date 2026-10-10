using System.Net;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.App;

/// <summary>
/// The App's HTTP surface. It owns no listener and no port: the viewer
/// service serves the App and the documents it opens from one loopback
/// origin, so the page that frames a document is the document's own origin.
/// </summary>
internal sealed class AppRequestHandler
{
    private const int MaxJsonBytes = 64 * 1024;
    private readonly AppHost _host;
    private readonly string _csrf = Convert.ToHexString(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    private readonly LocalHttpRequestSecurity _security;
    private readonly JsonSerializerOptions _json = AppJsonContext.Default.Options;
    private readonly AppErrorMessages _messages;

    public AppRequestHandler(AppHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _security = new LocalHttpRequestSecurity(_csrf);
        _messages = new AppErrorMessages(host.Catalog);
    }

    /// <summary>Paths the App answers; everything else belongs to the viewer.</summary>
    public static bool Owns(string path) =>
        path is "/" or "/home" or "/preview" or "/settings" or "/app.css" or "/app.js"
        || path.StartsWith("/api/", StringComparison.Ordinal);

    /// <summary>Answers one request on the viewer service's listener.</summary>
    public void Handle(HttpListenerContext context, int port) =>
        HandleAsync(context, port, CancellationToken.None).GetAwaiter().GetResult();

    private async Task HandleAsync(
        HttpListenerContext context,
        int port,
        CancellationToken cancellationToken)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;
        bool stopAfterResponse = false;
        try
        {
            if (!LocalHttpRequestSecurity.IsRequestAllowed(request, port))
            {
                AddSecurityHeaders(response);
                await WriteError(
                    response,
                    HttpStatusCode.Forbidden,
                    "LOOPBACK_REJECTED",
                    "This local App only accepts exact loopback requests.")
                    .ConfigureAwait(false);
                return;
            }

            string path = request.Url?.AbsolutePath ?? "/";
            AddSecurityHeaders(response);
            if (request.HttpMethod == "GET")
            {
                await HandleGet(path, response).ConfigureAwait(false);
                return;
            }

            if (!_security.IsMutationAuthorized(request, port))
            {
                await WriteError(response, HttpStatusCode.Forbidden, "CSRF_REJECTED", "The request did not come from this local App.").ConfigureAwait(false);
                return;
            }

            _host.Touch();
            (HttpStatusCode status, object body, bool stop) = await HandleMutation(
                path, request, cancellationToken).ConfigureAwait(false);
            stopAfterResponse = stop;
            await WriteJson(response, status, body).ConfigureAwait(false);
        }
        catch (CliException ex)
        {
            AddSecurityHeaders(response);
            await WriteError(response, StatusFor(ex), ex.Code.Name, _messages.For(ex.Code)).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            AddSecurityHeaders(response);
            await WriteError(response, HttpStatusCode.BadRequest, "REQUEST_INVALID", "The request body is not valid JSON.").ConfigureAwait(false);
        }
        catch (Exception)
        {
            AddSecurityHeaders(response);
            await WriteError(response, HttpStatusCode.InternalServerError, "INTERNAL", "The local App could not complete the request. Check Settings for diagnostics.").ConfigureAwait(false);
        }
        finally
        {
            try { response.Close(); }
            finally { if (stopAfterResponse) { _host.RequestStop(); } }
        }
    }

    private async Task HandleGet(string path, HttpListenerResponse response)
    {
        switch (path)
        {
            case "/":
            case "/home":
            case "/preview":
            case "/settings":
                string bootstrap = $"<meta name=\"aspose-csrf\" content=\"{_csrf}\">";
                await WriteText(response, HttpStatusCode.OK, "text/html; charset=utf-8", AppAssets.Html.Replace("__ASPOSE_CLI_BOOTSTRAP__", bootstrap, StringComparison.Ordinal)).ConfigureAwait(false);
                break;
            case "/app.css":
                await WriteText(response, HttpStatusCode.OK, "text/css; charset=utf-8", AppAssets.Css).ConfigureAwait(false);
                break;
            case "/app.js":
                await WriteText(response, HttpStatusCode.OK, "text/javascript; charset=utf-8", AppAssets.JavaScript).ConfigureAwait(false);
                break;
            case "/api/status":
                await WriteJson(response, HttpStatusCode.OK, _host.Status()).ConfigureAwait(false);
                break;
            default:
                await WriteError(response, HttpStatusCode.NotFound, "NOT_FOUND", "That local App page does not exist.").ConfigureAwait(false);
                break;
        }
    }

    private async Task<(HttpStatusCode Status, object Body, bool Stop)> HandleMutation(
        string path, HttpListenerRequest request, CancellationToken cancellationToken)
    {
        if (!_host.LicenseManagementApplicable && path == "/api/license")
        {
            return (HttpStatusCode.NotFound, new AppApiResult(false, "NOT_FOUND",
                "That local App operation does not exist in this build."), false);
        }
        switch ((request.HttpMethod, path))
        {
            case ("POST", "/api/onboarding/continue"):
                _host.CompleteOnboarding();
                break;
            case ("POST", "/api/files/pick"):
                return (HttpStatusCode.OK, _host.PickAndOpen(), false);
            case ("POST", "/api/files/upload"):
                await _host.UploadFileAsync(DecodeFileName(request.Headers["X-File-Name"]),
                    request.InputStream, request.ContentLength64, cancellationToken).ConfigureAwait(false);
                break;
            case ("POST", "/api/license"):
                _host.InstallLicense(request.InputStream, request.ContentLength64,
                    NormalizeProduct(request.Headers["X-Product"]));
                break;
            case ("DELETE", "/api/license"):
                _host.RemoveLicense(NormalizeProduct(request.Headers["X-Product"]));
                break;
            case ("POST", "/api/preferences"):
                AppApiResult preferences = _host.UpdatePreferences(await ReadJson<AppPreferenceRequest>(request).ConfigureAwait(false));
                return (HttpStatusCode.OK, preferences, false);
            case ("POST", "/api/documents/activate"):
                _host.ActivateDocument((await ReadJson<AppIdRequest>(request).ConfigureAwait(false)).Id);
                break;
            case ("POST", "/api/documents/close"):
                _host.CloseDocument((await ReadJson<AppIdRequest>(request).ConfigureAwait(false)).Id);
                break;
            case ("POST", "/api/documents/view"):
                AppDocumentViewRequest shown = await ReadJson<AppDocumentViewRequest>(request).ConfigureAwait(false);
                _host.ShowDocument(shown.Id, shown.View);
                break;
            case ("POST", "/api/recent/open"):
                _host.OpenRecent((await ReadJson<AppIdRequest>(request).ConfigureAwait(false)).Id);
                break;
            case ("POST", "/api/recent/remove"):
                _host.RemoveRecent((await ReadJson<AppIdRequest>(request).ConfigureAwait(false)).Id);
                break;
            case ("POST", "/api/recent/clear"):
                _host.ClearRecent();
                break;
            case ("POST", "/api/local-data/clear"):
                await _host.ClearLocalDataAsync(cancellationToken).ConfigureAwait(false);
                break;
            case ("POST", "/api/stop"):
                _host.PrepareStop();
                return (HttpStatusCode.OK, new AppApiResult(true), true);
            default:
                return (HttpStatusCode.NotFound, new AppApiResult(false, "NOT_FOUND", "That local App action does not exist."), false);
        }
        return (HttpStatusCode.OK, new AppApiResult(true), false);
    }

    private async Task<T> ReadJson<T>(HttpListenerRequest request)
    {
        if (request.ContentLength64 > MaxJsonBytes)
        {
            throw CliErrors.FileTooLarge(request.ContentLength64, MaxJsonBytes);
        }

        using var memory = new MemoryStream();
        await BoundedStreamCopy.CopyAsync(
            request.InputStream,
            memory,
            MaxJsonBytes,
            total => CliErrors.FileTooLarge(
                total,
                MaxJsonBytes)).ConfigureAwait(false);
        memory.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(memory, _json).ConfigureAwait(false)
            ?? throw new JsonException("Request body is empty.");
    }

    private static string DecodeFileName(string? encoded)
    {
        string name = Path.GetFileName(Uri.UnescapeDataString(encoded ?? string.Empty));
        return name.Length == 0 ? "upload" : name;
    }

    private static string? NormalizeProduct(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task WriteJson(HttpListenerResponse response, HttpStatusCode status, object value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), _json);
        await WriteBytes(response, status, "application/json; charset=utf-8", bytes).ConfigureAwait(false);
    }

    private Task WriteError(HttpListenerResponse response, HttpStatusCode status, string code, string message) =>
        WriteJson(response, status, new AppApiResult(false, code, message));

    private static Task WriteText(HttpListenerResponse response, HttpStatusCode status, string contentType, string value) =>
        WriteBytes(response, status, contentType, Encoding.UTF8.GetBytes(value));

    private static async Task WriteBytes(HttpListenerResponse response, HttpStatusCode status, string contentType, byte[] bytes)
    {
        response.StatusCode = (int)status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static HttpStatusCode StatusFor(CliException exception) => exception.Code == ErrorCodes.AppBusy
        ? HttpStatusCode.Conflict : exception.ExitCode switch
    {
        ExitCode.Usage or ExitCode.InputError or ExitCode.ValidationError or ExitCode.FormatError => HttpStatusCode.BadRequest,
        ExitCode.LicenseError => HttpStatusCode.UnprocessableEntity,
        _ => HttpStatusCode.InternalServerError,
    };

    private static void AddSecurityHeaders(HttpListenerResponse response)
        => LocalServiceSecurityHeaders.Apply(
            response,
            LocalServicePageKind.AppShell);
}

/// <summary>
/// What a person reads for an App error. Codes are matched by name, because
/// an error re-raised from a CLI child carries the child's own exit code.
/// </summary>
internal sealed class AppErrorMessages
{
    private readonly Dictionary<ErrorCode, string> _messages;

    public AppErrorMessages(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        string[] products = catalog.Products
            .Select(static product => product.Manifest.DisplayName)
            .ToArray();
        string choices = products.Length switch
        {
            0 => "supported",
            1 => products[0],
            _ => string.Join(", ", products[..^1]) + " or " + products[^1],
        };
        _messages = new Dictionary<ErrorCode, string>(ErrorCodeNameComparer.Instance)
        {
            [ErrorCodes.AppBusy] = "The App is stopping. Start it again to continue.",
            [ErrorCodes.FileNotFound] = "That file is no longer available. Choose it again from the Files page.",
            [ErrorCodes.FileAccessDenied] = "Aspose CLI does not have permission to read that file.",
            [ErrorCodes.FileLocked] = "That file is temporarily locked by another program. Wait for its save to finish and try again.",
            [ErrorCodes.FileCorrupt] = "That file could not be opened. It may be damaged or use a different file type.",
            [ErrorCodes.PasswordRequired] = "That file is password protected. Use the CLI password options to preview it.",
            [ErrorCodes.PasswordInvalid] = "The file password was not accepted.",
            [ErrorCodes.FileTooLarge] = "That file is larger than the configured local processing limit.",
            [ErrorCodes.FormatUnsupported] = $"That file type is not supported. Choose a {choices} file.",
            [ErrorCodes.LicenseInvalid] = "The selected license was rejected. Choose a valid Aspose .lic file or continue in evaluation mode.",
            [ErrorCodes.LicenseFileNotFound] = "A configured license file is missing. Install a replacement here or remove the broken environment setting.",
        };
    }

    public string For(ErrorCode code) =>
        _messages.TryGetValue(code, out string? message)
            ? message
            : "The file could not be opened or rendered. Check that it is a supported, readable document.";

    private sealed class ErrorCodeNameComparer : IEqualityComparer<ErrorCode>
    {
        public static readonly ErrorCodeNameComparer Instance = new();

        public bool Equals(ErrorCode? x, ErrorCode? y) =>
            string.Equals(x?.Name, y?.Name, StringComparison.Ordinal);

        public int GetHashCode(ErrorCode code) => StringComparer.Ordinal.GetHashCode(code.Name);
    }
}
