using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>Black-box coverage for starting, joining and refusing the App service.</summary>
[Collection("Local service lifecycle")]
public sealed class AppCommandTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Category(TestCategory.Slow)]
    [Fact]
    public async Task App_WithADocument_MountsPreviewOnItsLoopbackOrigin()
    {
        string workbook = _workspace.File("app-preview.xlsx");
        CliResult created = _workspace.Run(
            "cells",
            "create",
            workbook,
            "--sheet-names",
            "Data",
            "--output-format",
            "json");
        Assert.Equal(0, created.ExitCode);
        CliResult discovered = _workspace.Run(
            "capabilities",
            "--output-format",
            "json");
        Assert.Equal(0, discovered.ExitCode);
        JsonNode capabilities = Parse(discovered.StdOut);

        try
        {
            CliResult started = _workspace.Run(
                "app",
                "open",
                "--no-open",
                "--output-format",
                "json");
            Assert.Equal(0, started.ExitCode);
            JsonNode startedJson = Parse(started.StdOut);
            Assert.True(startedJson["running"]!.GetValue<bool>());
            Assert.Equal(
                "home",
                startedJson["route"]!.GetValue<string>());
            var launchUri = new Uri(
                startedJson["url"]!.GetValue<string>());

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            };
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(
                    $"http://127.0.0.1:{launchUri.Port}"),
            };
            string? cookie = await LocalServiceEntry.Cookie(launchUri.AbsoluteUri);
            Assert.NotNull(cookie);
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            using HttpResponseMessage page =
                await client.GetAsync(launchUri.AbsolutePath);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.False(page.Headers.Contains("Set-Cookie"));
            string shell = await page.Content.ReadAsStringAsync();
            Assert.Contains("id=\"product-grid\"", shell, StringComparison.Ordinal);
            Assert.Contains("id=\"drop-zone\"", shell, StringComparison.Ordinal);
            Assert.DoesNotContain("license", shell, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("evaluation", shell, StringComparison.OrdinalIgnoreCase);

            CliResult opened = _workspace.Run(
                "app",
                "open", workbook,
                "--no-open",
                "--output-format",
                "json");
            Assert.Equal(0, opened.ExitCode);
            JsonNode openedJson = Parse(opened.StdOut);
            Assert.True(openedJson["reused"]!.GetValue<bool>());
            Assert.Equal(
                launchUri.Port,
                openedJson["port"]!.GetValue<int>());

            using HttpResponseMessage status =
                await client.GetAsync("/api/status");
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            string statusText =
                await status.Content.ReadAsStringAsync();
            JsonNode statusJson = Parse(statusText);
            JsonSchema statusSchema = PublishedSchemas.Schema("v2/common/app-status");
            using JsonDocument statusDocument =
                JsonDocument.Parse(statusText);
            EvaluationResults schemaResult = statusSchema.Evaluate(
                statusDocument.RootElement);
            Assert.True(
                schemaResult.IsValid,
                JsonSerializer.Serialize(schemaResult));

            using HttpResponseMessage repeatedStatus =
                await client.GetAsync("/api/status");
            Assert.Equal(
                HttpStatusCode.OK,
                repeatedStatus.StatusCode);
            Assert.Equal(
                statusText,
                await repeatedStatus.Content.ReadAsStringAsync());

            Assert.Equal(
                Aspose.Cli.Sdk.DistributionInfo.DisplayName,
                statusJson["displayName"]!.GetValue<string>());
            Assert.Null(statusJson["experience"]);
            Assert.Null(statusJson["license"]!["applicable"]);
            AssertAppProductsMatchCapabilities(
                statusJson["products"]!.AsArray(),
                capabilities["products"]!.AsArray());
            // One list of every installable Skill: the platform Skill first,
            // with no product, then one per product naming its owner.
            Assert.Equal(
                capabilities["products"]!.AsArray()
                    .Select(static product =>
                        $"aspose-cli-{product!["id"]!.GetValue<string>()}:{product["id"]!.GetValue<string>()}")
                    .Prepend("aspose-cli-platform:")
                    .ToArray(),
                statusJson["skills"]!.AsArray()
                    .Select(static skill =>
                        $"{skill!["name"]!.GetValue<string>()}:{skill["product"]?.GetValue<string>()}")
                    .ToArray());
            JsonNode recent = Assert.Single(
                statusJson["recentFiles"]!.AsArray())!;
            Assert.Equal("cells", recent["productId"]!.GetValue<string>());
            Assert.Equal("workbook", recent["view"]!.GetValue<string>());

            var previewUri = new Uri(
                statusJson["previewUrl"]!.GetValue<string>());
            Assert.Equal("127.0.0.1", previewUri.Host);
            // The App and the document it frames are one origin.
            Assert.Equal(launchUri.Port, previewUri.Port);
            Assert.Matches("^/d/[0-9a-f]{32}/$", previewUri.AbsolutePath);

            using HttpResponseMessage preview =
                await client.GetAsync(previewUri);
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            // The App frames it from the same origin, and nothing else may.
            Assert.Equal(
                "SAMEORIGIN",
                preview.Headers.GetValues("X-Frame-Options").Single());
            Assert.EndsWith(
                "frame-ancestors 'self'",
                preview.Headers.GetValues(
                    "Content-Security-Policy").Single(),
                StringComparison.Ordinal);

            // The documents answer GET and nothing else; only the App mutates.
            using HttpResponseMessage rejected =
                await client.PostAsync(
                    "/d/refresh",
                    content: null);
            Assert.Equal(
                HttpStatusCode.MethodNotAllowed,
                rejected.StatusCode);

            foreach ((string[] arguments, string route, string path) in new[]
            {
                (new[] { "app", "open", "--welcome", "--no-open", "--output-format", "json" }, "welcome", "/"),
                (new[] { "app", "open", "--no-open", "--output-format", "json" }, "home", "/home"),
            })
            {
                CliResult activated = _workspace.Run(arguments);
                Assert.Equal(0, activated.ExitCode);
                JsonNode active = Parse(activated.StdOut);
                Assert.True(active["reused"]!.GetValue<bool>());
                Assert.Equal(route, active["route"]!.GetValue<string>());
                var activeUri = new Uri(active["url"]!.GetValue<string>());
                Assert.Equal(launchUri.Port, activeUri.Port);
                Assert.Equal(path, activeUri.AbsolutePath);
                CliResult activeStatus = _workspace.Run("app", "status", "--output-format", "json");
                Assert.Equal(0, activeStatus.ExitCode);
                Assert.Equal(route, Parse(activeStatus.StdOut)["route"]!.GetValue<string>());
                using HttpResponseMessage activeEntry = await client.GetAsync(activeUri);
                Assert.Equal(HttpStatusCode.SeeOther, activeEntry.StatusCode);
                Assert.Equal(path, activeEntry.Headers.Location!.AbsolutePath);
                using HttpResponseMessage activePage = await client.GetAsync(activeEntry.Headers.Location);
                Assert.Equal(HttpStatusCode.OK, activePage.StatusCode);
            }
        }
        finally
        {
            CliResult stopped = _workspace.Run(
                "app",
                "stop",
                "--output-format",
                "json");
            Assert.True(stopped.ExitCode == 0, stopped.StdErr);
            Assert.False(Parse(stopped.StdOut)["running"]!.GetValue<bool>());
        }
    }

    /// <summary>
    /// In the foreground the App reports itself as <c>app open</c> does in the background: with the
    /// address, carrying a one-time entry ticket, that lets a browser in.
    /// </summary>
    [Fact]
    public async Task App_InTheForeground_ReportsItsEntryAddress()
    {
        System.Diagnostics.ProcessStartInfo start = _workspace.StartInfo();
        foreach (string argument in new[] { "app", "open", "--foreground", "--no-open", "--output-format", "json" })
        {
            start.ArgumentList.Add(argument);
        }
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = System.Diagnostics.Process.Start(start)!;
        try
        {
            var text = new System.Text.StringBuilder();
            JsonNode? reported = null;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (reported is null && await process.StandardOutput.ReadLineAsync(deadline.Token) is { } line)
            {
                text.AppendLine(line);
                try { reported = JsonNode.Parse(text.ToString()); }
                catch (JsonException) { }
            }
            Assert.NotNull(reported);
            string url = reported["url"]?.GetValue<string>() ?? string.Empty;
            Assert.Matches(@"^http://127\.0\.0\.1:[0-9]+/home\?t=[0-9a-f]{64}$", url);
            Assert.True(reported["running"]!.GetValue<bool>());
            Assert.Equal(new Uri(url).Port, reported["port"]!.GetValue<int>());
            Assert.NotNull(await LocalServiceEntry.Cookie(url));
        }
        finally
        {
            CliResult stopped = _workspace.Run("app", "stop", "--output-format", "json");
            if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                process.Kill(entireProcessTree: true);
            }
            Assert.True(stopped.ExitCode == 0, stopped.StdErr);
        }
    }

    [Fact]
    public void App_WithAnOccupiedPort_ReturnsActionableListenerFailure()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        int port = ((IPEndPoint)occupied.LocalEndpoint).Port;

        CliResult result = _workspace.Run(
            "app",
            "open",
            "--foreground",
            "--port",
            port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--no-open",
            "--output-format",
            "json");

        Assert.Equal(5, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal(
            "LOOPBACK_PORT_IN_USE",
            error["code"]!.GetValue<string>());
        Assert.Equal(
            port,
            error["details"]!["port"]!.GetValue<int>());
        Assert.Contains(
            "--port 0",
            error["hint"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json)
        ?? throw new InvalidOperationException("Output was not JSON:\n" + json);

    private static void AssertAppProductsMatchCapabilities(
        JsonArray appProducts,
        JsonArray capabilityProducts)
    {
        Assert.Equal(capabilityProducts.Count, appProducts.Count);
        foreach (JsonNode? capability in capabilityProducts)
        {
            string id = capability!["id"]!.GetValue<string>();
            JsonNode app = appProducts.Single(candidate =>
                string.Equals(
                    candidate!["id"]!.GetValue<string>(),
                    id,
                    StringComparison.Ordinal))!;
            Assert.Equal(
                capability["verbs"]!.ToJsonString(),
                app["verbs"]!.ToJsonString());
            Assert.Equal(
                capability["preview"]!["defaultView"]!.GetValue<string>(),
                app["preview"]!["defaultView"]!.GetValue<string>());
            Assert.Equal(
                capability["review"]!["defaultView"]!.GetValue<string>(),
                app["review"]!["defaultView"]!.GetValue<string>());
            Assert.Equal(
                capability["review"]!["visualInspectionRequired"]!
                    .GetValue<bool>(),
                app["review"]!["visualInspectionRequired"]!
                    .GetValue<bool>());
            Assert.Equal(
                capability["renderFormats"]!.AsArray().Count > 0
                    ? "rendered"
                    : "semantic",
                app["preview"]!["fidelity"]!.GetValue<string>());
            Assert.Null(app["skill"]);

            JsonArray appFormats = app["formats"]!.AsArray();
            JsonArray capabilityFormats = capability["formats"]!.AsArray();
            Assert.Equal(capabilityFormats.Count, appFormats.Count);
            foreach (JsonNode? format in capabilityFormats)
            {
                string formatId = format!["id"]!.GetValue<string>();
                JsonNode appFormat = appFormats.Single(candidate =>
                    string.Equals(
                        candidate!["id"]!.GetValue<string>(),
                        formatId,
                        StringComparison.Ordinal))!;
                Assert.Equal(
                    format["extensions"]!.ToJsonString(),
                    appFormat["extensions"]!.ToJsonString());
                Assert.Equal(
                    format["uses"]!.ToJsonString(),
                    appFormat["uses"]!.ToJsonString());
            }
        }
    }
}
