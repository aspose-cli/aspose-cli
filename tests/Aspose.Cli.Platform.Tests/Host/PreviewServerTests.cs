using Aspose.Cli.Host.Preview;
using Aspose.Cli.Host.LocalServices;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests.PreviewInfrastructure;

/// <summary>
/// End-to-end behavior of <see cref="PreviewServer"/> over a real
/// <see cref="HttpListener"/> and <see cref="HttpClient"/>: routing, script
/// injection, the Host allow-list, asset containment, port handling, the SSE
/// stream, the image view's shell and frame routes, the shell stylesheet
/// route, and the manual-refresh endpoint. Every server binds port 0 so
/// parallel test classes never collide.
/// </summary>
public sealed class PreviewServerTests
{
    private const string ClientScript = "// aspose-cli preview live client\n";

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x2A];

    [Fact]
    public async Task Start_WithPortZero_BindsARealPortAndServesTheRoot()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub));

        int port = server.Start();

        Assert.InRange(port, 1, ushort.MaxValue);
        using HttpClient client = NewClient(port);
        HttpResponseMessage response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdmissionLimit_RejectsBeforeStartingAdditionalProductWork()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int productCalls = 0;
        PreviewSnapshot? Current()
        {
            Interlocked.Increment(ref productCalls);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return null;
        }

        using var hub = new LiveEventHub();
        using var server = new PreviewServer(
            Options(Current, hub),
            Limits(maximumRequests: 1));
        using HttpClient client = NewClient(server.Start());

        Task<HttpResponseMessage> first = client.GetAsync("/");
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        using HttpResponseMessage rejected =
            await client.GetAsync("/");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            rejected.StatusCode);
        Assert.Equal(1, Volatile.Read(ref productCalls));

        release.Set();
        using HttpResponseMessage completed = await first;
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    [Fact]
    public async Task Root_WithoutASnapshot_ServesTheWaitingPageWithTheLiveClient()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage response = await client.GetAsync("/");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/live/client.js", body, StringComparison.Ordinal);
        Assert.Contains("window.__asposePreview=", body, StringComparison.Ordinal);
        Assert.Contains("\"revision\":0", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_WithAnInlineSnapshot_ServesTheInjectedHtml()
    {
        var snapshot = new PreviewSnapshot(
            Revision: 3,
            DirectoryPath: null,
            EntryFileName: "book.html",
            InlineHtml: "<html><head><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'\"><style>body{margin:0}</style></head><body><p>INLINE-DOC</p></body></html>");
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(() => snapshot, hub));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage response = await client.GetAsync("/");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(
            "nosniff",
            Assert.Single(
                response.Headers.GetValues(
                    "X-Content-Type-Options")));
        Assert.Equal(
            "no-referrer",
            Assert.Single(
                response.Headers.GetValues(
                    "Referrer-Policy")));
        string contentSecurityPolicy = Assert.Single(
            response.Headers.GetValues(
                "Content-Security-Policy"));
        Assert.Contains(
            "frame-ancestors http://127.0.0.1:*",
            contentSecurityPolicy,
            StringComparison.Ordinal);
        Assert.Contains(
            "script-src 'nonce-test-script-nonce'",
            contentSecurityPolicy,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "script-src 'self'",
            contentSecurityPolicy,
            StringComparison.Ordinal);
        Assert.Contains(
            "style-src 'self' 'unsafe-inline'",
            contentSecurityPolicy,
            StringComparison.Ordinal);
        Assert.Equal(
            "same-origin",
            Assert.Single(
                response.Headers.GetValues(
                    "Cross-Origin-Opener-Policy")));
        Assert.Contains("INLINE-DOC", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "http-equiv=\"Content-Security-Policy\"",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "<script nonce=\"test-script-nonce\">window.__asposePreview=",
            body,
            StringComparison.Ordinal);
        Assert.Contains(
            "<script nonce=\"test-script-nonce\" src=\"/live/client.js\" defer></script>",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("stateStorageKey", body, StringComparison.Ordinal);
        Assert.Contains("window.__asposePreview=", body, StringComparison.Ordinal);
        Assert.Contains("\"revision\":3", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_WithHtmlSnapshotUnderProductView_ServesHtmlEntry()
    {
        var snapshot = new PreviewSnapshot(
            Revision: 1,
            DirectoryPath: null,
            EntryFileName: "sheet.html",
            InlineHtml: "<html><head></head><body>SHEET-PREVIEW</body></html>");
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(
            () => snapshot,
            hub,
            view: "sheet"));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage response = await client.GetAsync("/");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("SHEET-PREVIEW", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveClientJs_ReturnsTheConfiguredScript()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage response = await client.GetAsync("/live/client.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/javascript", response.Content.Headers.ContentType?.MediaType);
        string script = await response.Content.ReadAsStringAsync();
        int csrf = script.IndexOf(
            "const f=window.fetch.bind(window)",
            StringComparison.Ordinal);
        int runtime = script.IndexOf(
            "window.AsposePreviewRuntime",
            StringComparison.Ordinal);
        int product = script.IndexOf(
            ClientScript,
            StringComparison.Ordinal);
        Assert.True(csrf >= 0 && csrf < runtime);
        Assert.True(runtime < product);
    }

    [Fact]
    public async Task ShellCss_WhenConfigured_IsServedAndLinkedFromTheDocument()
    {
        var snapshot = new PreviewSnapshot(1, null, "book.html", "<html><body></body></html>");
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(
            () => snapshot, hub, shellStylesheet: "body{margin:0}", documentName: "sales.xlsx", evalMode: true));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage stylesheet = await client.GetAsync("/live/shell.css");
        string css = await stylesheet.Content.ReadAsStringAsync();
        string document = await client.GetStringAsync("/");

        Assert.Equal(HttpStatusCode.OK, stylesheet.StatusCode);
        Assert.Equal("text/css", stylesheet.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", stylesheet.Content.Headers.ContentType?.CharSet);
        // The stylesheet follows the client script's caching stance.
        Assert.True(stylesheet.Headers.CacheControl?.NoStore);
        Assert.Equal("body{margin:0}", css);
        // And every composed page links it and carries the session metadata.
        Assert.Contains("<link rel=\"stylesheet\" href=\"/live/shell.css\">", document, StringComparison.Ordinal);
        Assert.Contains("\"file\":\"sales.xlsx\"", document, StringComparison.Ordinal);
        Assert.Contains("\"eval\":true", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShellCss_WhenNotConfigured_Returns404AndNoLinkIsInjected()
    {
        var snapshot = new PreviewSnapshot(1, null, "book.html", "<html><body></body></html>");
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(() => snapshot, hub));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage stylesheet = await client.GetAsync("/live/shell.css");
        string document = await client.GetStringAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, stylesheet.StatusCode);
        Assert.DoesNotContain("shell.css", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgedHostHeader_IsRejected()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub));
        using HttpClient client = NewClient(server.Start());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "evil.example";

        HttpResponseMessage response = await client.SendAsync(request);

        // The HTTP stack may reject an unmatched prefix before the
        // application runs (Windows: 400, Linux: 404); when dispatched, the
        // in-process exact-host gate answers 403. None may serve the page.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            response.StatusCode,
            new[]
            {
                HttpStatusCode.BadRequest,
                HttpStatusCode.Forbidden,
                HttpStatusCode.NotFound,
            });
    }

    [Fact]
    public async Task UnknownPathsAndNonGetMethods_Return404()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage unknown = await client.GetAsync("/nothing/here");
        HttpResponseMessage post = await client.PostAsync("/live/refresh", new StringContent(""));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    public async Task Asset_InsideTheVersionDirectory_IsServedWithItsMimeType()
    {
        using var temp = new TempDirectory();
        PreviewSnapshot snapshot = DirectorySnapshot(temp);
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(() => snapshot, hub));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage response = await client.GetAsync("/asset/style.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("body{color:teal}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Asset_TraversalAttempt_Returns404WithoutLeakingTheFile()
    {
        using var temp = new TempDirectory();
        PreviewSnapshot snapshot = DirectorySnapshot(temp);
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(() => snapshot, hub));
        using HttpClient client = NewClient(server.Start());

        // "..%2f" survives HttpClient's dot-segment normalization; the server
        // decodes it and must refuse to leave the version directory.
        HttpResponseMessage response = await client.GetAsync("/asset/..%2fsecret.txt");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("TOP-SECRET", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_WhenThePortIsAlreadyBound_ThrowsLoopbackPortInUse()
    {
        var squatter = new TcpListener(IPAddress.Loopback, 0);
        squatter.Start();
        try
        {
            int port = ((IPEndPoint)squatter.LocalEndpoint).Port;
            using var hub = new LiveEventHub();
            using var server = new PreviewServer(Options(static () => null, hub, port));

            CliException error = Assert.Throws<CliException>(() => server.Start());

            Assert.Equal(ErrorCodes.LoopbackPortInUse, error.Code);
        }
        finally
        {
            squatter.Stop();
        }
    }

    [Fact]
    public async Task Stop_IsIdempotent_AndRequestsFailAfterwards()
    {
        using var hub = new LiveEventHub();
        var server = new PreviewServer(Options(static () => null, hub));
        int port = server.Start();
        using HttpClient client = NewClient(port);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        server.Stop();
        server.Stop();
        server.Dispose();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/"));
    }

    [Fact]
    public async Task Stop_WhenARequestIsStillActive_DefersCleanupWithoutThrowing()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        PreviewSnapshot? Current()
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return null;
        }

        using var hub = new LiveEventHub();
        var server = new PreviewServer(Options(Current, hub));
        using HttpClient client = NewClient(server.Start());
        Task<HttpResponseMessage> request = client.GetAsync("/");
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

        Assert.False(server.Stop(TimeSpan.FromMilliseconds(50)));

        release.Set();
        server.WaitUntilStopped();
        server.Dispose();
        try
        {
            using HttpResponseMessage response = await request;
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            // Closing the listener may reset an in-flight client response;
            // the ownership assertion is that the admitted handler drains.
        }
    }

    [Fact]
    public async Task EventsEndpoint_SendsTheHelloFrameAndRelaysBroadcasts()
    {
        var snapshot = new PreviewSnapshot(2, null, "book.html", "<html><body></body></html>");
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(() => snapshot, hub));
        using HttpClient client = NewClient(server.Start());

        using HttpResponseMessage response = await client.GetAsync(
            "/live/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        string afterHello = await ReadUntilAsync(
            stream, "event: hello\ndata: {\"revision\":2,\"view\":\"html\"}\n\n", TimeSpan.FromSeconds(10));
        Assert.Contains("event: hello", afterHello, StringComparison.Ordinal);

        hub.Broadcast("update", "{\"revision\":3,\"scope\":\"document\"}");

        string afterUpdate = await ReadUntilAsync(
            stream, "event: update\ndata: {\"revision\":3,\"scope\":\"document\"}\n\n", TimeSpan.FromSeconds(10));
        Assert.Contains("event: update", afterUpdate, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_WithSessionProof_InvokesTheCallbackAndReturns202WithTheRevision()
    {
        var snapshot = new PreviewSnapshot(4, null, "book.html", "<html><body></body></html>");
        int refreshes = 0;
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(
            () => snapshot, hub, refreshRequested: () => Interlocked.Increment(ref refreshes)));
        int port = server.Start();
        using HttpClient client = NewClient(port);
        using var request =
            new HttpRequestMessage(HttpMethod.Post, "/live/refresh");
        request.Headers.TryAddWithoutValidation(
            "Origin",
            $"http://127.0.0.1:{port}");
        request.Headers.TryAddWithoutValidation(
            "X-CSRF-Token",
            "test-csrf");

        HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"revision\":4}", body);
        Assert.Equal(1, refreshes);
    }

    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("https://evil.example:4680")]
    [InlineData("null")]
    public async Task Refresh_WithANonLoopbackOrigin_Returns403WithoutInvoking(string origin)
    {
        int refreshes = 0;
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(
            static () => null, hub, refreshRequested: () => Interlocked.Increment(ref refreshes)));
        using HttpClient client = NewClient(server.Start());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/live/refresh");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation(
            "X-CSRF-Token",
            "test-csrf");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, refreshes);
    }

    [Fact]
    public async Task Refresh_WithALoopbackOrigin_Returns202WithRevisionZeroBeforeTheFirstRender()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub, refreshRequested: static () => { }));
        int port = server.Start();
        using HttpClient client = NewClient(port);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/live/refresh");
        request.Headers.TryAddWithoutValidation("Origin", $"http://127.0.0.1:{port}");
        request.Headers.TryAddWithoutValidation(
            "X-CSRF-Token",
            "test-csrf");

        HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("{\"revision\":0}", body);
    }

    [Fact]
    public async Task Refresh_WithAGetMethod_Returns405WithAllowPost()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub, refreshRequested: static () => { }));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage response = await client.GetAsync("/live/refresh");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("POST", Assert.Single(response.Content.Headers.Allow));
    }

    [Fact]
    public async Task Refresh_WhenNoHandlerIsWired_Returns404ForEveryMethod()
    {
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(Options(static () => null, hub));
        using HttpClient client = NewClient(server.Start());

        HttpResponseMessage post = await client.PostAsync("/live/refresh", content: null);
        HttpResponseMessage get = await client.GetAsync("/live/refresh");

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("http://127.0.0.1:1", "test-csrf")]
    [InlineData("https://127.0.0.1:{PORT}", "test-csrf")]
    [InlineData("http://localhost:{PORT}", "test-csrf")]
    [InlineData("http://127.0.0.1:{PORT}", null)]
    [InlineData("http://127.0.0.1:{PORT}", "wrong-csrf")]
    public async Task SecuredSession_InvalidMutationProofIsForbidden(
        string? originTemplate,
        string? csrf)
    {
        int refreshes = 0;
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(SecureOptions(
            static () => null,
            hub,
            () => Interlocked.Increment(ref refreshes)));
        int port = server.Start();
        using HttpClient client = NewClient(port);
        using var request =
            new HttpRequestMessage(HttpMethod.Post, "/live/refresh");
        if (originTemplate is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "Origin",
                originTemplate.Replace(
                    "{PORT}",
                    port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.Ordinal));
        }

        if (csrf is not null)
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-Token", csrf);
        }

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, refreshes);
    }

    [Fact]
    public async Task SecuredSession_ExactOriginAndCsrfAuthorizeMutation()
    {
        int refreshes = 0;
        using var hub = new LiveEventHub();
        using var server = new PreviewServer(SecureOptions(
            static () => null,
            hub,
            () => Interlocked.Increment(ref refreshes)));
        int port = server.Start();
        using HttpClient client = NewClient(port);
        using var request =
            new HttpRequestMessage(HttpMethod.Post, "/live/refresh");
        request.Headers.TryAddWithoutValidation(
            "Origin",
            $"http://127.0.0.1:{port}");
        request.Headers.TryAddWithoutValidation(
            "X-CSRF-Token",
            "test-csrf");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, refreshes);
    }

    [Fact]
    public async Task SecuredSession_RejectsAnotherSessionsCsrfToken()
    {
        using var firstHub = new LiveEventHub();
        using var firstServer = new PreviewServer(SecureOptions(
            static () => null,
            firstHub,
            refreshRequested: static () => { },
            csrf: "first-csrf"));
        using var secondHub = new LiveEventHub();
        using var secondServer = new PreviewServer(SecureOptions(
            static () => null,
            secondHub,
            refreshRequested: static () => { },
            csrf: "second-csrf"));
        int firstPort = firstServer.Start();
        int secondPort = secondServer.Start();
        using HttpClient first = NewClient(firstPort);
        using HttpClient second = NewClient(secondPort);
        using var request =
            new HttpRequestMessage(HttpMethod.Post, "/live/refresh");
        request.Headers.TryAddWithoutValidation(
            "Origin",
            $"http://127.0.0.1:{secondPort}");
        request.Headers.TryAddWithoutValidation(
            "X-CSRF-Token",
            "first-csrf");

        HttpResponseMessage response = await second.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static PreviewServerOptions Options(
        Func<PreviewSnapshot?> currentSnapshot,
        LiveEventHub hub,
        int port = 0,
        string view = "html",
        Action? refreshRequested = null,
        string? shellStylesheet = null,
        string? documentName = null,
        bool evalMode = false) =>
        new(
            port,
            new PreviewRequestOptions(
                view,
                ClientScript,
                currentSnapshot,
                hub,
                "test-csrf",
                "test-script-nonce",
                refreshRequested,
                shellStylesheet,
                documentName,
                evalMode));

    private static PreviewServerOptions SecureOptions(
        Func<PreviewSnapshot?> currentSnapshot,
        LiveEventHub hub,
        Action? refreshRequested = null,
        string csrf = "test-csrf")
    {
        PreviewServerOptions options = Options(
            currentSnapshot,
            hub,
            refreshRequested: refreshRequested);
        return options with
        {
            Requests = options.Requests with
            {
                CsrfToken = csrf,
            },
        };
    }

    private static LocalServiceResourceLimits Limits(
        int maximumRequests) => new(
        MaximumConcurrentRequests: maximumRequests,
        MaximumSseClients: 4,
        SseQueueCapacity: 4,
        SseWriteTimeout: TimeSpan.FromSeconds(1),
        MaximumSnapshotFiles: 16,
        MaximumSnapshotFileBytes: 1024,
        MaximumSnapshotBytes: 4096,
        MaximumInlineHtmlBytes: 1024,
        MaximumUploadFiles: 4,
        MaximumUploadSessionBytes: 4096,
        RenderTimeout: TimeSpan.FromMinutes(1));

    private static HttpClient NewClient(int port)
    {
        var client = new HttpClient()
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
        return client;
    }

    /// <summary>
    /// A directory-mode snapshot: <c>v1/book.html</c> plus <c>v1/style.css</c>,
    /// with a <c>secret.txt</c> planted one level above the version directory
    /// for the traversal test.
    /// </summary>
    private static PreviewSnapshot DirectorySnapshot(TempDirectory temp)
    {
        string versionDirectory = temp.File("v1");
        Directory.CreateDirectory(versionDirectory);
        File.WriteAllText(Path.Combine(versionDirectory, "book.html"), "<html><body>DOC</body></html>");
        File.WriteAllText(Path.Combine(versionDirectory, "style.css"), "body{color:teal}");
        File.WriteAllText(temp.File("secret.txt"), "TOP-SECRET");
        return new PreviewSnapshot(1, versionDirectory, "book.html", InlineHtml: null);
    }

    private static async Task<string> ReadUntilAsync(Stream stream, string fragment, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        byte[] buffer = new byte[4096];
        var text = new StringBuilder();
        while (!text.ToString().Contains(fragment, StringComparison.Ordinal))
        {
            int read = await stream.ReadAsync(buffer, cts.Token);
            if (read == 0)
            {
                break;
            }

            text.Append(Encoding.UTF8.GetString(buffer, 0, read));
        }

        string result = text.ToString();
        Assert.Contains(fragment, result, StringComparison.Ordinal);
        return result;
    }
}
