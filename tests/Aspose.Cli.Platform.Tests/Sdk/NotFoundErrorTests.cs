using System.Text.Json;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Serialization;
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

    [Theory]
    [InlineData("fontSize", new[] { "size", "font" })]
    [InlineData("fontName", new[] { "name", "font" })]
    [InlineData("borderColor", new[] { "color", "border" })]
    [InlineData("italicFont", new[] { "font" })]
    public void Closest_PrefersTheContainedNameThatEndsACompoundFieldName(string requested, string[] expected) =>
        Assert.Equal(expected, NameSuggestions.Closest(requested, ["font", "name", "size", "color", "border", "bold"], fieldNames: true));

    [Theory]
    [InlineData("replacement", new[] { "replace", "maxReplacementCount" })]
    [InlineData("replaceWith", new[] { "replace" })]
    [InlineData("count", new[] { "maxReplacementCount" })]
    public void Closest_PrefersTheFieldNameSharingTheRequestsStemOverOneThatHoldsItInside(string requested, string[] expected) =>
        Assert.Equal(expected, NameSuggestions.Closest(requested, ["find", "maxReplacementCount", "replace"], fieldNames: true));

    [Theory]
    [InlineData("subtitle", new[] { "title", "body" }, new string[0])]
    [InlineData("Sub title", new[] { "title" }, new[] { "title" })]
    [InlineData("Quarter", new[] { "Quarterly report" }, new[] { "Quarterly report" })]
    [InlineData("销售额汇总表", new[] { "销售额", "利润表" }, new[] { "销售额" })]
    public void Closest_SuggestsANameInsideTheRequestOnlyWhenItIsAWholeWordOfIt(
        string requested, string[] candidates, string[] expected) =>
        Assert.Equal(expected, NameSuggestions.Closest(requested, candidates));

    [Fact]
    public void Closest_KeepsTheOrderOfContainingNamesOtherThanFieldNames() =>
        Assert.Equal(["Sales 2026", "Old Sales"], NameSuggestions.Closest("Sales", ["Sales 2026", "Old Sales"]));

    [Fact]
    public void NotFound_ListsBoundedAvailableNamesAndSuggestionsThatMatchTheSchema()
    {
        string[] names = [.. Enumerable.Range(1, 60).Select(static index => $"Sheet{index}"), "Sales"];

        CliException error = CliErrors.NotFound(SheetNotFound, "sheet", "Salez", names);

        Assert.Equal("Did you mean 'Sales'?", error.Hint);
        Assert.Equal(61, error.Details!["availableCount"]!.GetValue<int>());
        Assert.Equal(CliErrors.MaximumAvailableNames, error.Details["available"]!.AsArray().Count);
        Assert.Equal("Sales", error.Details["suggestions"]![0]!.GetValue<string>());
        JsonSchema schema = JsonSchema.FromText(SdkSchemaCatalog.Read(NotFoundDetails.CatalogId));
        using JsonDocument details = JsonDocument.Parse(error.Details.ToJsonString());
        Assert.True(schema.Evaluate(details.RootElement).IsValid);
    }

    [Fact]
    public void NotFound_AcceptsOnlyCodesDeclaredAsNotFoundAndPublishesTheirSchema()
    {
        Assert.Throws<ArgumentException>(() => CliErrors.NotFound(ErrorCodes.OpsInvalid, "sheet", "x", []));
        Assert.Throws<ArgumentException>(() => CliErrors.NotFoundAt(ErrorCodes.OpsInvalid, "page", "4", 3));
        Assert.Equal(
            NotFoundDetails.CatalogId,
            DiagnosticDescriptor.Error(SheetNotFound, "cells", "validation").DetailsSchemaId);
    }
}
