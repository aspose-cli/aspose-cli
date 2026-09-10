using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Preview;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductPreviewDefinitionTests
{
    [Fact]
    public void ReviewDefinition_RejectsAnUndeclaredViewBeforeProductDispatch()
    {
        ProductReviewDefinition review = ProductReviewDefinition.Create(
            new TestProductReviewAdapter<ITestPort>(),
            "test");
        ProductBinding<ITestPort> binding =
            ProductBinding.CreateLicenseFree<ITestPort>(
                "test",
                static _ => new TestPort());

        CliException failure = Assert.Throws<CliException>(() =>
            review.CreateRenderer(
                binding,
                "file.test",
                new ProductReviewRequest("missing", MaxItems: 1)));

        Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
    }

    [Fact]
    public void ReviewDefinition_MissingFontsCannotRemainComplete()
    {
        ProductReviewDefinition review = ProductReviewDefinition.Create(
            new TestProductReviewAdapter<ITestPort>(),
            "test");
        ProductBinding<ITestPort> binding =
            ProductBinding.CreateLicenseFree<ITestPort, FontPort>(
                "test",
                static _ => new FontPort());

        ProductReviewRenderOutcome outcome = review.CreateRenderer(
            binding,
            "file.test",
            new ProductReviewRequest("document", MaxItems: 1))("evidence");

        Assert.False(outcome.Complete);
        Assert.Contains(
            outcome.Findings!,
            static finding => finding.Code == "FONTS_MISSING_OR_SUBSTITUTED"
                && finding.Severity == "error");
    }

    [Fact]
    public void ValidatePayload_RejectsDuplicatePropertiesWithoutRelyingOnHttp()
    {
        ProductPreviewDefinition preview = ProductPreviewDefinition.Create(
            new TestProductPreviewAdapter<ITestPort>(
            [
                new ProductPreviewPayloadContract
                {
                    Kind = ProductPreviewPayloadKinds.Selector,
                    SchemaVersion = 2,
                    SchemaId = "v2/test/selector",
                },
            ]),
            "test");
        using JsonDocument document = JsonDocument.Parse(
            """{"item":1,"nested":{"value":1,"value":2}}""");
        var payload = new ProductPreviewPayload
        {
            ProductId = "test",
            Kind = ProductPreviewPayloadKinds.Selector,
            SchemaVersion = 2,
            SchemaId = "v2/test/selector",
            Payload = document.RootElement.Clone(),
        };

        JsonException failure = Assert.Throws<JsonException>(() =>
            preview.ValidatePayload(
                payload,
                ProductPreviewPayloadKinds.Selector));

        Assert.Contains("$.nested property 'value' is duplicated", failure.Message);
    }

    private interface ITestPort;

    private sealed class TestPort : ITestPort;

    private sealed class FontPort : ITestPort, IFontEnvironment
    {
        public FontListResult ListFonts() => new() { Sources = [] };

        public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) => new()
        {
            Source = new SourceInfo
            {
                Path = filePath,
                Format = "test",
                SizeBytes = 0,
            },
            AllAvailable = false,
            Fonts = [new FontAvailability { Name = "Missing", Available = false }],
        };
    }
}
