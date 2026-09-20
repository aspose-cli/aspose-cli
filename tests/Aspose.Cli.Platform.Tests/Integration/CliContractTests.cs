using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.Commands;
using Aspose.Cli.Sdk.Resources;
using Aspose.Cli.TestKit;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Black-box coverage for the commercial executable contract.</summary>
[Collection("Local service lifecycle")]
public sealed class CliContractTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Version_PrintsVersionAndExitsZero()
    {
        CliResult result = _workspace.Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StdOut));
        Assert.True(char.IsAsciiDigit(result.StdOut.TrimStart()[0]));
    }

    [Fact]
    public void Capabilities_KeepPreviewOwnershipAndLogicalViewsUnambiguous()
    {
        var expectedPreviewViews = new Dictionary<
            string,
            (string DefaultView, string[] Views)>(StringComparer.Ordinal)
        {
            ["cells"] = ("workbook", ["workbook", "sheet"]),
            ["pdf"] = ("pages", ["pages"]),
            ["slides"] = ("slides", ["slides"]),
            ["words"] = ("document", ["document"]),
        };
        CliResult result = _workspace.Run("capabilities", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        JsonNode capabilities = Parse(result.StdOut);
        Assert.Equal(
            "commercial",
            capabilities["edition"]!.GetValue<string>());
        JsonArray products = capabilities["products"]!.AsArray();
        string[] schemas = capabilities["schemas"]!.AsArray()
            .Select(static schema => schema!.GetValue<string>())
            .ToArray();
        string[] ids = products
            .Select(static product => product!["id"]!.GetValue<string>())
            .ToArray();
        Assert.NotEmpty(ids);
        Assert.Equal(
            ids.Length,
            ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            expectedPreviewViews.Keys.Order(StringComparer.Ordinal),
            ids.Order(StringComparer.Ordinal));

        JsonArray commands = capabilities["commands"]!.AsArray();
        Assert.Contains(
            commands,
            static command => command!["path"]!.GetValue<string>() ==
                "aspose-cli preview");
        foreach (JsonNode? product in products)
        {
            string id = product!["id"]!.GetValue<string>();
            string[] verbs = product["verbs"]!.AsArray()
                .Select(static verb => verb!.GetValue<string>())
                .ToArray();
            Assert.DoesNotContain("watch", verbs);
            Assert.DoesNotContain("preview", verbs);
            Assert.Null(product["ops"]);
            JsonArray operations = product["operations"]!.AsArray();
            Assert.NotEmpty(operations);
            foreach (JsonNode? operation in operations)
            {
                string command = operation!["command"]!.GetValue<string>();
                Assert.Contains(
                    product["commands"]!.AsArray(),
                    candidate => candidate!["path"]!.GetValue<string>() ==
                        $"{id} {command}");
                Assert.Contains(
                    operation["inputSchema"]!.GetValue<string>(),
                    schemas);
                Assert.True(
                    operation["atomicByDefault"]!.GetValue<bool>());
                Assert.True(
                    operation["supportsDryRun"]!.GetValue<bool>());
            }

            (string defaultView, string[] views) = expectedPreviewViews[id];
            JsonNode preview = product["preview"]!;
            Assert.Equal(
                defaultView,
                preview["defaultView"]!.GetValue<string>());
            Assert.Equal(
                views,
                preview["views"]!.AsArray()
                    .Select(static view => view!.GetValue<string>()));
            Assert.DoesNotContain(
                commands,
                command => command!["path"]!.GetValue<string>() ==
                    $"aspose-cli {id} watch");
        }
        Assert.DoesNotContain(
            commands,
            static command => command!["path"]!.GetValue<string>() ==
                "aspose-cli preview start");
        Assert.Contains(
            commands,
            static command => command!["path"]!.GetValue<string>() ==
                "aspose-cli license");
        JsonNode rootCommand = commands.Single(command =>
            command!["path"]!.GetValue<string>() == "aspose-cli")!;
        Assert.Contains(
            rootCommand["options"]!.AsArray(),
            option => option!["name"]!.GetValue<string>() == "--license");
    }

    [Theory]
    [InlineData("full")]
    [InlineData("product")]
    [InlineData("command")]
    [InlineData("unknown-product")]
    [InlineData("unknown-command")]
    public void Capabilities_Selection_IsSmallAndActionable(
        string selection)
    {
        CliResult result = selection switch
        {
            "full" => _workspace.Run(
                "capabilities", "--output", "json"),
            "product" => _workspace.Run(
                "capabilities", "cells", "--output", "json"),
            "command" => _workspace.Run(
                "capabilities", "cells", "edit", "--output", "json"),
            "unknown-product" => _workspace.Run(
                "capabilities", "missing", "--output", "json"),
            "unknown-command" => _workspace.Run(
                "capabilities", "cells", "missing", "--output", "json"),
            _ => throw new ArgumentOutOfRangeException(nameof(selection)),
        };

        if (selection.StartsWith("unknown", StringComparison.Ordinal))
        {
            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.StdOut);
            JsonNode error = Parse(result.StdErr)["error"]!;
            Assert.Equal(
                "OPTION_INVALID",
                error["code"]!.GetValue<string>());
            JsonNode details = error["details"]!;
            Assert.Equal(
                selection == "unknown-product"
                    ? "product"
                    : "command",
                details["selection"]!.GetValue<string>());
            Assert.NotEmpty(details["available"]!.AsArray());
            return;
        }

        Assert.Equal(0, result.ExitCode);
        JsonNode json = Parse(result.StdOut);
        JsonArray products = json["products"]!.AsArray();
        if (selection == "full")
        {
            Assert.Equal(4, products.Count);
            return;
        }

        JsonNode product = Assert.Single(products)!;
        Assert.Equal("cells", product["id"]!.GetValue<string>());
        Assert.All(
            json["schemas"]!.AsArray(),
            schema => Assert.True(
                schema!.GetValue<string>().StartsWith(
                    "v2/common/",
                    StringComparison.Ordinal)
                || schema.GetValue<string>().StartsWith(
                    "v2/cells/",
                    StringComparison.Ordinal)));
        Assert.All(
            json["diagnostics"]!.AsArray(),
            diagnostic => Assert.Contains(
                diagnostic!["owner"]!.GetValue<string>(),
                new[] { "common", "cells" }));
        Assert.All(
            json["commands"]!.AsArray(),
            command => Assert.StartsWith(
                "aspose-cli cells",
                command!["path"]!.GetValue<string>(),
                StringComparison.Ordinal));

        if (selection == "command")
        {
            Assert.Equal(
                ["edit"],
                product["verbs"]!.AsArray()
                    .Select(static verb => verb!.GetValue<string>()));
            Assert.All(
                product["operations"]!.AsArray(),
                operation => Assert.Equal(
                    "edit",
                    operation!["command"]!.GetValue<string>()));
            Assert.All(
                product["commands"]!.AsArray(),
                command => Assert.StartsWith(
                    "cells edit",
                    command!["path"]!.GetValue<string>(),
                    StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SchemaOperation_ReturnsExactViewAndUnknownIdsAreActionable()
    {
        CliResult capabilitiesResult = _workspace.Run(
            "capabilities", "cells", "edit", "--output", "json");
        Assert.Equal(0, capabilitiesResult.ExitCode);
        JsonNode capabilities = Parse(capabilitiesResult.StdOut);
        JsonNode operation = capabilities["products"]![0]!["operations"]![0]!;
        string schemaId = operation["inputSchema"]!.GetValue<string>();
        string operationId = operation["id"]!.GetValue<string>();

        CliResult selected = _workspace.Run(
            "schema", schemaId, "--operation", operationId);
        Assert.Equal(0, selected.ExitCode);
        Assert.Equal(string.Empty, selected.StdErr);
        JsonNode selectedSchema = Parse(selected.StdOut);
        Assert.Equal(
            $"https://schemas.aspose.dev/aspose-cli/{schemaId}.schema.json",
            selectedSchema["$id"]!.GetValue<string>());

        CliResult unknown = _workspace.Run(
            "schema", schemaId, "--operation", "__unknown_operation__");
        Assert.Equal(2, unknown.ExitCode);
        Assert.Equal(string.Empty, unknown.StdOut);
        JsonNode error = Parse(unknown.StdErr)["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(
            operationId,
            error["details"]!["available"]!.AsArray()
                .Select(static item => item!.GetValue<string>()));
    }

    [Fact]
    public void SchemaList_UsesEnvelopeAndHumanRenderers()
    {
        CliResult json = _workspace.Run("schema", "--output", "json");
        Assert.Equal(0, json.ExitCode);
        JsonNode list = Parse(json.StdOut);
        Assert.Equal(
            "https://schemas.aspose.dev/aspose-cli/v2/common/schema-list.schema.json",
            list["schema"]!.GetValue<string>());
        Assert.NotEmpty(list["schemas"]!.AsArray());
        Assert.True(
            list["schemas"]!.AsArray().Select(item => item!.GetValue<string>())
                .SequenceEqual(
                    list["schemas"]!.AsArray()
                        .Select(item => item!.GetValue<string>())
                        .Order(StringComparer.Ordinal),
                    StringComparer.Ordinal));

        CliResult table = _workspace.Run("schema", "--output", "table");
        Assert.Equal(0, table.ExitCode);
        Assert.Contains("id", table.StdOut, StringComparison.Ordinal);
        Assert.Contains("v2/common/schema-list", table.StdOut, StringComparison.Ordinal);

        CliResult markdown = _workspace.Run("schema", "--output", "markdown");
        Assert.Equal(0, markdown.ExitCode);
        Assert.Contains("| id |", markdown.StdOut, StringComparison.Ordinal);
        Assert.Contains("v2/common/schema-list", markdown.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void LicenseStatus_ReflectsTheCompiledEngineContract()
    {
        CliResult result = _workspace.Run("license", "status", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        JsonNode json = Parse(result.StdOut);
        Assert.True(json["applicable"]!.GetValue<bool>());
        Assert.Null(json["mode"]);
        Assert.Null(json["source"]);
        Assert.Null(json["license"]);
        Assert.All(json["products"]!.AsArray(), product =>
            Assert.Equal("evaluation", product!["mode"]!.GetValue<string>()));
    }

    [Fact]
    public void UnknownCommand_UsesTheCurrentUsageEnvelope()
    {
        CliResult result = _workspace.Run("frobnicate");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.NotNull(error["details"]!["errors"]!.AsArray());
        string hint = error["hint"]!.GetValue<string>();
        Assert.Contains("aspose-cli --help", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("aspose-cli cells", hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task App_WithADocument_MountsPreviewOnItsLoopbackOrigin()
    {
        string workbook = _workspace.File("app-preview.xlsx");
        CliResult created = _workspace.Run(
            "cells",
            "create",
            workbook,
            "--sheets",
            "Data",
            "--output",
            "json");
        Assert.Equal(0, created.ExitCode);
        CliResult discovered = _workspace.Run(
            "capabilities",
            "--output",
            "json");
        Assert.Equal(0, discovered.ExitCode);
        JsonNode capabilities = Parse(discovered.StdOut);

        try
        {
            CliResult started = _workspace.Run(
                "app",
                "--no-open",
                "--output",
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
            using HttpResponseMessage page =
                await client.GetAsync(launchUri);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.False(page.Headers.Contains("Set-Cookie"));
            string shell = await page.Content.ReadAsStringAsync();
            Assert.Contains("id=\"product-grid\"", shell, StringComparison.Ordinal);
            Assert.Contains("id=\"drop-zone\"", shell, StringComparison.Ordinal);
            Assert.DoesNotContain("license", shell, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("evaluation", shell, StringComparison.OrdinalIgnoreCase);

            CliResult opened = _workspace.Run(
                "app",
                workbook,
                "--no-open",
                "--output",
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
            JsonSchema statusSchema = JsonSchema.FromText(
                SdkSchemaCatalog.Read("v2/common/app-status"));
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
                capabilities["edition"]!.GetValue<string>(),
                statusJson["edition"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(
                statusJson["editionName"]!.GetValue<string>()));
            Assert.Equal(
                "licensed",
                statusJson["experience"]!.GetValue<string>());
            Assert.True(
                statusJson["license"]!["applicable"]!
                    .GetValue<bool>());
            AssertAppProductsMatchCapabilities(
                statusJson["products"]!.AsArray(),
                capabilities["products"]!.AsArray());
            JsonNode recent = Assert.Single(
                statusJson["recentFiles"]!.AsArray())!;
            Assert.Equal("cells", recent["productId"]!.GetValue<string>());
            Assert.Equal("workbook", recent["view"]!.GetValue<string>());

            var previewUri = new Uri(
                statusJson["previewUrl"]!.GetValue<string>());
            Assert.Equal(launchUri.Port, previewUri.Port);
            Assert.Equal("/document", previewUri.AbsolutePath);

            using HttpResponseMessage preview =
                await client.GetAsync(previewUri);
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.False(
                preview.Headers.Contains("X-Frame-Options"));
            Assert.Contains(
                "frame-ancestors 'self'",
                preview.Headers.GetValues(
                    "Content-Security-Policy").Single(),
                StringComparison.Ordinal);

            using HttpResponseMessage rejected =
                await client.PostAsync(
                    "/live/refresh",
                    content: null);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                rejected.StatusCode);

            foreach ((string[] arguments, string route, string path) in new[]
            {
                (new[] { "app", "--welcome", "--no-open", "--output", "json" }, "welcome", "/"),
                (new[] { "app", "--no-open", "--output", "json" }, "home", "/home"),
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
                CliResult activeStatus = _workspace.Run("app", "status", "--output", "json");
                Assert.Equal(0, activeStatus.ExitCode);
                Assert.Equal(route, Parse(activeStatus.StdOut)["route"]!.GetValue<string>());
                using HttpResponseMessage activePage = await client.GetAsync(activeUri);
                Assert.Equal(HttpStatusCode.OK, activePage.StatusCode);
            }
        }
        finally
        {
            CliResult stopped = _workspace.Run(
                "app",
                "stop",
                "--output",
                "json");
            Assert.True(stopped.ExitCode == 0, stopped.StdErr);
            Assert.False(Parse(stopped.StdOut)["running"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task Preview_UrlRemainsUsableAcrossClientsStatusAndReuse()
    {
        string workbook = _workspace.File("preview-url.xlsx");
        CliResult created = _workspace.Run(
            "cells", "create", workbook, "--sheets", "Data", "--output", "json");
        Assert.Equal(0, created.ExitCode);

        string? id = null;
        try
        {
            CliResult started = _workspace.Run("preview", workbook, "--output", "json");
            Assert.Equal(0, started.ExitCode);
            JsonNode session = Parse(started.StdOut);
            id = session["id"]!.GetValue<string>();
            string url = session["url"]!.GetValue<string>();
            Assert.Equal($"/d/{id}/", new Uri(url).AbsolutePath);
            Assert.False(session["reused"]!.GetValue<bool>());

            using var first = new HttpClient(new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false,
            });
            using var second = new HttpClient(new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false,
            });
            foreach (HttpClient client in new[] { first, second, first })
            {
                using HttpResponseMessage response = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.False(response.Headers.Contains("Set-Cookie"));
            }

            CliResult status = _workspace.Run("preview", "status", id, "--output", "json");
            Assert.Equal(0, status.ExitCode);
            JsonNode discovered = Assert.Single(Parse(status.StdOut)["sessions"]!.AsArray())!;
            Assert.Equal(url, discovered["url"]!.GetValue<string>());

            CliResult reused = _workspace.Run("preview", workbook, "--output", "json");
            Assert.Equal(0, reused.ExitCode);
            JsonNode reusedSession = Parse(reused.StdOut);
            Assert.True(reusedSession["reused"]!.GetValue<bool>());
            Assert.Equal(id, reusedSession["id"]!.GetValue<string>());
            Assert.Equal(url, reusedSession["url"]!.GetValue<string>());
            using HttpResponseMessage afterReuse = await second.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, afterReuse.StatusCode);

            // The viewer answers reads only: nothing can be posted to it.
            using HttpResponseMessage refused = await second.PostAsync(url, content: null);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, refused.StatusCode);
        }
        finally
        {
            if (id is not null)
            {
                CliResult stopped = _workspace.Run("preview", "stop", id, "--output", "json");
                Assert.Equal(0, stopped.ExitCode);
                Assert.Equal(id, Assert.Single(Parse(stopped.StdOut)["stopped"]!.AsArray())!.GetValue<string>());
            }
        }
    }

    [Fact]
    public void Doctor_ReportsTheCurrentDevelopmentEnvironment()
    {
        CliResult result = _workspace.Run("doctor", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        JsonArray checks = Parse(result.StdOut)["checks"]!.AsArray();
        string[] names = checks
            .Select(static item => item!["name"]!.GetValue<string>())
            .ToArray();
        Assert.Contains("cli", names);
        Assert.Contains("runtime", names);
        Assert.Contains("resource-budgets", names);
        Assert.Contains("license", names);
        Assert.Contains("output", names);
    }

    [Fact]
    public void LicenseProvisioning_FollowsTheCompiledEngineContract()
    {
        string invalid = _workspace.File("invalid.lic");
        File.WriteAllText(invalid, "not a license");

        CliResult install = _workspace.Run(
            "license",
            "install",
            invalid,
            "--output",
            "json");
        Assert.Equal(7, install.ExitCode);
        Assert.Equal(
            "LICENSE_INVALID",
            Parse(install.StdErr)["error"]!["code"]!.GetValue<string>());

        CliResult remove = _workspace.Run(
            "license",
            "remove",
            "--output",
            "json");
        Assert.Equal(0, remove.ExitCode);
        Assert.All(Parse(remove.StdOut)["products"]!.AsArray(), product =>
            Assert.Equal("evaluation", product!["mode"]!.GetValue<string>()));
    }

    [Fact]
    public void TimeoutOption_RejectsNonPositiveValues()
    {
        CliResult result = _workspace.Run(
            "doctor",
            "--timeout",
            "0",
            "--output",
            "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains("--timeout", error["message"]!.GetValue<string>());
    }

    [Fact]
    public void UpdateCheck_RequiresConfiguredReleaseTrust()
    {
        string manifest = _workspace.File("RELEASE-MANIFEST.json");
        string signature = _workspace.File("RELEASE-MANIFEST.sig");
        File.WriteAllText(
            manifest,
            """{"schemaVersion":1,"productId":"aspose-cli","edition":"free","runtimeIdentifier":"win-x64","artifactVersion":"99.0.0","sourceRevision":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","archive":{"path":"release.zip","size":0,"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"signature":{"status":"signed","algorithm":"ECDSA-P256-SHA256","keyId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","path":"RELEASE-MANIFEST.sig"}}""",
            System.Text.Encoding.UTF8);
        File.WriteAllText(signature, "AA==", System.Text.Encoding.ASCII);

        CliResult result = _workspace.Run(
            "update",
            "check",
            manifest,
            "--output",
            "json");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal(
            "RELEASE_TRUST_UNAVAILABLE",
            Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void UpdateCheck_RejectsSecretBearingHttpsFeedWithoutEchoingIt()
    {
        CliResult result = _workspace.Run(
            "update",
            "check",
            "https://user:secret@example.invalid/RELEASE-MANIFEST.json?token=secret#secret",
            "--output",
            "json");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal("OPTION_INVALID", Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain("secret", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateInstall_RejectsUnsafeZipEntriesBeforeInstallerHandoff()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = _workspace.File("signed-feed");
        Directory.CreateDirectory(root);
        string archive = Path.Combine(root, "release.zip");
        string escapeName = "aspose-update-escape-" + Guid.NewGuid().ToString("N") + ".txt";
        string escaped = Path.Combine(Path.GetTempPath(), escapeName);
        using (FileStream stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            zip.CreateEntry("../" + escapeName);
        }

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string keyId = Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
        string archiveHash = HashFile(archive);
        string revision = new string('a', 40);
        var links = new[]
        {
            new { product = "cells", packageId = "Test.cells", version = "1.0.0", contentHash = Convert.ToBase64String(Enumerable.Repeat((byte)1, 64).ToArray()) },
            new { product = "pdf", packageId = "Test.pdf", version = "1.0.0", contentHash = Convert.ToBase64String(Enumerable.Repeat((byte)2, 64).ToArray()) },
            new { product = "slides", packageId = "Test.slides", version = "1.0.0", contentHash = Convert.ToBase64String(Enumerable.Repeat((byte)3, 64).ToArray()) },
            new { product = "words", packageId = "Test.words", version = "1.0.0", contentHash = Convert.ToBase64String(Enumerable.Repeat((byte)4, 64).ToArray()) },
        };
        string edition = Aspose.Cli.Sdk.DistributionInfo.Edition;
        byte[] payload = ReleaseManifestVerifier.CreateSigningPayload(
            "aspose-cli", edition, "win-x64", "99.0.0", revision,
            "release.zip", new FileInfo(archive).Length, archiveHash, false,
            links.Select(static link => new ReleaseEnginePackage(link.product, link.packageId, link.version, link.contentHash)),
            "signed", "ECDSA-P256-SHA256", "rfc3279-der", keyId, "RELEASE-MANIFEST.sig");
        File.WriteAllText(
            Path.Combine(root, "RELEASE-MANIFEST.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                productId = "aspose-cli",
                edition,
                runtimeIdentifier = "win-x64",
                artifactVersion = "99.0.0",
                sourceRevision = revision,
                buildDirty = false,
                enginePackages = links,
                archive = new { path = "release.zip", size = new FileInfo(archive).Length, sha256 = archiveHash },
                signature = new { status = "signed", algorithm = "ECDSA-P256-SHA256", format = "rfc3279-der", keyId, path = "RELEASE-MANIFEST.sig" },
            }),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(root, "RELEASE-MANIFEST.sig"),
            Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)),
            Encoding.ASCII);
        string ring = Path.Combine(root, "keys.json");
        File.WriteAllText(
            ring,
            JsonSerializer.Serialize(new { keys = new[] { new { keyId, publicKeyPem = key.ExportSubjectPublicKeyInfoPem() } } }),
            Encoding.UTF8);

        CliResult result = _workspace.RunWithEnv(
            new Dictionary<string, string?> { [ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable] = ring },
            "update", "install", Path.Combine(root, "RELEASE-MANIFEST.json"), "--output", "json");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal("RELEASE_VERIFICATION_FAILED", Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(escaped));
    }

    [Fact]
    public void UpdateCheck_RejectsDifferentIdentityAtEqualSemanticPrecedence()
    {
        string root = _workspace.File("revision-feed");
        Directory.CreateDirectory(root);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string keyId = Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
        string edition = Aspose.Cli.Sdk.DistributionInfo.Edition;
        string revision = new string('b', 40);
        var links = new[]
        {
            new ReleaseEnginePackage("cells", "Test.cells", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)1, 64).ToArray())),
            new ReleaseEnginePackage("pdf", "Test.pdf", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)2, 64).ToArray())),
            new ReleaseEnginePackage("slides", "Test.slides", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)3, 64).ToArray())),
            new ReleaseEnginePackage("words", "Test.words", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)4, 64).ToArray())),
        };
        byte[] payload = ReleaseManifestVerifier.CreateSigningPayload(
            "aspose-cli", edition, "win-x64", "1.0.0+bbbb", revision,
            "release.zip", 0, new string('a', 64), false, links, "signed",
            "ECDSA-P256-SHA256", "rfc3279-der", keyId, "RELEASE-MANIFEST.sig");
        File.WriteAllText(
            Path.Combine(root, "RELEASE-MANIFEST.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                productId = "aspose-cli",
                edition,
                runtimeIdentifier = "win-x64",
                artifactVersion = "1.0.0+bbbb",
                sourceRevision = revision,
                buildDirty = false,
                enginePackages = links.Select(static link => new { product = link.Product, packageId = link.PackageId, version = link.Version, contentHash = link.ContentHash }),
                archive = new { path = "release.zip", size = 0, sha256 = new string('a', 64) },
                signature = new { status = "signed", algorithm = "ECDSA-P256-SHA256", format = "rfc3279-der", keyId, path = "RELEASE-MANIFEST.sig" },
            }),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(root, "RELEASE-MANIFEST.sig"),
            Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)),
            Encoding.ASCII);
        string ring = Path.Combine(root, "keys.json");
        File.WriteAllText(
            ring,
            JsonSerializer.Serialize(new { keys = new[] { new { keyId, publicKeyPem = key.ExportSubjectPublicKeyInfoPem() } } }),
            Encoding.UTF8);

        CliResult result = _workspace.RunWithEnv(
            new Dictionary<string, string?> { [ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable] = ring },
            "update", "check", Path.Combine(root, "RELEASE-MANIFEST.json"), "--output", "json");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal(
            "RELEASE_VERIFICATION_FAILED",
            Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
    }

    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json)
        ?? throw new InvalidOperationException("Output was not JSON:\n" + json);

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

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
            Assert.Equal(
                $"aspose-cli-{id}",
                app["skill"]!["name"]!.GetValue<string>());

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

    [Fact]
    public void App_WithAnOccupiedPort_ReturnsActionableListenerFailure()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        int port = ((IPEndPoint)occupied.LocalEndpoint).Port;

        CliResult result = _workspace.Run(
            "app",
            "--foreground",
            "--port",
            port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--no-open",
            "--output",
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

}
