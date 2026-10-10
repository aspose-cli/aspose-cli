using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Extensibility;
using Xunit;

namespace Aspose.Cli.Product.Cells.Tests;

/// <summary>Cells composition, routing, schema, and package invariants.</summary>
public sealed class CellsModuleTests
    : ProductContractTests<CellsModule>
{
    protected override IReadOnlyList<ProductSchemaSample> CanonicalInputs { get; } =
        [new(CellsOp.Catalog.SchemaId, CellsOpsSample.Batch)];

    protected override IReadOnlyDictionary<string, string> Homonyms { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["left"] = "a diff or verification change's value in the first file, not a page margin",
            ["right"] = "a diff or verification change's value in the second file, not a page margin",
        };

    [Theory]
    [InlineData("chartInfo", "create_chart")]
    [InlineData("validationInfo", "set_validation")]
    public void InspectedTypes_StateTheOperationVocabulary(string info, string operation)
    {
        ProductCatalog catalog = ProductCatalog.Build([new CellsModule()]);
        JsonNode read = JsonNode.Parse(catalog.Resources.Read("v2/cells/workbook-info"))!;
        JsonNode ops = JsonNode.Parse(catalog.Resources.Read("v2/cells/ops"))!;

        Assert.Equal(
            ops["$defs"]![operation]!["properties"]!["type"]!["enum"]!.ToJsonString(),
            read["$defs"]![info]!["properties"]!["type"]!["anyOf"]![0]!["enum"]!.ToJsonString());
    }

    [Fact]
    public void OperationBatch_RejectsNullOperations()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            ParseOps("""{"ops":null}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Theory]
    [InlineData("""{"op":"set_formula","range":"A1","formula":null}""")]
    [InlineData("""{"op":"set_values","range":"A1","values":null}""")]
    [InlineData("""{"op":"rename_sheet","sheet":"Sheet1","to":null}""")]
    public void NonNullableFields_RejectExplicitNullAsInvalidOperations(string operation)
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() =>
            ParseOps($$"""{"ops":[{{operation}}]}"""));
        Assert.Equal(Aspose.Cli.Sdk.Errors.ErrorCodes.OpsInvalid, error.Code);
    }

    [Fact]
    public void JsonRejections_NameTheOperationAndFieldWithoutClrTypes()
    {
        var error = Assert.Throws<Aspose.Cli.Sdk.Errors.CliException>(() => ParseOps(
            """{"ops":[{"op":"set_values","range":"A1","values":[[1]]},{"op":"format_range","range":"A1","style":{"bold":true,"shiny":1}}]}"""));

        Assert.Equal(1, error.Details!["index"]!.GetValue<int>());
        Assert.Equal("format_range", error.Details["op"]!.GetValue<string>());
        Assert.StartsWith("unknown field 'style.shiny'; style accepts: ", error.Details["reason"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("Aspose.Cli", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationObjects_RejectUnknownNestedFields() =>
        AssertOperationObjectIsStrict<CellsOp>(
            """{"op":"format_range","range":"A1","style":{"bold":true}}""", "style");

    [Fact]
    public Task EveryDefaultInputFormatHasPositiveRoutingEvidence() =>
        ProductRoutingContract.AssertPositiveRoutesAsync<CellsModule>(
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["xlsx"] = ProductRoutingContract.ZipMarker("xl/workbook.xml"),
                ["xltx"] = ProductRoutingContract.ZipMarker("xl/workbook.xml"),
                ["xlsm"] = ProductRoutingContract.ZipMarker("xl/workbook.xml"),
                ["xltm"] = ProductRoutingContract.ZipMarker("xl/workbook.xml"),
                ["xlsb"] = ProductRoutingContract.ZipMarker("xl/workbook.bin"),
                ["xls"] = ProductRoutingContract.CompoundFile(),
                ["ods"] = ProductRoutingContract.ZipMarker(
                    "application/vnd.oasis.opendocument.spreadsheet"),
                ["csv"] = ProductRoutingContract.Utf8("Name,Value\nA,1\n"),
                ["tsv"] = ProductRoutingContract.Utf8("Name\tValue\nA\t1\n"),
            });

    [Fact]
    public void CanonicalOps_CoverEveryRegisteredOperation() =>
        Assert.Equal(
            CellsOp.Catalog.Names.Order(StringComparer.Ordinal),
            CellsOpsSample.Batch.Ops
                .Select(CellsOp.Catalog.NameOf)
                .Order(StringComparer.Ordinal));

    private static CellsOpsBatch ParseOps(string json) =>
        CellsOp.Catalog.Parse<CellsOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
