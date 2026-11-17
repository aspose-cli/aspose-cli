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

    [Fact]
    public void NotFound_ListsBoundedAvailableNamesThatMatchTheSchema()
    {
        string[] names = [.. Enumerable.Range(1, 60).Select(static index => $"Sheet{index}"), "Sales"];

        CliException error = CliErrors.NotFound(SheetNotFound, "sheet", "Salez", names);
        Assert.Equal(61, error.Details!["availableCount"]!.GetValue<int>());
        Assert.Equal(CliErrors.MaximumAvailableNames, error.Details["available"]!.AsArray().Count);
        JsonSchema schema = JsonSchema.FromText(SdkSchemaCatalog.Read(NotFoundDetails.CatalogId));
        using JsonDocument details = JsonDocument.Parse(error.Details.ToJsonString());
        Assert.True(schema.Evaluate(details.RootElement).IsValid);
    }

    [Theory]
    [InlineData(1, "No page '4' was found; 1 exists.")]
    [InlineData(3, "No page '4' was found; 3 exist.")]
    [InlineData(0, "No page '4' was found; 0 exist.")]
    public void NotFoundAt_CountsTheTargetsInAgreement(int count, string message) =>
        Assert.Equal(message, CliErrors.NotFoundAt(ErrorCodes.PageNotFound, "page", "4", count).Message);

    [Fact]
    public void NotFound_AcceptsOnlyCodesDeclaredAsNotFoundAndPublishesTheirSchema()
    {
        Assert.Throws<ArgumentException>(() => CliErrors.NotFound(ErrorCodes.OpsInvalid, "sheet", "x", []));
        Assert.Throws<ArgumentException>(() => CliErrors.NotFoundAt(ErrorCodes.OpsInvalid, "page", "4", 3));
        Assert.Equal(
            NotFoundDetails.CatalogId,
            DiagnosticDescriptor.Error(SheetNotFound, "cells").DetailsSchemaId);
    }
}
