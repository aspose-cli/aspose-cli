using System.Net;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The App is a local service another web page or another user's tools must not drive:
/// it answers only the exact loopback Host and a browser that entered through an address the
/// CLI returned, changes state only for its own page, and obeys control only from the
/// configuration that started it.
/// </summary>
[Collection("Local service lifecycle")]
public sealed class AppHttpSecurityTests
{
    private const string Preferences = """{"product":"cells","defaultView":"sheets","rememberRecentFiles":true}""";

    [Fact]
    public async Task Requests_WithoutTheExactLoopbackHost_AreRefused()
    {
        await using var app = await AppTestSession.Start();
        int port = app.Url.Port;

        foreach (string host in new[] { $"localhost:{port}", $"[::1]:{port}", $"127.0.0.1.:{port}", "attacker.example", $"127.0.0.1:{port + 1}" })
        {
            (int status, string body) = await Raw(port, $"GET /api/status HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n");
            // HTTP.sys refuses some spellings itself (400); the service refuses the rest (403).
            Assert.True(status is 400 or 403, $"Host '{host}' was answered with {status}.");
            Assert.DoesNotContain("previewUrl", body, StringComparison.Ordinal);
        }
        (int missing, _) = await Raw(port, "GET /api/status HTTP/1.0\r\n\r\n");
        Assert.True(missing is 400 or 403, $"A request without Host was answered with {missing}.");

        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
    }

    /// <summary>
    /// Every route the service answers is behind its session: the App's pages, assets and API,
    /// and each document's page, event stream and files. Knowing the port and a document id is
    /// not enough, and neither is a cookie the service did not grant.
    /// </summary>
    [Fact]
    public async Task EveryRoute_WithoutTheSessionCookie_IsRefused()
    {
        await using var app = await AppTestSession.Start();
        string origin = app.Url.GetLeftPart(UriPartial.Authority);
        string document = new Uri((await app.Status())["previewUrl"]!.GetValue<string>()).AbsolutePath;
        Assert.Matches("^/d/[0-9a-f]{32}/$", document);
        string settings = Path.Combine(app.Workspace.ConfigDirectory, "app-settings.json");
        string? before = File.Exists(settings) ? File.ReadAllText(settings) : null;
        (HttpMethod Method, string Path)[] routes =
        [
            (HttpMethod.Get, "/"),
            (HttpMethod.Get, "/home"),
            (HttpMethod.Get, "/preview"),
            (HttpMethod.Get, "/settings"),
            (HttpMethod.Get, "/app.css"),
            (HttpMethod.Get, "/app.js"),
            (HttpMethod.Get, "/api/status"),
            (HttpMethod.Get, "/api/unknown"),
            (HttpMethod.Get, "/unknown"),
            (HttpMethod.Get, document),
            (HttpMethod.Get, document.TrimEnd('/')),
            (HttpMethod.Get, document + "events"),
            (HttpMethod.Get, document + "r/1/view.json"),
            (HttpMethod.Get, document + "p/1"),
            (HttpMethod.Get, "/d/00000000000000000000000000000000/"),
            (HttpMethod.Post, "/api/onboarding/continue"),
            (HttpMethod.Post, "/api/files/pick"),
            (HttpMethod.Post, "/api/files/upload"),
            (HttpMethod.Post, "/api/license"),
            (HttpMethod.Delete, "/api/license"),
            (HttpMethod.Post, "/api/preferences"),
            (HttpMethod.Post, "/api/documents/activate"),
            (HttpMethod.Post, "/api/documents/close"),
            (HttpMethod.Post, "/api/documents/view"),
            (HttpMethod.Post, "/api/recent/open"),
            (HttpMethod.Post, "/api/recent/remove"),
            (HttpMethod.Post, "/api/recent/clear"),
            (HttpMethod.Post, "/api/local-data/clear"),
            (HttpMethod.Post, "/api/stop"),
            (HttpMethod.Post, document),
        ];
        using var bare = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(origin),
            Timeout = TimeSpan.FromSeconds(30),
        };
        string cookieName = $"aspose-cli-{app.Url.Port}";

        foreach (string? cookie in new[] { null, $"{cookieName}={new string('0', 64)}", $"{cookieName}=" })
        {
            foreach ((HttpMethod method, string path) in routes)
            {
                using var request = new HttpRequestMessage(method, path);
                // The page's own Origin: the session alone decides.
                request.Headers.Add("Origin", origin);
                if (cookie is not null) { request.Headers.Add("Cookie", cookie); }
                if (method != HttpMethod.Get)
                {
                    request.Content = new StringContent(Preferences, Encoding.UTF8, "application/json");
                }
                using HttpResponseMessage response = await bare.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                    $"{method} {path} with cookie '{cookie}' was answered with {(int)response.StatusCode}.");
                Assert.False(response.Headers.Contains("Set-Cookie"));
                string body = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("first.csv", body, StringComparison.Ordinal);
            }
        }

        Assert.Equal(before, File.Exists(settings) ? File.ReadAllText(settings) : null);
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
    }

    /// <summary>
    /// The address the CLI returns carries a one-time entry ticket. The first browser to open it
    /// gets an HttpOnly, SameSite=Strict session cookie and is sent on to the same address
    /// without the ticket; the same ticket admits nobody else.
    /// </summary>
    [Fact]
    public async Task EntryTicket_GrantsOneSessionOnce()
    {
        await using var app = await AppTestSession.Start();
        int port = app.Url.Port;
        string entry = Url(app.Workspace.Run("app", "status", "--output-format", "json"));
        Assert.Matches($"^http://127\\.0\\.0\\.1:{port}/preview\\?t=[0-9a-f]{{64}}$", entry);
        using var bare = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        string cookie;
        using (HttpResponseMessage admitted = await bare.GetAsync(entry))
        {
            Assert.Equal(HttpStatusCode.SeeOther, admitted.StatusCode);
            Assert.Equal($"http://127.0.0.1:{port}/preview", admitted.Headers.Location!.OriginalString);
            string setCookie = Assert.Single(admitted.Headers.GetValues("Set-Cookie"));
            string[] attributes = setCookie.Split(';', StringSplitOptions.TrimEntries);
            Assert.Matches($"^aspose-cli-{port}=[0-9a-f]{{64}}$", attributes[0]);
            Assert.Contains("HttpOnly", attributes);
            Assert.Contains("SameSite=Strict", attributes);
            Assert.Contains("Path=/", attributes);
            cookie = attributes[0];
        }
        using (var page = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/preview"))
        {
            page.Headers.Add("Cookie", cookie);
            using HttpResponseMessage shown = await bare.SendAsync(page);
            Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        }
        using (var status = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/status"))
        {
            status.Headers.Add("Cookie", cookie);
            using HttpResponseMessage shown = await bare.SendAsync(status);
            Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        }

        // Spent: another browser cannot use it, nor can a made-up ticket.
        foreach (string replay in new[] { entry, entry[..^1] + (entry[^1] == '0' ? '1' : '0') })
        {
            using HttpResponseMessage refused = await bare.GetAsync(replay);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.False(refused.Headers.Contains("Set-Cookie"));
        }
        // The browser that holds the session opens the printed address again and simply goes on.
        using (var again = new HttpRequestMessage(HttpMethod.Get, entry))
        {
            again.Headers.Add("Cookie", cookie);
            using HttpResponseMessage kept = await bare.SendAsync(again);
            Assert.Equal(HttpStatusCode.SeeOther, kept.StatusCode);
            Assert.False(kept.Headers.Contains("Set-Cookie"));
        }

        // A document's address works the same way, and `preview status` mints a new one each time.
        CliResult listed = app.Workspace.Run("preview", "status", "--output-format", "json");
        Assert.True(listed.ExitCode == 0, listed.StdErr);
        string document = JsonNode.Parse(listed.StdOut)!["sessions"]![0]!["url"]!.GetValue<string>();
        Assert.Matches($"^http://127\\.0\\.0\\.1:{port}/d/[0-9a-f]{{32}}/\\?t=[0-9a-f]{{64}}$", document);
        string? documentCookie = await LocalServiceEntry.Cookie(document);
        Assert.NotNull(documentCookie);
        using (var shown = new HttpRequestMessage(HttpMethod.Get, document[..document.IndexOf('?', StringComparison.Ordinal)]))
        {
            shown.Headers.Add("Cookie", documentCookie);
            using HttpResponseMessage response = await bare.SendAsync(shown);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>
    /// A state change needs the service's exact Origin as well as the session, and the App's
    /// page carries no token a script could read.
    /// </summary>
    [Fact]
    public async Task Mutations_FromAnotherOrigin_ChangeNothing()
    {
        await using var app = await AppTestSession.Start();
        string origin = app.Url.GetLeftPart(UriPartial.Authority);
        string? cookie = app.Client.DefaultRequestHeaders.TryGetValues("Cookie", out IEnumerable<string>? held)
            ? held.Single()
            : null;
        string settings = Path.Combine(app.Workspace.ConfigDirectory, "app-settings.json");
        string? before = File.Exists(settings) ? File.ReadAllText(settings) : null;
        using var bare = new HttpClient(new HttpClientHandler { UseCookies = false })
        {
            BaseAddress = new Uri(origin),
            Timeout = TimeSpan.FromSeconds(30),
        };

        foreach (string? requestOrigin in new[]
        {
            "http://attacker.example",
            $"http://localhost:{app.Url.Port}",
            $"http://127.0.0.1:{app.Url.Port + 1}",
            $"https://127.0.0.1:{app.Url.Port}",
            origin + "/",
            "null",
            null,
        })
        {
            foreach (string path in new[] { "/api/preferences", "/api/stop", "/api/local-data/clear" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path)
                {
                    Content = new StringContent(Preferences, Encoding.UTF8, "application/json"),
                };
                if (cookie is not null) { request.Headers.Add("Cookie", cookie); }
                if (requestOrigin is not null) { request.Headers.Add("Origin", requestOrigin); }
                using HttpResponseMessage response = await bare.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                    $"{path} from '{requestOrigin}' was answered with {(int)response.StatusCode}: {body}");
                Assert.Equal("ORIGIN_REJECTED", JsonNode.Parse(body)!["code"]!.GetValue<string>());
            }
        }

        Assert.Equal(before, File.Exists(settings) ? File.ReadAllText(settings) : null);
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
        // No anti-forgery token is handed to the page or read by its script.
        foreach (string asset in new[] { "/", "/preview", "/app.js" })
        {
            string text = await app.Client.GetStringAsync(asset);
            Assert.DoesNotContain("csrf", text, StringComparison.OrdinalIgnoreCase);
        }
        // The App's own page, with its session and origin, is still obeyed.
        Assert.True((await app.Preferences("sheets"))["ok"]!.GetValue<bool>());
    }

    /// <summary>
    /// The App shell embeds no plugin, cannot be framed by any page, and lends its resources to
    /// no other origin.
    /// </summary>
    [Fact]
    public async Task AppShell_CannotBeFramedAndLoadsNoPlugin()
    {
        await using var app = await AppTestSession.Start();

        using HttpResponseMessage shell = await app.Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, shell.StatusCode);
        string policy = Header(shell, "Content-Security-Policy");
        Assert.Contains("object-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Equal("DENY", Header(shell, "X-Frame-Options"));
        Assert.Equal("same-origin", Header(shell, "Cross-Origin-Resource-Policy"));
    }

    /// <summary>
    /// A request body beyond the service's bound is refused whether it declares its length or
    /// streams in chunks, and changes nothing.
    /// </summary>
    [Fact]
    public async Task Mutations_WithAnOversizedBody_AreRefused()
    {
        await using var app = await AppTestSession.Start();
        string settings = Path.Combine(app.Workspace.ConfigDirectory, "app-settings.json");
        string? before = File.Exists(settings) ? File.ReadAllText(settings) : null;
        string oversized = Preferences[..^1] + ",\"padding\":\"" + new string('x', 1024 * 1024) + "\"}";

        foreach (bool chunked in new[] { false, true })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/preferences")
            {
                Content = new StringContent(oversized, Encoding.UTF8, "application/json"),
            };
            request.Headers.TransferEncodingChunked = chunked;
            try
            {
                using HttpResponseMessage response = await app.Client.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                    $"An oversized body (chunked: {chunked}) was answered with {(int)response.StatusCode}: {body}");
                Assert.Equal("FILE_TOO_LARGE", JsonNode.Parse(body)!["code"]!.GetValue<string>());
            }
            catch (HttpRequestException)
            {
                // The service may close the connection while the rest of the body is still being sent.
            }
        }

        Assert.Equal(before, File.Exists(settings) ? File.ReadAllText(settings) : null);
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
    }

    private static string Header(HttpResponseMessage response, string name) =>
        string.Join(", ", response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values
            : response.Content.Headers.TryGetValues(name, out IEnumerable<string>? content) ? content : []);

    private static string Url(CliResult result)
    {
        Assert.True(result.ExitCode == 0, result.StdErr);
        return JsonNode.Parse(result.StdOut)!["url"]!.GetValue<string>();
    }

    [Fact]
    public async Task Listener_IsBoundOnlyToTheIPv4Loopback()
    {
        await using var app = await AppTestSession.Start();
        int port = app.Url.Port;
        Assert.Equal(IPAddress.Loopback, IPAddress.Parse(app.Url.Host));

        Assert.False(await Accepts(IPAddress.IPv6Loopback, port), "The App accepted a connection on [::1].");
        IPAddress? external = (await Dns.GetHostAddressesAsync(Dns.GetHostName()))
            .FirstOrDefault(static address => !IPAddress.IsLoopback(address)
                && address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6);
        if (external is not null)
        {
            Assert.False(await Accepts(external, port), $"The App accepted a connection on {external}.");
        }
        Assert.True(await Accepts(IPAddress.Loopback, port));
    }

    [Fact]
    public async Task ServiceControl_FromAnotherConfigurationOrWithoutItsSecret_IsRefused()
    {
        await using var app = await AppTestSession.Start();

        // Another configuration has its own service; it neither sees nor stops this one.
        using (var other = new TempWorkspace())
        {
            CliResult stopped = other.Run("app", "stop", "--output-format", "json");
            Assert.True(stopped.ExitCode == 0, stopped.StdErr);
        }
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());

        // The public marker names the endpoint, but control needs the private token beside it.
        // The refusal is judged, not its speed: a loaded test machine once took longer than the
        // default 5 s for the exchange.
        JsonNode marker = JsonNode.Parse(File.ReadAllText(
            Path.Combine(app.Workspace.ConfigDirectory, "viewer", "service.json")))!;
        // A closed connection is a refusal too: the service gives each connection a fixed stage
        // budget against slow callers, and on a loaded machine that budget can run out before it
        // replies. Either way the forged command must not have run, which the status shows.
        try
        {
            LocalServiceControlResponse forged = LocalServiceControlServer.Send(
                new LocalServiceControlEndpoint("viewer", marker["id"]!.GetValue<string>()),
                marker["nonce"]!.GetValue<string>(),
                token: "forged-token",
                command: "stop",
                timeout: TimeSpan.FromSeconds(30));
            Assert.False(forged.Ok);
        }
        catch (IOException)
        {
            // The service closed the connection without executing the request (EndOfStreamException included).
        }
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
    }

    /// <summary>
    /// The secret that proves control of the service is readable only by the current user, even
    /// in a configuration directory that lets everyone read what is created inside it.
    /// </summary>
    [Fact]
    public void ServiceSecret_InASharedConfigurationDirectory_IsReadableOnlyByTheCurrentUser()
    {
        Requires.Windows();
        using var workspace = new TempWorkspace();
        AccessRules.ShareForReading(workspace.ConfigDirectory);
        File.WriteAllText(workspace.File("first.csv"), "Label,Value\nFIRST_DOCUMENT,42\n");

        CliResult opened = workspace.Run("app", "open", workspace.File("first.csv"), "--no-open", "--output-format", "json");
        try
        {
            Assert.True(opened.ExitCode == 0, opened.StdErr);
            AccessRules.AssertCurrentUserOnly(Path.Combine(workspace.ConfigDirectory, "viewer", "service.secret"));
        }
        finally
        {
            workspace.Run("app", "stop", "--output-format", "json");
        }
    }

    /// <summary>
    /// The service's control pipe is owned by the current user and grants nobody else access, so
    /// another user of the machine cannot even connect to send a command.
    /// </summary>
    [Fact]
    public async Task ControlPipe_GrantsAccessOnlyToTheCurrentUser()
    {
        await using var app = await AppTestSession.Start();
        JsonNode marker = JsonNode.Parse(File.ReadAllText(
            Path.Combine(app.Workspace.ConfigDirectory, "viewer", "service.json")))!;
        var endpoint = new LocalServiceControlEndpoint("viewer", marker["id"]!.GetValue<string>());
        using WindowsIdentity current = WindowsIdentity.GetCurrent();

        PipeSecurity security;
        using (var pipe = new NamedPipeClientStream(".", endpoint.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly))
        {
            await pipe.ConnectAsync(30_000);
            security = pipe.GetAccessControl();
        }

        // PipeOptions.CurrentUserOnly owns the pipe by, and grants it to, the token's owner: the
        // user, or for an elevated administrator (as on CI runners) the Administrators group, whose
        // elevated members already control the machine. No other account is admitted.
        IdentityReference?[] self = [current.User, current.Owner];
        Assert.Contains(security.GetOwner(typeof(SecurityIdentifier)), self);
        AuthorizationRule[] rules = [.. security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<AuthorizationRule>()];
        Assert.NotEmpty(rules);
        Assert.All(rules, rule =>
        {
            var access = Assert.IsType<PipeAccessRule>(rule);
            Assert.True(access.AccessControlType == AccessControlType.Deny || self.Contains(access.IdentityReference),
                $"The control pipe allows {access.IdentityReference} {access.PipeAccessRights}.");
        });
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
    }

    private static async Task<(int Status, string Body)> Raw(int port, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string response = await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(30));
        string[] status = response.Split("\r\n", 2)[0].Split(' ');
        int separator = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        return (int.Parse(status[1], System.Globalization.CultureInfo.InvariantCulture),
            separator < 0 ? string.Empty : response[(separator + 4)..]);
    }

    private static async Task<bool> Accepts(IPAddress address, int port)
    {
        using var client = new TcpClient(address.AddressFamily);
        try
        {
            await client.ConnectAsync(address, port).WaitAsync(TimeSpan.FromSeconds(5));
            return true;
        }
        catch (Exception exception) when (exception is SocketException or TimeoutException)
        {
            return false;
        }
    }
}
