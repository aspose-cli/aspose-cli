using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Resources;
using Aspose.Cli.Sdk.Text;
using Json.Schema;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Sdk;

public sealed class NotFoundErrorTests
{
    private static readonly ErrorCode SheetNotFound = ErrorCode.NotFound("SHEET_NOT_FOUND");

    [Theory]
    [InlineData("sales ", new[] { "Sales", "Sales 2026" })]
    [InlineData("Salez", new[] { "Sales" })]
    [InlineData("Slaes", new[] { "Sales" })]
    [InlineData("2026", new[] { "Sales 2026" })]
    [InlineData("Q1", new[] { "Q4" })]
    [InlineData("Inventory", new string[0])]
    public void Closest_RanksCaseSlipsThenContainmentThenSmallEdits(string requested, string[] expected) =>
        Assert.Equal(expected, NameSuggestions.Closest(requested, ["Summary", "Sales", "Q4", "Sales 2026"]));

    [Fact]
    public void NotFound_ListsBoundedAvailableNamesAndSuggestionsThatMatchTheSchema()
    {
        string[] names = [.. Enumerable.Range(1, 60).Select(static index => $"Sheet{index}"), "Sales"];

        CliException error = CliErrors.NotFound(SheetNotFound, "sheet", "Salez", names);

        Assert.Equal("Did you mean 'Sales'?", error.Hint);
        Assert.Equal(61, error.Details!["availableCount"]!.GetValue<int>());
        Assert.Equal(CliErrors.MaximumAvailableNames, error.Details["available"]!.AsArray().Count);
        Assert.Equal("Sales", error.Details["suggestions"]![0]!.GetValue<string>());
        JsonSchema schema = JsonSchema.FromText(SdkSchemaCatalog.Read(CommonSchemaIds.NotFoundDetails));
        using JsonDocument details = JsonDocument.Parse(error.Details.ToJsonString());
        Assert.True(schema.Evaluate(details.RootElement).IsValid);
    }

    [Fact]
    public void NotFound_AcceptsOnlyCodesDeclaredAsNotFoundAndPublishesTheirSchema()
    {
        Assert.Throws<ArgumentException>(() => CliErrors.NotFound(ErrorCodes.OpsInvalid, "sheet", "x", []));
        Assert.Throws<ArgumentException>(() => CliErrors.NotFoundAt(ErrorCodes.OpsInvalid, "page", "4", 3));
        Assert.Equal(
            CommonSchemaIds.NotFoundDetails,
            DiagnosticDescriptor.Error(SheetNotFound, "cells", "validation").DetailsSchemaId);
    }
}
