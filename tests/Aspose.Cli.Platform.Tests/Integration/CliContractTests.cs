using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aspose.Cli.Architecture.Tests.TestSupport;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Black-box coverage for the commercial executable contract.</summary>
public sealed class CliContractTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>Every product of this build, so a new product is pinned without changing a test.</summary>
    public static TheoryData<string> ProductIds() => [.. CliCatalog.Current.Products.Select(static product => product.Id)];

    [Fact]
    public void Version_PrintsVersionAndExitsZero()
    {
        CliResult result = _workspace.Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StdOut));
        Assert.True(char.IsAsciiDigit(result.StdOut.TrimStart()[0]));
        // Plain --version stays the parser's single text line, never a JSON envelope.
        Assert.Matches("^[0-9][^\\s]*\r?\n$", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Theory]
    [InlineData("--version", "--output-format", "json")]
    [InlineData("--output-format", "json", "--version")]
    [InlineData("--version", "--output-format=json")]
    public void VersionWithJsonOutput_ReportsTheCatalogsBuildIdentity(params string[] args)
    {
        CliResult plain = _workspace.Run("--version");
        CliResult result = _workspace.Run(args);
        JsonNode catalog = Parse(_workspace.Run("schema").StdOut);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StdErr);
        AssertConformsTo(ResultEnvelope.SchemaUri("common", "version"), result.StdOut);
        JsonNode version = Parse(result.StdOut);
        Assert.Equal(catalog["cliVersion"]!.GetValue<string>(), version["cliVersion"]!.GetValue<string>());
        Assert.Equal(
            catalog["products"]!.AsArray().Select(static product => $"{product!["id"]} {product["engine"]} {product["engineVersion"]}").Order(StringComparer.Ordinal),
            version["enginePins"]!.AsArray().Select(static pin => $"{pin!["product"]} {pin["engine"]} {pin["version"]}"));
        // Plain --version prints the artifact version; the result names it only when it differs.
        string artifact = version["artifactVersion"]?.GetValue<string>()
            ?? version["cliVersion"]!.GetValue<string>();
        Assert.Equal(plain.StdOut.Trim(), artifact);
    }

    [Fact]
    public void VersionWithCompactOutput_UsesTheOrdinaryResultWriter()
    {
        CliResult json = _workspace.Run("--version", "--output-format", "json");
        CliResult compact = _workspace.Run("--version", "--output-format", "compact");

        Assert.Equal(0, compact.ExitCode);
        Assert.Single(compact.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.True(JsonNode.DeepEquals(Parse(json.StdOut), Parse(compact.StdOut)));
    }

    [Theory]
    [InlineData("--version", "--verbose")]
    [InlineData("--version", "--output-format", "json", "--verbose")]
    [InlineData("--version", "--output-format", "text")]
    [InlineData("--version", "--output-format", "json", "--workdir", ".")]
    [InlineData("--version", "--output-format", "json", "schema")]
    [InlineData("--version", "--bogus")]
    [InlineData("-f", "json", "--version")]
    public void VersionWithAnotherOption_StaysAUsageError(params string[] args)
    {
        CliResult result = _workspace.Run(args);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal("USAGE_ERROR", Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Version_ExposesTheCurrentDeterministicSourceRevision()
    {
        CliResult result = _workspace.Run("--version", "--output-format", "json");

        Assert.Equal(0, result.ExitCode);
        using JsonDocument version = JsonDocument.Parse(result.StdOut);
        string sourceRevision = version.RootElement
            .GetProperty("sourceRevision")
            .GetString()!;

        bool dirty = version.RootElement.GetProperty("buildDirty").GetBoolean();
        if (sourceRevision == "unknown")
        {
            Assert.True(dirty);
        }
        else
        {
            Assert.Matches("^[0-9a-f]{40}$", sourceRevision);
        }
        Assert.True(version.RootElement.TryGetProperty("enginePins", out _));
        string declaredVersion = XDocument.Load(Path.Combine(RepositoryPaths.Root, "Directory.Build.props"))
            .Descendants("Version").Single().Value;
        Assert.Equal(declaredVersion, version.RootElement.GetProperty("cliVersion").GetString());
    }

    /// <summary>
    /// Pins what <c>schema</c> publishes for each product, its catalog entry and every document
    /// its ids name, in <c>schema/&lt;product&gt;.json</c> (see <see cref="SchemaSnapshots"/>). Only the
    /// build identity is normalized: the CLI version and the engine package versions change with
    /// releases and SDK updates, not with command definitions, and
    /// <see cref="Version_ExposesTheCurrentDeterministicSourceRevision"/> checks them.
    /// </summary>
    [Theory]
    [MemberData(nameof(ProductIds))]
    public void ProductSchema_MatchesTheSnapshot(string product)
    {
        JsonNode catalog = NormalizedCatalog();

        Assert.Contains(product, SchemaSnapshots.Products(catalog));
        AssertSnapshot(
            Path.Combine("schema", product + ".json"),
            SchemaSnapshots.Product(catalog, product, Document));
    }

    /// <summary>
    /// Pins the rest of what <c>schema</c> publishes in <c>schema/common.json</c>: the catalog
    /// without the products' entries, and every common document. The build identity is normalized
    /// as in <see cref="ProductSchema_MatchesTheSnapshot"/>.
    /// </summary>
    [Fact]
    public void CommonSchema_MatchesTheSnapshot()
    {
        JsonNode catalog = NormalizedCatalog();

        AssertSnapshot(
            Path.Combine("schema", "common.json"),
            SchemaSnapshots.Common(catalog, Document));
    }

    private JsonNode NormalizedCatalog()
    {
        CliResult result = _workspace.Run("schema");

        Assert.Equal(0, result.ExitCode);
        return Parse(BuildIdentity.Replace(
            result.StdOut,
            static match => match.Groups["key"].Value + "\"<build>\""));
    }

    private JsonNode Document(string id)
    {
        CliResult result = _workspace.Run("schema", id);
        Assert.True(result.ExitCode == 0, $"schema {id}: {result.StdErr}");
        return Parse(result.StdOut);
    }

    /// <summary>
    /// Pins the help text of a product group and of every command under it, byte for byte.
    /// The command list comes from the schema catalog, whose snapshot pins that list.
    /// </summary>
    [Theory]
    [MemberData(nameof(ProductIds))]
    public void ProductHelp_MatchesTheSnapshot(string product)
    {
        IEnumerable<string[]> paths = CliCatalog.Current.Commands
            .Where(command => command.Product == product)
            .Select(static command => command.Tokens);
        AssertSnapshot($"{product}.help.txt", CollectHelp(paths));
    }

    /// <summary>
    /// Pins the help text of the root command and of every Host command, byte for byte. The
    /// command list is every command of the schema catalog outside a product group.
    /// </summary>
    [Fact]
    public void HostHelp_MatchesTheSnapshot()
    {
        IEnumerable<string[]> paths = CliCatalog.Current.Commands
            .Where(static command => command.Product is null)
            .Select(static command => command.Tokens);
        AssertSnapshot("host.help.txt", CollectHelp(paths));
    }

    /// <summary>
    /// A long value list moves from the option label into the description, so it cannot widen the
    /// option column of every row; the command's schema document still lists every value.
    /// </summary>
    [Theory]
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

    /// <summary>Joins the <c>--help</c> output of each command path, relative to the root, in order.</summary>
    private static string CollectHelp(IEnumerable<string[]> paths)
    {
        string[][] ordered = [.. paths];
        HelpOutputs.Prefetch(ordered);
        var help = new StringBuilder();
        foreach (string[] path in ordered)
        {
            CliResult result = HelpOutputs.Of(path);
            string command = string.Join(' ', ["aspose-cli", .. path]);
            Assert.True(result.ExitCode == 0, $"{command} --help exited {result.ExitCode}: {result.StdErr}");
            help.Append("## ").Append(command).Append(" --help\n")
                .Append(result.StdOut)
                .Append('\n');
        }
        return help.ToString();
    }

    [Fact]
    public void SchemaPart_ReturnsExactViewAndUnknownIdsAreActionable()
    {
        string schemaId = ResultEnvelope.CatalogId("pdf", "ops");
        const string part = "field";

        CliResult selected = _workspace.Run("schema", $"{schemaId}/{part}");
        Assert.Equal(0, selected.ExitCode);
        Assert.Equal(string.Empty, selected.StdErr);
        JsonNode selectedSchema = Parse(selected.StdOut);
        Assert.Equal(
            $"{ResultEnvelope.SchemaUri("pdf", "ops")}#/$defs/{part}",
            selectedSchema["$id"]!.GetValue<string>());

        CliResult unknown = _workspace.Run("schema", $"{schemaId}/__unknown_part__");
        Assert.Equal(2, unknown.ExitCode);
        Assert.Equal(string.Empty, unknown.StdOut);
        JsonNode error = Parse(unknown.StdErr)["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(
            $"{schemaId}/{part}",
            error["details"]!["available"]!.AsArray()
                .Select(static item => item!.GetValue<string>()));
    }

    [Fact]
    public void SchemaCatalog_UsesTheEnvelopeAndStaysSmall()
    {
        CliResult json = _workspace.Run("schema", "--output-format", "json");
        CliResult compact = _workspace.Run("schema", "--output-format", "compact");
        Assert.Equal(0, json.ExitCode);
        AssertConformsTo(ResultEnvelope.SchemaUri("common", "schema-catalog"), json.StdOut);
        // The first look stays small next to the documents it lists.
        Assert.True(json.StdOut.Length < 30_000, $"The catalog is {json.StdOut.Length} characters.");
        Assert.Single(compact.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.True(JsonNode.DeepEquals(Parse(json.StdOut), Parse(compact.StdOut)));
        string[] ids = [.. Parse(json.StdOut)["ids"]!.AsArray().Select(static item => item!.GetValue<string>())];
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
    }

    [Fact]
    public void LicenseStatus_ReflectsTheCompiledEngineContract()
    {
        CliResult result = _workspace.Run("license", "status", "--output-format", "json");

        Assert.Equal(0, result.ExitCode);
        JsonNode json = Parse(result.StdOut);
        Assert.Null(json["applicable"]);
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
        // One problem is the message; details.errors lists problems only when there are several.
        Assert.Null(error["details"]!["errors"]);
    }

    [Fact]
    public void FontsList_WithoutProduct_ListsEveryProductEngine()
    {
        string[] products = [.. CliCatalog.Current.Command("fonts list").Option("--product")!.AllowedValues];

        CliResult all = _workspace.Run("fonts", "list", "--output-format", "json");
        CliResult one = _workspace.Run("fonts", "list", "--product", products[^1], "--output-format", "json");

        Assert.True(all.ExitCode == 0, all.StdErr);
        JsonArray listed = Parse(all.StdOut)["products"]!.AsArray();
        Assert.Equal(products, listed.Select(static item => item!["product"]!.GetValue<string>()));
        Assert.All(listed.SelectMany(static item => item!["sources"]!.AsArray()), source =>
        {
            Assert.NotNull(source!["kind"]);
            Assert.Null(source["type"]);
        });
        Assert.True(one.ExitCode == 0, one.StdErr);
        Assert.Equal([products[^1]], Parse(one.StdOut)["products"]!.AsArray().Select(static item => item!["product"]!.GetValue<string>()));
    }

    [Fact]
    public void UnknownValue_IsOneLineThatListsTheValuesAvailable()
    {
        CliResult result = _workspace.Run("cells", "convert", "a.xlsx", "--to", "xlss", "--output-format", "json");

        JsonNode error = Parse(result.StdErr)["error"]!;
        string message = error["message"]!.GetValue<string>();
        string[] available = [.. error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>())];
        Assert.DoesNotContain('\n', message);
        Assert.EndsWith($"Must be one of: {string.Join(", ", available)}", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cells convert a.xlsx --to xlss", "xls")]
    [InlineData("--license-mode autoo cells read a.xlsx", "auto")]
    [InlineData("skill install aspose-cli-platform --host calude-code", "claude-code")]
    public void UnknownValue_ListsTheAllowedValues(string commandLine, string value)
    {
        CliResult result = _workspace.Run([.. commandLine.Split(' '), "--output-format", "json"]);

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Contains(value, error["details"]!["available"]!.AsArray().Select(static item => item!.GetValue<string>()));
    }

    [Theory]
    [InlineData("-f json cells read a.xlsx", "-f")]
    [InlineData("cells read a.xlsx --output json", "--output")]
    [InlineData("cells read a.xlsx --output=json", "--output")]
    [InlineData("-? cells", "-?")]
    [InlineData("cells read a.xlsx /h", "/h")]
    [InlineData("cells read a.xlsx --preview", "--preview")]
    [InlineData("skill install aspose-cli-platform --host codex --scope user", "--scope")]
    public void RemovedNames_AreUnknownOptions(string commandLine, string removed)
    {
        CliResult result = _workspace.Run(commandLine.Split(' '));

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        Assert.Contains($"'{removed}'", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        // A value joined to an unknown option may be a secret, so the message names the option alone.
        Assert.DoesNotContain("=json", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The schema catalog describes the public grammar: the hidden internal commands that the CLI
    /// starts for itself stay out.
    /// </summary>
    [Fact]
    public void SchemaCatalog_ListsNoInternalCommand()
    {
        Assert.DoesNotContain(CliCatalog.Current.Commands, static command => command.Words.Contains("__", StringComparison.Ordinal));
        Assert.DoesNotContain(CliCatalog.Current.Ids, static id => id.Contains("__", StringComparison.Ordinal));
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
        Assert.Equal("FILE_NOT_FOUND", Parse(escaped.StdErr)["error"]!["code"]!.GetValue<string>());
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
            "--output-format",
            "json");
        Assert.Equal(7, install.ExitCode);
        Assert.Equal(
            "LICENSE_INVALID",
            Parse(install.StdErr)["error"]!["code"]!.GetValue<string>());

        CliResult remove = _workspace.Run(
            "license",
            "remove",
            "--output-format",
            "json");
        Assert.Equal(0, remove.ExitCode);
        Assert.All(Parse(remove.StdOut)["products"]!.AsArray(), product =>
            Assert.Equal("evaluation", product!["mode"]!.GetValue<string>()));
    }

    /// <summary>Set to 1 to rewrite the snapshots under Integration/Snapshots from the current build.</summary>
    private const string UpdateSnapshotsVariable = Aspose.Cli.Sdk.DistributionInfo.EnvironmentVariablePrefix + "TEST_UPDATE_SNAPSHOTS";

    private static readonly Regex BuildIdentity = new(
        """(?<key>"(?:cliVersion|sourceRevision|buildDirty|version|sdkVersion|engineVersion)": )(?:"[^"]*"|true|false)""",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The numbers this repository gives its own requirements and rules, such as P-5, C-R26,
    /// C-N115, C-B01 or (D7), which mean nothing to a reader of the help or of a schema document.
    /// </summary>
    private static readonly Regex InternalNumber = new(
        @"\bP-[0-9]+\b|\bC-[RNB][0-9]+\b|\(D[0-9]+\)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Compares output with its committed snapshot exactly. Line endings are compared as LF
    /// because Git may check the snapshot out with CRLF. What a snapshot holds is published, so
    /// it never names an internal requirement or rule number.
    /// </summary>
    private static void AssertSnapshot(string name, string actual)
    {
        Assert.True(
            !InternalNumber.IsMatch(actual),
            $"Snapshot {name} publishes the internal number '{InternalNumber.Match(actual).Value}'; describe the behavior instead.");
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
        JsonSchema schema = HostCommonSchemas.CreateOptions().SchemaRegistry
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
