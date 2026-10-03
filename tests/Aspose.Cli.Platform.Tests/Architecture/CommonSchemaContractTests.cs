using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Architecture.Tests.TestSupport;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Sdk.Resources;
using Aspose.Cli.Host.Serialization;
using Aspose.Cli.Platform.Tests.Sdk;
using Json.Schema;
using Aspose.Cli.TestKit;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>Real JSON Schema validation for Host/SDK result envelopes.</summary>
public sealed class CommonSchemaContractTests
{
    private static readonly string RepositoryRoot = RepositoryPaths.Root;
    private static readonly ProductCatalog CommonCatalog = CreateCatalog();

    [Fact]
    public void EveryCommonCanonicalResult_ConformsToItsDeclaredSchema()
    {
        IReadOnlyList<CommonSchemaSample> samples = CommonSchemaSamples.All;
        Assert.NotEmpty(samples);

        foreach (CommonSchemaSample sample in samples)
        {
            string schemaPath = SchemaPath(sample.Schema);
            JsonSchema schema = ParseSchema(File.ReadAllText(schemaPath));
            string json = new HostContractJson(CommonCatalog)
                .Serializer.Serialize(sample.Value);
            using JsonDocument instance = JsonDocument.Parse(json);
            EvaluationResults evaluation = schema.Evaluate(instance.RootElement);
            Assert.True(
                evaluation.IsValid,
                $"{sample.Value.GetType().FullName} does not conform to "
                    + $"{sample.Schema}: {JsonSerializer.Serialize(evaluation)}");

            JsonObject missing = JsonNode.Parse(json)!.AsObject();
            Assert.True(missing.Remove("schema"));
            Assert.False(
                schema.Evaluate(
                    JsonDocument.Parse(missing.ToJsonString()).RootElement)
                    .IsValid);
            JsonObject wrongType = JsonNode.Parse(json)!.AsObject();
            wrongType["schemaVersion"] = "invalid";
            Assert.False(
                schema.Evaluate(
                    JsonDocument.Parse(wrongType.ToJsonString()).RootElement)
                    .IsValid);
        }
    }

    [Fact]
    public void EveryCommonSchema_ParsesAndHasCanonicalIdentity()
    {
        string directory = Path.Combine(
            RepositoryRoot,
            "src",
            "Aspose.Cli.Sdk",
            "Schemas");
        string[] schemas = Directory.GetFiles(
            directory,
            "*.schema.json",
            SearchOption.AllDirectories)
            .Where(path => string.Equals(
                new FileInfo(path).Directory?.Name,
                "common",
                StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(schemas);
        foreach (string path in schemas)
        {
            string text = File.ReadAllText(path);
            _ = ParseSchema(text);
            JsonObject document = JsonNode.Parse(text)!.AsObject();
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                document["$schema"]?.GetValue<string>());
            string relative = Path.GetRelativePath(
                    Path.Combine(
                        RepositoryRoot,
                        "src",
                        "Aspose.Cli.Sdk",
                        "Schemas"),
                    path)
                .Replace(Path.DirectorySeparatorChar, '/');
            Assert.Equal(
                "https://schemas.aspose.com/aspose-cli/" + relative,
                document["$id"]?.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("http://127.0.0.1:54322/d/0123456789abcdef0123456789abcdef/", true)]
    [InlineData("http://127.0.0.1:54322/", false)]
    [InlineData("http://127.0.0.1:54322/d/0123456789abcdef0123456789abcdef/extra", false)]
    [InlineData("http://127.0.0.1:54322/d/0123456789abcdef0123456789abcdef/?token=value", false)]
    [InlineData("http://example.com:54322/d/0123456789abcdef0123456789abcdef/", false)]
    [InlineData("https://127.0.0.1:54322/d/0123456789abcdef0123456789abcdef/", false)]
    public void PreviewSessionUrl_RequiresOneLoopbackDocument(string url, bool valid)
    {
        ProductPreviewStartResult sample = CommonSchemaSamples.ProductPreviewStart with { Url = url };
        string json = new HostContractJson(CommonCatalog).Serializer.Serialize(sample);
        using JsonDocument instance = JsonDocument.Parse(json);
        JsonSchema schema = ParseSchema(SdkSchemaCatalog.Read("v2/common/preview-session"));
        Assert.Equal(valid, schema.Evaluate(instance.RootElement).IsValid);
    }

    [Theory]
    [InlineData("licensed", true)]
    [InlineData("evaluation", true)]
    [InlineData("invalid", true)]
    [InlineData("not-applicable", true)]
    [InlineData("unknown", false)]
    public void DoctorProducts_ValidateTheReportedLicenseMode(string mode, bool valid)
    {
        DoctorResult sample = CommonSchemaSamples.Doctor with
        {
            Products = [new DoctorProductStatus { Product = "cells", Engine = "aspose", LicenseMode = mode }],
        };
        string json = new HostContractJson(CommonCatalog).Serializer.Serialize(sample);
        JsonObject instance = JsonNode.Parse(json)!.AsObject();
        JsonSchema schema = ParseSchema(SdkSchemaCatalog.Read("v2/common/doctor"));
        using JsonDocument complete = JsonDocument.Parse(json);
        Assert.Equal(valid, schema.Evaluate(complete.RootElement).IsValid);
        instance["products"]![0]!.AsObject().Remove("engine");
        using JsonDocument missingEngine = JsonDocument.Parse(instance.ToJsonString());
        Assert.False(schema.Evaluate(missingEngine.RootElement).IsValid);
    }

    [Fact]
    public void OperationDescriptor_SerializesInDeterministicContractOrder()
    {
        string json = new HostContractJson(CommonCatalog)
            .Serializer.Serialize(CommonSchemaSamples.Capabilities);
        JsonObject operation = JsonNode.Parse(json)!["products"]![0]![
            "operations"]![0]!.AsObject();

        Assert.Equal(
            [
                "command",
                "inputSchema",
                "operationSchema",
                "contractFingerprint",
                "maximumOperationCount",
                "ops",
            ],
            operation.Select(static property => property.Key));
        Assert.Equal(
            """{"command":"edit","inputSchema":"v2/test/ops","operationSchema":"aspose-cli schema v2/test/ops --operation <op>","contractFingerprint":"sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a","maximumOperationCount":16,"ops":["replace_text"]}""",
            operation.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public void BoundedEditFoundation_IsCataloguedAndValidatesCanonicalValues()
    {
        (string Id, string Json)[] contracts =
        [
            (
                "v2/common/file-fingerprint",
                """{"sha256":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}"""),
            (
                "v2/common/view",
                """{"schema":"https://schemas.aspose.com/aspose-cli/v2/common/view.schema.json","schemaVersion":2,"view":"pages","sourceFormat":"docx","sourceSizeBytes":10,"totalPartCount":2,"parts":[{"id":"page-1","label":"Page 1","file":"page-0001.png","kind":"image","width":816,"height":1056,"hidden":false,"digest":"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","properties":{"notes":"Speaker notes"},"elements":[{"id":"shape-3","kind":"paragraph","box":{"x":96,"y":96.5,"width":624,"height":20},"digest":"f00d","label":"Hello","level":1}]}]}"""),
        ];

        foreach ((string id, string json) in contracts)
        {
            Assert.Contains(id, SdkSchemaCatalog.Ids);
            JsonSchema schema = JsonSchema.FromText(SdkSchemaCatalog.Read(id));
            using JsonDocument instance = JsonDocument.Parse(json);
            Assert.True(schema.Evaluate(instance.RootElement).IsValid, id);
        }
    }

    [Theory]
    [InlineData("v2/common/backup", typeof(BackupInfo))]
    [InlineData("v2/common/file-fingerprint", typeof(FileFingerprint))]
    [InlineData("v2/common/operation-outcome", typeof(BoundedOperationOutcome))]
    [InlineData("v2/common/verification-issue", typeof(VerificationIssue))]
    public void CommonResultSchema_RequiresExactlyTheRecordsNonNullableMembers(
        string id,
        Type record)
    {
        JsonObject schema = JsonNode.Parse(SdkSchemaCatalog.Read(id))!.AsObject();
        NullabilityInfoContext nullability = new();
        PropertyInfo[] members = record.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(
            members.Select(static member => JsonNamingPolicy.CamelCase.ConvertName(member.Name)).Order(),
            schema["properties"]!.AsObject().Select(static pair => pair.Key).Order());
        Assert.Equal(
            members
                .Where(member => nullability.Create(member).ReadState != NullabilityState.Nullable)
                .Select(static member => JsonNamingPolicy.CamelCase.ConvertName(member.Name))
                .Order(),
            schema["required"]!.AsArray().Select(static value => value!.GetValue<string>()).Order());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
    }

    [Fact]
    public void CommonWarningShapes_AllowStructuredVisualEvidence()
    {
        string directory = Path.Combine(
            RepositoryRoot,
            "src",
            "Aspose.Cli.Sdk",
            "Schemas",
            "v2",
            "common");
        string[] files =
        [
            "app-result.schema.json",
            "capabilities-summary.schema.json",
            "doctor.schema.json",
            "font-check.schema.json",
            "font-list.schema.json",
            "license-status.schema.json",
            "preview-session.schema.json",
            "preview-status.schema.json",
            "review.schema.json",
            "schema-list.schema.json",
            "skill-install.schema.json",
            "skill-list.schema.json",
            "version.schema.json",
        ];

        foreach (string file in files)
        {
            JsonObject document = JsonNode.Parse(
                File.ReadAllText(Path.Combine(directory, file)))!.AsObject();
            JsonObject warning = Assert.Single(
                DescendantObjects(document),
                static item => item["required"] is JsonArray required
                    && required.Any(static value => value?.GetValue<string>() == "code")
                    && required.Any(static value => value?.GetValue<string>() == "message")
                    && item["properties"]?["docs"] is not null);
            JsonObject properties = warning["properties"]!.AsObject();
            Assert.False(warning["additionalProperties"]!.GetValue<bool>());
            Assert.Equal(1, properties["location"]!["minLength"]!.GetValue<int>());
            Assert.Equal(
                "boolean",
                properties["affectsCompleteness"]!["type"]!.GetValue<string>());

            JsonSchema schema = JsonSchema.FromText(warning.ToJsonString());
            using JsonDocument valid = JsonDocument.Parse(
                """{"code":"VISUAL_LIMITED","message":"Evidence is incomplete.","location":"page:2","affectsCompleteness":true}""");
            using JsonDocument invalid = JsonDocument.Parse(
                """{"code":"VISUAL_LIMITED","message":"Evidence is incomplete.","location":""}""");
            using JsonDocument unknown = JsonDocument.Parse(
                """{"code":"VISUAL_LIMITED","message":"Evidence is incomplete.","unexpected":true}""");
            Assert.True(schema.Evaluate(valid.RootElement).IsValid, file);
            Assert.False(schema.Evaluate(invalid.RootElement).IsValid, file);
            Assert.False(schema.Evaluate(unknown.RootElement).IsValid, file);
        }
    }

    /// <summary>Names every integer count of the SDK and Host contracts <c>&lt;noun&gt;Count</c>.</summary>
    [Fact]
    public void IntegerCounts_AreNamedNounCount()
    {
        IReadOnlyList<string> violations = JsonCountNames.Violations(
            [typeof(SdkJsonContext).Assembly, typeof(HostContractJson).Assembly]);
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ProductCatalog_RejectsAnOperationVocabularyOutsideTheProductNamespace()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => CreateCatalog("other", [TestOp.Catalog.Describe("edit")]));

        Assert.Contains("references unowned schema 'v2/test/ops'", error.Message, StringComparison.Ordinal);
    }

    private static string SchemaPath(string schema)
    {
        const string prefix = "https://schemas.aspose.com/aspose-cli/";
        Assert.StartsWith(prefix, schema, StringComparison.Ordinal);
        string relative = schema[prefix.Length..]
            .Replace('/', Path.DirectorySeparatorChar);
        string path = Path.Combine(
            RepositoryRoot,
            "src",
            "Aspose.Cli.Sdk",
            "Schemas",
            relative);
        Assert.True(File.Exists(path), $"Schema does not exist: {path}");
        return path;
    }

    private static JsonSchema ParseSchema(string document) =>
        JsonSchema.FromText(
            document,
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry(),
            });

    private static IEnumerable<JsonObject> DescendantObjects(JsonNode node)
    {
        if (node is JsonObject item)
        {
            yield return item;
            foreach (JsonNode child in item.Select(static pair => pair.Value)
                         .Where(static child => child is not null).Cast<JsonNode>())
            {
                foreach (JsonObject descendant in DescendantObjects(child))
                {
                    yield return descendant;
                }
            }
        }
        else if (node is JsonArray items)
        {
            foreach (JsonNode child in items.Where(static child => child is not null)
                         .Cast<JsonNode>())
            {
                foreach (JsonObject descendant in DescendantObjects(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static ProductCatalog CreateCatalog(
        string productId = "test",
        IReadOnlyList<ProductOperationCommand>? operations = null)
    {
        ProductDefinition definition = ExtProduct.Define<ITestPort>(
                new ProductManifest
                {
                    Id = productId,
                    DisplayName = "Test",
                    Operations = operations ?? [],
                    Engine = new ProductEngineCapabilities
                    {
                        Id = "test",
                        Sdk = "Aspose.Test",
                        SdkVersion = "1.0.0",
                        LicenseApplicable = true,
                        LicenseRequired = false,
                        SupportsFontDiagnostics = true,
                    },
                    AvailableEngines = ["test"],
                })
            .Formats(
            [
                FormatDescriptor.Input(
                    "test",
                    0,
                    RouteOwnership.Explicit,
                    ".test"),
            ])
            .Diagnostics([])
            .Json(new ProductJsonDefinition(productId, SdkJsonContext.Default))
            .View(new TestProductViewAdapter<ITestPort>())
            .Output<TestResult>(static (_, _) => { })
            .Commands(_ => new Command("test"))
            .Activator(static _ =>
                throw new InvalidOperationException(
                    "Common schema tests must not activate product ports."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface ITestPort;

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : ResultEnvelope("test/result", 1);
#pragma warning restore APCLI003

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }

}
