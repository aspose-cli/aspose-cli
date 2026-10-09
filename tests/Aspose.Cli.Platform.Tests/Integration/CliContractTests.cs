using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Json.Schema;
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
        // Plain --version stays the parser's single text line, never a JSON envelope.
        Assert.Matches(@"^[0-9][^\s]*?
$", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Theory]
    [InlineData("--version", "--output", "json")]
    [InlineData("--output", "json", "--version")]
    [InlineData("--version", "--output=json")]
    [InlineData("-f", "json", "--version")]
    public void VersionWithJsonOutput_ReportsTheCapabilitiesBuildIdentity(params string[] args)
    {
        CliResult plain = _workspace.Run("--version");
        CliResult result = _workspace.Run(args);
        CliResult capabilities = _workspace.Run("capabilities", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StdErr);
        AssertConformsTo(ResultEnvelope.SchemaUri("common", "version"), result.StdOut);
        JsonNode version = Parse(result.StdOut);
        JsonNode expected = Parse(capabilities.StdOut);
        foreach (string field in new[] { "cliVersion", "sourceRevision", "buildDirty", "enginePins" })
        {
            Assert.True(JsonNode.DeepEquals(expected[field], version[field]), field);
        }
        // Plain --version prints the artifact version; the result names it only when it differs.
        string artifact = version["artifactVersion"]?.GetValue<string>()
            ?? version["cliVersion"]!.GetValue<string>();
        Assert.Equal(plain.StdOut.Trim(), artifact);
    }

    [Fact]
    public void VersionWithCompactOrTableOutput_UsesTheOrdinaryResultWriter()
    {
        CliResult json = _workspace.Run("--version", "--output", "json");
        CliResult compact = _workspace.Run("--version", "--output", "compact");
        CliResult table = _workspace.Run("--version", "--output", "table");

        Assert.Equal(0, compact.ExitCode);
        Assert.Single(compact.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.True(JsonNode.DeepEquals(Parse(json.StdOut), Parse(compact.StdOut)));
        Assert.Equal(0, table.ExitCode);
        Assert.StartsWith(
            "aspose-cli " + Parse(json.StdOut)["cliVersion"]!.GetValue<string>(),
            table.StdOut,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--version", "--quiet")]
    [InlineData("--version", "--output", "json", "--quiet")]
    [InlineData("--version", "--output", "text")]
    [InlineData("--version", "--output", "json", "--workdir", ".")]
    [InlineData("--version", "--output", "json", "doctor")]
    public void VersionWithAnotherOption_StaysAUsageError(params string[] args)
    {
        CliResult result = _workspace.Run(args);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal("USAGE_ERROR", Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
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
                Assert.True(operation["maximumOperationCount"]!.GetValue<int>() > 0);
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
    /// Pins the complete capabilities of each product, including every command's option metadata
    /// and the product's operation and result schemas, in <c>capabilities/&lt;product&gt;.json</c>
    /// (see <see cref="CapabilitiesSnapshots"/>). Only the build identity is normalized: the CLI
    /// version, source revision, dirty flag and engine package versions change with releases and
    /// SDK updates, not with command definitions, and
    /// <see cref="Capabilities_ExposeTheCurrentDeterministicSourceRevision"/> checks them.
    /// </summary>
    [Theory]
    [InlineData("cells")]
    [InlineData("pdf")]
    [InlineData("slides")]
    [InlineData("words")]
    public void ProductCapabilities_MatchTheSnapshot(string product)
    {
        JsonNode capabilities = NormalizedCapabilities();

        Assert.Contains(product, CapabilitiesSnapshots.Products(capabilities));
        AssertSnapshot(
            Path.Combine("capabilities", product + ".json"),
            CapabilitiesSnapshots.Product(capabilities, product, ProductCapabilities, SchemaDocument));
    }

    /// <summary>
    /// Pins the rest of the capabilities document in <c>capabilities/host.json</c>: the Host
    /// commands, common diagnostics, routing defaults, budgets, engine pins and common schemas.
    /// The build identity is normalized as in <see cref="ProductCapabilities_MatchTheSnapshot"/>.
    /// </summary>
    [Fact]
    public void HostCapabilities_MatchTheSnapshot()
    {
        JsonNode capabilities = NormalizedCapabilities();

        AssertSnapshot(
            Path.Combine("capabilities", "host.json"),
            CapabilitiesSnapshots.Host(capabilities, ProductCapabilities, SchemaDocument));
    }

    private JsonNode NormalizedCapabilities()
    {
        CliResult result = _workspace.Run("capabilities", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        return Parse(BuildIdentity.Replace(
            result.StdOut,
            static match => match.Groups["key"].Value + "\"<build>\""));
    }

    private JsonNode ProductCapabilities(string product)
    {
        CliResult result = _workspace.Run("capabilities", product, "--output", "json");
        Assert.True(result.ExitCode == 0, $"capabilities {product}: {result.StdErr}");
        return Parse(result.StdOut);
    }

    private JsonNode SchemaDocument(string id)
    {
        CliResult result = _workspace.Run("schema", id);
        Assert.True(result.ExitCode == 0, $"schema {id}: {result.StdErr}");
        return Parse(result.StdOut);
    }

    /// <summary>
    /// Pins the capabilities summary; the build identity is normalized as in
    /// <see cref="ProductCapabilities_MatchTheSnapshot"/>.
    /// </summary>
    [Fact]
    public void CapabilitiesSummary_MatchesTheSnapshot()
    {
        CliResult result = _workspace.Run("capabilities", "--summary", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        string normalized = BuildIdentity.Replace(
            result.StdOut,
            static match => match.Groups["key"].Value + "\"<build>\"");
        AssertSnapshot("capabilities-summary.json", normalized);
    }

    [Fact]
    public void CapabilitiesSummary_ProjectsTheCapabilitiesDocument()
    {
        CliResult result = _workspace.Run("capabilities", "--summary", "--output", "json");
        CliResult full = _workspace.Run("capabilities", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        AssertConformsTo(ResultEnvelope.SchemaUri("common", "capabilities-summary"), result.StdOut);
        // The first look stays small next to the full document.
        Assert.True(result.StdOut.Length < 16 * 1024, $"The summary is {result.StdOut.Length} characters.");
        JsonNode summary = Parse(result.StdOut);
        JsonNode capabilities = Parse(full.StdOut);
        Assert.Equal(capabilities["cliVersion"]!.GetValue<string>(), summary["cliVersion"]!.GetValue<string>());
        JsonArray products = capabilities["products"]!.AsArray();
        JsonArray summarized = summary["products"]!.AsArray();
        Assert.Equal(
            products.Select(static product => product!["id"]!.GetValue<string>()),
            summarized.Select(static product => product!["id"]!.GetValue<string>()));
        foreach ((JsonNode? product, JsonNode? entry) in products.Zip(summarized))
        {
            string id = product!["id"]!.GetValue<string>();
            foreach (string formats in new[] { "loadFormats", "convertFormats", "renderFormats" })
            {
                Assert.True(JsonNode.DeepEquals(product[formats], entry![formats]), $"{id} {formats}");
            }
            Assert.Equal(product["engine"]!["sdkVersion"]!.GetValue<string>(), entry!["engineVersion"]!.GetValue<string>());
            Assert.Equal(
                product["commands"]!.AsArray()
                    .Where(static command => !command!["hidden"]!.GetValue<bool>())
                    .Select(static command => command!["path"]!.GetValue<string>())
                    .Where(path => path != id)
                    .Select(path => path[(id.Length + 1)..]),
                entry["commands"]!.AsArray().Select(static command => command!["command"]!.GetValue<string>()));
            Assert.True(
                JsonNode.DeepEquals(
                    new JsonArray(product["operations"]!.AsArray()
                        .Select(static operation => (JsonNode)new JsonObject
                        {
                            ["command"] = operation!["command"]!.DeepClone(),
                            ["ops"] = operation["ops"]!.DeepClone(),
                        })
                        .ToArray()),
                    entry["operations"]),
                $"{id} operations");
        }
    }

    [Fact]
    public void CapabilitiesSummary_SelectsOneProductButNoCommand()
    {
        CliResult cells = _workspace.Run("capabilities", "cells", "--summary", "--output", "json");
        CliResult command = _workspace.Run("capabilities", "cells", "edit", "--summary", "--output", "json");
        CliResult unknown = _workspace.Run("capabilities", "missing", "--summary", "--output", "json");

        Assert.Equal(0, cells.ExitCode);
        Assert.Equal("cells", Assert.Single(Parse(cells.StdOut)["products"]!.AsArray())!["id"]!.GetValue<string>());
        Assert.Equal(2, command.ExitCode);
        Assert.Equal("USAGE_ERROR", Parse(command.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.Equal(2, unknown.ExitCode);
        Assert.Equal("OPTION_INVALID", Parse(unknown.StdErr)["error"]!["code"]!.GetValue<string>());
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

    /// <summary>
    /// A long value list moves from the option label into the description, so it cannot widen the
    /// option column of every row; capabilities still list every value.
    /// </summary>
    [Theory]
    [InlineData("--code <code>", "Accepts ", "review")]
    [InlineData("--to <to>", "Values: azw3, doc, ", "words", "convert")]
    public void Help_KeepsLongValueListsOutOfTheOptionColumn(string label, string description, params string[] command)
    {
        CliResult result = _workspace.Run([.. command, "--help"]);

        Assert.Equal(0, result.ExitCode);
        string[] options = [.. result.StdOut.Split('\n')
            .SkipWhile(static line => !line.StartsWith("Options:", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(static line => line.Trim().Length > 0)];
        Assert.NotEmpty(options);
        Assert.All(options, line => Assert.True(
            line.TrimStart().IndexOf("  ", StringComparison.Ordinal) < 48,
            $"The option column is too wide: {line[..Math.Min(line.Length, 120)]}"));
        string row = Assert.Single(options, line => line.TrimStart().StartsWith(label, StringComparison.Ordinal));
        Assert.Contains(description, row, StringComparison.Ordinal);
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
                details["option"]!.GetValue<string>());
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

    [Theory]
    [InlineData("pdf inspcet a.pdf", "pdf inspect", "'inspcet' is not a command of 'aspose-cli pdf'")]
    [InlineData("pdf extact a.pdf --pages 1", "pdf extract", "'extact' is not a command of 'aspose-cli pdf'")]
    [InlineData("wrods convert a.docx", "words", "'wrods' is not a command of 'aspose-cli'")]
    [InlineData("app sattus", "app status", "'sattus' is not a command of 'aspose-cli app'")]
    [InlineData("preview sattus", "preview status", "'sattus' is not a command of 'aspose-cli preview'")]
    public void UnknownCommand_SuggestsTheClosestCommands(string commandLine, string suggestion, string message)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.StartsWith(message, error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // The tokens after an unknown command are not reported as unknown on their own.
        Assert.DoesNotContain(".docx", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain(".pdf", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownOption_SuggestsTheClosestOptionsOfTheCommand()
    {
        CliResult result = _workspace.Run("cells", "compare", "a.xlsx", "b.xlsx", "--password-env", "PASSWORD");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Equal(
            ["--left-password-env", "--right-password-env"],
            error["details"]!["suggestions"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.StartsWith("Did you mean '--left-password-env' or '--right-password-env'?", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cells inspect a.xlsx --detail cahrts", "charts")]
    [InlineData("--license-mode autoo cells inspect a.xlsx", "auto")]
    [InlineData("skill install --host calude-code", "claude-code")]
    public void UnknownValue_ListsTheAllowedValuesAndSuggestsTheClosest(string commandLine, string suggestion)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Contains(suggestion, error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'?", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("app", "--route", true)]
    [InlineData("cells query range", "--scan-range", true)]
    [InlineData("cells convert", "--out", false)]
    public void Capabilities_PublishWhetherAnOptionIsHidden(string command, string option, bool hidden)
    {
        CliResult result = _workspace.Run("capabilities", "--output", "json");

        JsonNode described = Parse(result.StdOut)["commands"]!.AsArray()
            .Single(item => item!["path"]!.GetValue<string>() == $"aspose-cli {command}")!["options"]!.AsArray()
            .Single(item => item!["name"]!.GetValue<string>() == option)!;
        Assert.Equal(hidden, described["hidden"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("app --routee", "--route")]
    [InlineData("cells query range a.xlsx --sacn-range", "--scan-range")]
    public void UnknownOption_NeverSuggestsAHiddenOption(string commandLine, string hidden)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.DoesNotContain(hidden, (error["details"]!["suggestions"]?.AsArray() ?? []).Select(static item => item!.GetValue<string>()));
        Assert.DoesNotContain(hidden, error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
    }

    [Fact]
    public void UnknownOption_OfTheRootCommandSuggestsItsOwnOptions()
    {
        CliResult result = _workspace.Run("--vresion", "--output", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal(["--version"], error["details"]!["suggestions"]!.AsArray().Select(static item => item!.GetValue<string>()));
    }

    [Theory]
    [InlineData("docs edting", "editing")]
    [InlineData("schema v2/common/not-found-detail", "v2/common/not-found-details")]
    [InlineData("schema v2/pdf/ops --operation rotate_page", "rotate_pages")]
    public void UnknownName_ListsWhatExistsAndSuggestsTheClosest(string commandLine, string suggestion)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(suggestion, error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal(suggestion, error["details"]!["suggestions"]![0]!.GetValue<string>());
        Assert.StartsWith($"Did you mean '{suggestion}'?", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownOption_IsNeverTakenAsAFileArgument()
    {
        CliResult result = _workspace.Run("pdf", "merge", "a.pdf", "b.pdf", "--out", "c.pdf", "--bookmark", "drop");
        CliResult escaped = _workspace.Run("pdf", "merge", "a.pdf", "--out", "c.pdf", "--", "--bookmark");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Contains("--bookmark", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(["--bookmarks"], error["details"]!["suggestions"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal("FILE_NOT_FOUND", Parse(escaped.StdErr)["error"]!["code"]!.GetValue<string>());
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
        """(?<key>"(?:cliVersion|sourceRevision|buildDirty|version|sdkVersion|engineVersion)": )(?:"[^"]*"|true|false)""",
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

    private static void AssertConformsTo(string schemaId, string json)
    {
        JsonSchema schema = SchemaTestRegistry.CreateOptions().SchemaRegistry
            .Get(new Uri(schemaId)) as JsonSchema
            ?? throw new InvalidOperationException($"Schema {schemaId} is not registered.");
        using JsonDocument document = JsonDocument.Parse(json);
        EvaluationResults evaluation = schema.Evaluate(document.RootElement);
        Assert.True(evaluation.IsValid, JsonSerializer.Serialize(evaluation));
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
