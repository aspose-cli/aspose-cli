using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.Preview;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Host.App;

/// <summary>Loopback-only HTTP surface for the embedded local Web App.</summary>
internal sealed class AppHttpServer : IDisposable
{
    internal static readonly TimeSpan DefaultStopTimeout =
        TimeSpan.FromSeconds(2);

    private const int MaxJsonBytes = 64 * 1024;
    private readonly AppHost _host;
    private readonly int _requestedPort;
    private readonly string _csrf = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    private readonly LocalHttpRequestSecurity _security;
    private readonly HttpRequestGate _requests;
    private readonly JsonSerializerOptions _json =
        AppJsonContext.Default.Options;
    private HttpListener? _listener;
    private CancellationTokenSource? _shutdown;
    private Task? _acceptLoop;
    private int _stopped;
    private int _disposed;

    public AppHttpServer(
        AppHost host,
        int requestedPort)
    {
        _host = host;
        _requestedPort = requestedPort;
        _security = new LocalHttpRequestSecurity(_csrf);
        _requests = new HttpRequestGate(
            LocalServiceResourceLimits.Resolve()
                .MaximumConcurrentRequests);
    }

    public int Port { get; private set; }

    public int Start()
    {
        if (_listener is not null)
        {
            return Port;
        }

        LoopbackHttpListenerBinding binding = LoopbackHttpListenerBinder.Start(
            _requestedPort,
            ["127.0.0.1"]);
        Port = binding.Port;
        HttpListener listener = binding.Listener;

        _listener = listener;
        _shutdown = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoop(listener, _shutdown.Token));
        return Port;
    }

    public void Stop()
    {
        _ = Stop(DefaultStopTimeout);
    }

    internal bool Stop(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        var elapsed = Stopwatch.StartNew();
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
        {
            _requests.StopAccepting();
            _shutdown?.Cancel();
            CloseListener();
        }

        TimeSpan remaining = Remaining(timeout, elapsed.Elapsed);
        if (!WaitForAcceptLoop(remaining))
        {
            return false;
        }

        remaining = Remaining(timeout, elapsed.Elapsed);
        return _requests.WaitForDrain(remaining);
    }

    internal void WaitUntilStopped()
    {
        _ = WaitForAcceptLoop(Timeout.InfiniteTimeSpan);
        _ = _requests.WaitForDrain(Timeout.InfiniteTimeSpan);
    }

    private void CloseListener()
    {
        HttpListener? listener = Interlocked.Exchange(ref _listener, null);
        if (listener is null)
        {
            return;
        }

        try
        {
            listener.Stop();
            listener.Close();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or AggregateException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        DeferredResourceCleanup.CompleteOrDefer(
            Stop(DefaultStopTimeout),
            WaitUntilStopped,
            DisposeStoppedResources,
            "aspose-app-http-cleanup",
            "App HTTP server");
    }

    private void DisposeStoppedResources()
    {
        _shutdown?.Dispose();
        _requests.Dispose();
    }

    private static TimeSpan Remaining(TimeSpan timeout, TimeSpan elapsed) =>
        elapsed >= timeout ? TimeSpan.Zero : timeout - elapsed;

    private bool WaitForAcceptLoop(TimeSpan timeout)
    {
        try
        {
            return _acceptLoop?.Wait(timeout) ?? true;
        }
        catch (AggregateException exception)
        {
            Trace.TraceWarning(
                "The App accept loop ended unexpectedly: {0}",
                exception.GetBaseException().GetType().Name);
            return true;
        }
    }

    private async Task AcceptLoop(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
            {
                return;
            }

            if (!_requests.TryEnter(out IDisposable? lease))
            {
                AddSecurityHeaders(context.Response);
                context.Response.StatusCode =
                    (int)HttpStatusCode.ServiceUnavailable;
                context.Response.Close();
                continue;
            }

            _ = Task.Run(
                async () =>
                {
                    using (lease)
                    {
                        await Handle(
                            context,
                            cancellationToken).ConfigureAwait(false);
                    }
                },
                CancellationToken.None);
        }
    }

    private async Task Handle(
        HttpListenerContext context,
        CancellationToken cancellationToken)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;
        bool previewOwnsResponse = false;
        bool stopAfterResponse = false;
        try
        {
            if (!LocalHttpRequestSecurity.IsRequestAllowed(request, Port))
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

            if (!_security.IsMutationAuthorized(request, Port))
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
            await WriteError(response, StatusFor(ex), ex.Code.Name, FriendlyMessage(ex)).ConfigureAwait(false);
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
            try { if (!previewOwnsResponse) { response.Close(); } }
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
            case "/api/health":
                await WriteJson(
                    response,
                    HttpStatusCode.OK,
                    new AppHealthResult(
                        true,
                        Environment.ProcessId))
                    .ConfigureAwait(false);
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
                string installed = _host.InstallLicense(request.InputStream, request.ContentLength64,
                    NormalizeProduct(request.Headers["X-Product"]));
                return (HttpStatusCode.OK, new AppLicenseSavedResult(true, installed), true);
            case ("DELETE", "/api/license"):
                string removed = _host.RemoveLicense(NormalizeProduct(request.Headers["X-Product"]));
                return (HttpStatusCode.OK, new AppLicenseSavedResult(true, removed), true);
            case ("POST", "/api/preferences"):
                AppApiResult preferences = _host.UpdatePreferences(await ReadJson<AppPreferenceRequest>(request).ConfigureAwait(false));
                return (HttpStatusCode.OK, preferences, false);
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

    private static string FriendlyMessage(CliException exception) => exception.Code.Name switch
    {
        "APP_BUSY" => "The App is stopping. Start it again to continue.",
        "FILE_NOT_FOUND" => "That file is no longer available. Choose it again from the Files page.",
        "FILE_ACCESS_DENIED" => "Aspose CLI does not have permission to read that file.",
        "FILE_LOCKED" => "That file is temporarily locked by another program. Wait for its save to finish and try again.",
        "FILE_CORRUPT" => "That file could not be opened. It may be damaged or use a different file type.",
        "PASSWORD_REQUIRED" => "That file is password protected. Use the CLI password options to preview it.",
        "PASSWORD_INVALID" => "The file password was not accepted.",
        "FILE_TOO_LARGE" => "That file is larger than the configured local processing limit.",
        "FORMAT_UNSUPPORTED" => "That file type is not supported. Choose a supported spreadsheet or word-processing document.",
        "LICENSE_INVALID" => "The selected license was rejected. Choose a valid Aspose .lic file or continue in evaluation mode.",
        "LICENSE_FILE_NOT_FOUND" => "A configured license file is missing. Install a replacement here or remove the broken environment setting.",
        _ => "The file could not be opened or rendered. Check that it is a supported, readable document.",
    };

    private static void AddSecurityHeaders(HttpListenerResponse response)
        => LocalServiceSecurityHeaders.Apply(
            response,
            LocalServicePageKind.AppShell);

}
