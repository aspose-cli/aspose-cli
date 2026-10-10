using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Architecture.Tests.TestSupport;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Serialization;
using Aspose.Cli.Platform.Tests.Sdk;
using Json.Schema;
using Aspose.Cli.TestKit;
using Xunit;
using ExtProduct = Aspose.Cli.Sdk.Extensibility.Product;
using Aspose.Cli.Platform.Tests;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>Real JSON Schema validation for Host/SDK result envelopes.</summary>
public sealed class CommonSchemaContractTests
{
    private const string UriPrefix = "https://schemas.aspose.com/aspose-cli/";
    private static readonly ProductCatalog CommonCatalog = CreateCatalog();

    private static readonly ContractJsonSerializer CommonSerializer = new(CommonCatalog.JsonDefinitions);
    private static readonly BuildOptions CommonSchemas = SchemaTestRegistry.CreateOptions();

    [Fact]
    public void EveryCommonCanonicalResult_ConformsToItsDeclaredSchema()
    {
        IReadOnlyList<CommonSchemaSample> samples = CommonSchemaSamples.All;
        Assert.NotEmpty(samples);

        foreach (CommonSchemaSample sample in samples)
        {
            JsonSchema schema = CommonSchema(sample.Schema);
            string json = CommonSerializer.Serialize(sample.Value);
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
        Assert.Contains("v2/common/error", SdkSchemaCatalog.Ids);
        foreach (string id in SdkSchemaCatalog.Ids)
        {
            Assert.StartsWith("v2/common/", id, StringComparison.Ordinal);
            string text = SdkSchemaCatalog.Read(id);
            Assert.NotNull(CommonSchema(UriPrefix + id + ".schema.json"));
            JsonObject document = JsonNode.Parse(text)!.AsObject();
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                document["$schema"]?.GetValue<string>());
            Assert.Equal(
                UriPrefix + id + ".schema.json",
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
        string json = CommonSerializer.Serialize(sample);
        using JsonDocument instance = JsonDocument.Parse(json);
        JsonSchema schema = CommonSchema(ResultEnvelope.SchemaUri("common", "preview-session"));
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
        string json = CommonSerializer.Serialize(sample);
        JsonObject instance = JsonNode.Parse(json)!.AsObject();
        JsonSchema schema = CommonSchema(ResultEnvelope.SchemaUri("common", "doctor"));
        using JsonDocument complete = JsonDocument.Parse(json);
        Assert.Equal(valid, schema.Evaluate(complete.RootElement).IsValid);
        instance["products"]![0]!.AsObject().Remove("engine");
        using JsonDocument missingEngine = JsonDocument.Parse(instance.ToJsonString());
        Assert.False(schema.Evaluate(missingEngine.RootElement).IsValid);
    }

    [Fact]
    public void OperationDescriptor_SerializesInDeterministicContractOrder()
    {
        string json = CommonSerializer.Serialize(CommonSchemaSamples.Capabilities);
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
                """{"schema":"https://schemas.aspose.com/aspose-cli/v2/common/view.schema.json","schemaVersion":2,"view":"pages","sourceFormat":"docx","sourceSizeBytes":10,"sourceEncrypted":false,"totalPartCount":2,"parts":[{"id":"page-1","label":"Page 1","file":"page-0001.png","kind":"image","width":816,"height":1056,"hidden":false,"digest":"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","properties":{"notes":"Speaker notes"},"elements":[{"id":"shape-3","kind":"paragraph","box":{"x":96,"y":96.5,"width":624,"height":20},"digest":"f00d","label":"Hello","level":1}]}]}"""),
        ];

        foreach ((string id, string json) in contracts)
        {
            Assert.Contains(id, SdkSchemaCatalog.Ids);
            JsonSchema schema = CommonSchema(UriPrefix + id + ".schema.json");
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
        string[] results =
        [
            "app-result",
            "capabilities-summary",
            "doctor",
            "font-check",
            "font-list",
            "license-status",
            "preview-session",
            "preview-status",
            "review",
            "schema-list",
            "skill-install",
            "skill-list",
            "version",
        ];
        const string WarningUri = UriPrefix + "v2/common/warning.schema.json";

        foreach (string id in results)
        {
            JsonObject document = JsonNode.Parse(SdkSchemaCatalog.Read("v2/common/" + id))!.AsObject();
            Assert.Equal(WarningUri, document["properties"]!["warnings"]!["items"]!["$ref"]!.GetValue<string>());
        }

        JsonObject warning = JsonNode.Parse(SdkSchemaCatalog.Read("v2/common/warning"))!.AsObject();
        JsonObject properties = warning["properties"]!.AsObject();
        Assert.False(warning["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(1, properties["location"]!["minLength"]!.GetValue<int>());
        Assert.Equal(
            "boolean",
            properties["affectsCompleteness"]!["type"]!.GetValue<string>());

        JsonSchema schema = CommonSchema(WarningUri);
        using JsonDocument valid = JsonDocument.Parse(
            """{"code":"VISUAL_LIMITED","message":"Evidence is incomplete.","location":"page:2","affectsCompleteness":true}""");
        using JsonDocument invalid = JsonDocument.Parse(
            """{"code":"VISUAL_LIMITED","message":"Evidence is incomplete.","location":""}""");
        using JsonDocument unknown = JsonDocument.Parse(
            """{"code":"VISUAL_LIMITED","message":"Evidence is incomplete.","unexpected":true}""");
        Assert.True(schema.Evaluate(valid.RootElement).IsValid);
        Assert.False(schema.Evaluate(invalid.RootElement).IsValid);
        Assert.False(schema.Evaluate(unknown.RootElement).IsValid);
    }

    /// <summary>Names every integer count of the SDK and Host contracts <c>&lt;noun&gt;Count</c>.</summary>
    [Fact]
    public void IntegerCounts_AreNamedNounCount()
    {
        IReadOnlyList<string> violations = JsonCountNames.Violations(
            [typeof(SdkJsonContext).Assembly, typeof(Aspose.Cli.Host.Invocation.HostContext).Assembly]);
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ProductCatalog_RejectsAnOperationVocabularyOutsideTheProductNamespace()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => CreateCatalog("other", TestOp.Catalog.Describe));

        Assert.Contains("references unowned schema 'v2/common/ops'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A common schema by its canonical URI, with every common schema registered for its references.</summary>
    private static JsonSchema CommonSchema(string uri)
    {
        Assert.StartsWith(UriPrefix, uri, StringComparison.Ordinal);
        return Assert.IsType<JsonSchema>(CommonSchemas.SchemaRegistry.Get(new Uri(uri)));
    }

    private static ProductCatalog CreateCatalog(
        string productId = "test",
        Func<string, ProductOperationCommand>? operations = null)
    {
        ProductDefinition definition = ExtProduct.Define<ITestSession>(
                new ProductManifest
                {
                    Id = productId,
                    DisplayName = "Test",
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
            .View(new TestProductViewAdapter<ITestSession>())
            .Describe("A test product.")
            .Command(
                () => new CommandDefinition<TestMenu.TestRequest, TestResult>(
                    "edit",
                    "Edits.",
                    new CommandTraits(),
                    [],
                    static (_, _) => new TestMenu.TestRequest(),
                    static (_, _) => { })
                {
                    Operations = operations,
                },
                static (ITestSession _, TestMenu.TestRequest _) => new TestResult())
            .Activator(static _ =>
                throw new InvalidOperationException(
                    "Common schema tests must not activate a product session."))
            .Build();
        return ProductCatalog.Build([new StaticModule(definition)]);
    }

    private interface ITestSession;

#pragma warning disable APCLI003 // A test result, not a product JSON root.
    private sealed record TestResult() : TestResultEnvelope("result");
#pragma warning restore APCLI003

    private sealed class StaticModule(ProductDefinition definition) : IProductModule
    {
        public ProductDefinition Define() => definition;
    }

}
