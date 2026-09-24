using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// The App is a local service another web page or another user's tools must not drive:
/// it answers only the exact loopback Host, changes state only for its own page, and
/// obeys control only from the configuration that started it.
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

    [Fact]
    public async Task Mutations_FromAnotherOriginOrWithoutTheCsrfToken_ChangeNothing()
    {
        await using var app = await AppTestSession.Start();
        string origin = app.Url.GetLeftPart(UriPartial.Authority);
        string csrf = app.Client.DefaultRequestHeaders.GetValues(LocalHttpRequestSecurity.CsrfHeader).Single();
        string settings = Path.Combine(app.Workspace.ConfigDirectory, "app-settings.json");
        string? before = File.Exists(settings) ? File.ReadAllText(settings) : null;
        using var bare = new HttpClient(new HttpClientHandler { UseCookies = false })
        {
            BaseAddress = new Uri(origin),
            Timeout = TimeSpan.FromSeconds(30),
        };

        foreach ((string? requestOrigin, string? token) in new (string?, string?)[]
        {
            ("http://attacker.example", csrf),
            ($"http://localhost:{app.Url.Port}", csrf),
            (null, csrf),
            ("null", csrf),
            (origin, null),
            (origin, "0" + csrf[1..]),
            (origin, csrf + "0"),
        })
        {
            foreach (string path in new[] { "/api/preferences", "/api/stop", "/api/local-data/clear" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path)
                {
                    Content = new StringContent(Preferences, Encoding.UTF8, "application/json"),
                };
                if (requestOrigin is not null) { request.Headers.Add("Origin", requestOrigin); }
                if (token is not null) { request.Headers.Add(LocalHttpRequestSecurity.CsrfHeader, token); }
                using HttpResponseMessage response = await bare.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                    $"{path} from '{requestOrigin}' with token '{token is not null}' was answered with {(int)response.StatusCode}: {body}");
                Assert.Equal("CSRF_REJECTED", JsonNode.Parse(body)!["code"]!.GetValue<string>());
            }
        }

        Assert.Equal(before, File.Exists(settings) ? File.ReadAllText(settings) : null);
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());
        // The App's own page, with its origin and token, is still obeyed.
        Assert.True((await app.Preferences("sheets"))["ok"]!.GetValue<bool>());
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
            CliResult stopped = other.Run("app", "stop", "--output", "json");
            Assert.True(stopped.ExitCode == 0, stopped.StdErr);
        }
        Assert.Equal("first.csv", (await app.Status())["file"]!.GetValue<string>());

        // The public marker names the endpoint, but control needs the private token beside it.
        JsonNode marker = JsonNode.Parse(File.ReadAllText(
            Path.Combine(app.Workspace.ConfigDirectory, "viewer", "service.json")))!;
        LocalServiceControlResponse forged = LocalServiceControlServer.Send(
            new LocalServiceControlEndpoint("viewer", marker["id"]!.GetValue<string>()),
            marker["nonce"]!.GetValue<string>(),
            token: "forged-token",
            command: "stop");
        Assert.False(forged.Ok);
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
