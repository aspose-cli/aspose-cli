using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Black-box coverage for the commercial executable contract.</summary>
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
            ["cells"] = ("workbook", ["sheets", "workbook"]),
            ["pdf"] = ("pages", ["pages"]),
            ["slides"] = ("slides", ["slides"]),
            ["words"] = ("pages", ["pages"]),
        };
        CliResult result = _workspace.Run("capabilities", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        JsonNode capabilities = Parse(result.StdOut);
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
            Assert.DoesNotContain("preview", verbs);
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
                Assert.True(operation["maximumOperations"]!.GetValue<int>() > 0);
                Assert.NotEmpty(operation["ops"]!.AsArray());
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
        }
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

    [Fact]
    public void Capabilities_ExposeTheCurrentDeterministicSourceRevision()
    {
        CliResult first = _workspace.Run("capabilities", "--output", "json");
        CliResult compact = _workspace.Run("capabilities", "--output", "compact");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, compact.ExitCode);
        // Compact output is the same document on one line.
        Assert.Single(compact.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.True(JsonNode.DeepEquals(Parse(first.StdOut), Parse(compact.StdOut)));

        using JsonDocument capabilities = JsonDocument.Parse(first.StdOut);
        string sourceRevision = capabilities.RootElement
            .GetProperty("sourceRevision")
            .GetString()!;

        bool dirty = capabilities.RootElement.GetProperty("buildDirty").GetBoolean();
        if (sourceRevision == "unknown")
        {
            Assert.True(dirty);
        }
        else
        {
            Assert.Matches("^[0-9a-f]{40}$", sourceRevision);
        }
        Assert.True(capabilities.RootElement.TryGetProperty("enginePins", out _));
        string declaredVersion = XDocument.Load(Path.Combine(RepositoryPaths.Root, "Directory.Build.props"))
            .Descendants("Version").Single().Value;
        Assert.Equal(declaredVersion, capabilities.RootElement.GetProperty("cliVersion").GetString());
    }

    /// <summary>
    /// Pins the complete capabilities document, including every command's option metadata.
    /// Only the build identity is normalized: the CLI version, source revision, dirty flag and
    /// engine package versions change with releases and SDK updates, not with command
    /// definitions, and <see cref="Capabilities_ExposeTheCurrentDeterministicSourceRevision"/>
    /// checks them.
    /// </summary>
    [Fact]
    public void Capabilities_MatchTheSnapshot()
    {
        CliResult result = _workspace.Run("capabilities", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        string normalized = BuildIdentity.Replace(
            result.StdOut,
            static match => match.Groups["key"].Value + "\"<build>\"");
        AssertSnapshot("capabilities.json", normalized);
    }

    /// <summary>
    /// Pins the help text of a product group and of every command under it, byte for byte.
    /// The command list comes from the capabilities document, whose snapshot pins that list.
    /// </summary>
    [Theory]
    [InlineData("cells")]
    [InlineData("pdf")]
    [InlineData("slides")]
    [InlineData("words")]
    public void ProductHelp_MatchesTheSnapshot(string product)
    {
        CliResult capabilities = _workspace.Run("capabilities", product, "--output", "json");
        Assert.Equal(0, capabilities.ExitCode);
        IEnumerable<string[]> paths = Assert.Single(Parse(capabilities.StdOut)["products"]!.AsArray())!["commands"]!.AsArray()
            .Select(static command => command!["path"]!.GetValue<string>().Split(' '));
        AssertSnapshot($"{product}.help.txt", CollectHelp(paths));
    }

    /// <summary>
    /// Pins the help text of the root command and of every Host command, including hidden ones,
    /// byte for byte. The command list is every capabilities command outside a product group.
    /// </summary>
    [Fact]
    public void HostHelp_MatchesTheSnapshot()
    {
        CliResult capabilities = _workspace.Run("capabilities", "--output", "json");
        Assert.Equal(0, capabilities.ExitCode);
        JsonNode json = Parse(capabilities.StdOut);
        HashSet<string> products = json["products"]!.AsArray()
            .Select(static product => product!["id"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        IEnumerable<string[]> paths = json["commands"]!.AsArray()
            .Select(static command => command!["path"]!.GetValue<string>().Split(' ').Skip(1).ToArray())
            .Where(path => path.Length == 0 || !products.Contains(path[0]));
        AssertSnapshot("host.help.txt", CollectHelp(paths));
    }

    /// <summary>Runs <c>--help</c> for each command path, relative to the root, and joins the outputs.</summary>
    private string CollectHelp(IEnumerable<string[]> paths)
    {
        var help = new StringBuilder();
        foreach (string[] path in paths)
        {
            CliResult result = _workspace.Run([.. path, "--help"]);
            string command = string.Join(' ', ["aspose-cli", .. path]);
            Assert.True(result.ExitCode == 0, $"{command} --help exited {result.ExitCode}: {result.StdErr}");
            help.Append("## ").Append(command).Append(" --help\n")
                .Append(result.StdOut)
                .Append('\n');
        }
        return help.ToString();
    }

    [Theory]
    [InlineData("product")]
    [InlineData("command")]
    [InlineData("unknown-product")]
    [InlineData("unknown-command")]
    public void Capabilities_Selection_IsSmallAndActionable(
        string selection)
    {
        CliResult result = selection switch
        {
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
        JsonNode product = Assert.Single(json["products"]!.AsArray())!;
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
        string operationId = operation["ops"]![0]!.GetValue<string>();

        CliResult selected = _workspace.Run(
            "schema", schemaId, "--operation", operationId);
        Assert.Equal(0, selected.ExitCode);
        Assert.Equal(string.Empty, selected.StdErr);
        JsonNode selectedSchema = Parse(selected.StdOut);
        Assert.Equal(
            $"https://schemas.aspose.com/aspose-cli/{schemaId}.schema.json",
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
            "https://schemas.aspose.com/aspose-cli/v2/common/schema-list.schema.json",
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
    public void UpdateCheck_ReportsTheReleaseAManifestDescribes()
    {
        string manifest = WriteReleaseManifest(_workspace.File("feed"), "release.zip", 1, new string('a', 64));

        CliResult result = _workspace.Run("update", "check", manifest, "--output", "json");

        Assert.Equal(0, result.ExitCode);
        JsonNode update = Parse(result.StdOut);
        Assert.Equal("available", update["status"]!.GetValue<string>());
        Assert.Equal("99.0.0", update["availableVersion"]!.GetValue<string>());
    }

    [Fact]
    public void UpdateInstall_RejectsAnArchiveThatDoesNotMatchItsManifest()
    {
        string root = _workspace.File("feed");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "release.zip"), "tampered", Encoding.UTF8);
        string manifest = WriteReleaseManifest(root, "release.zip", 8, new string('a', 64));

        CliResult result = _workspace.Run("update", "install", manifest, "--output", "json");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal("RELEASE_VERIFICATION_FAILED", Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void UpdateInstall_RejectsUnsafeZipEntriesBeforeInstallerHandoff()
    {
        Requires.Windows();

        string root = _workspace.File("feed");
        Directory.CreateDirectory(root);
        string archive = Path.Combine(root, "release.zip");
        string escapeName = "aspose-update-escape-" + Guid.NewGuid().ToString("N") + ".txt";
        string escaped = Path.Combine(Path.GetTempPath(), escapeName);
        using (FileStream stream = File.Create(archive))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            zip.CreateEntry("../" + escapeName);
        }

        string manifest = WriteReleaseManifest(root, "release.zip", new FileInfo(archive).Length, HashFile(archive));
        CliResult result = _workspace.Run("update", "install", manifest, "--output", "json");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal("RELEASE_VERIFICATION_FAILED", Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(escaped));
    }

    private static string WriteReleaseManifest(string root, string archive, long size, string sha256)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "RELEASE-MANIFEST.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                productId = Aspose.Cli.Sdk.DistributionInfo.Id,
                runtimeIdentifier = "win-x64",
                artifactVersion = "99.0.0",
                sourceRevision = new string('a', 40),
                archive = new { path = archive, size, sha256 },
            }),
            Encoding.UTF8);
        return path;
    }

    /// <summary>Set to 1 to rewrite the snapshots under Integration/Snapshots from the current build.</summary>
    private const string UpdateSnapshotsVariable = Aspose.Cli.Sdk.DistributionInfo.EnvironmentVariablePrefix + "TEST_UPDATE_SNAPSHOTS";

    private static readonly Regex BuildIdentity = new(
        """(?<key>"(?:cliVersion|sourceRevision|buildDirty|version|sdkVersion)": )(?:"[^"]*"|true|false)""",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Compares output with its committed snapshot exactly. Line endings are compared as LF
    /// because Git may check the snapshot out with CRLF.
    /// </summary>
    private static void AssertSnapshot(string name, string actual)
    {
        string path = Path.Combine(
            RepositoryPaths.Root, "tests", "Aspose.Cli.Platform.Tests", "Integration", "Snapshots", name);
        actual = actual.ReplaceLineEndings("\n");
        if (Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path), $"Snapshot {name} is missing; set {UpdateSnapshotsVariable}=1 to create it.");
        string expected = File.ReadAllText(path, Encoding.UTF8).ReplaceLineEndings("\n");
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        string[] expectedLines = expected.Split('\n');
        string[] actualLines = actual.Split('\n');
        int line = 0;
        while (line < expectedLines.Length && line < actualLines.Length &&
            string.Equals(expectedLines[line], actualLines[line], StringComparison.Ordinal))
        {
            line++;
        }
        Assert.Fail(
            $"Output differs from snapshot {name} at line {line + 1}.\n" +
            $"expected: {(line < expectedLines.Length ? expectedLines[line] : "<end of snapshot>")}\n" +
            $"actual:   {(line < actualLines.Length ? actualLines[line] : "<end of output>")}\n" +
            $"If the change is intended, set {UpdateSnapshotsVariable}=1, rerun and review the diff.");
    }

    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json)
        ?? throw new InvalidOperationException("Output was not JSON:\n" + json);

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
