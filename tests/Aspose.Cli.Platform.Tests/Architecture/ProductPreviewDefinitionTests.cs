using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Views;
using Xunit;

namespace Aspose.Cli.Architecture.Tests;

public sealed class ProductPreviewDefinitionTests
{
    [Fact]
    public void ViewDefinition_RejectsAnUndeclaredViewBeforeProductDispatch()
    {
        ProductViewDefinition views = ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestPort>(),
            "test");
        ProductBinding<ITestPort> binding =
            ProductBinding.CreateLicenseFree<ITestPort>(
                "test",
                static _ => new TestPort());

        CliException failure = Assert.Throws<CliException>(() =>
            views.Render(binding, "file.test", Request("missing"), new RejectingSink()));

        Assert.Equal(ErrorCodes.OptionInvalid, failure.Code);
    }

    [Fact]
    public void ViewDefinition_MissingFontsCannotRemainComplete()
    {
        ProductViewDefinition views = ProductViewDefinition.Create(
            new TestProductViewAdapter<ITestPort>(),
            "test");
        ProductBinding<ITestPort> binding =
            ProductBinding.CreateLicenseFree<ITestPort, FontPort>(
                "test",
                static _ => new FontPort());
        ViewRenderRequest request = Request("document");

        ViewManifest rendered = views.Render(binding, "file.test", request, new RejectingSink());
        ProductReviewAssessment assessment = views.Assess(binding, "file.test", request, rendered);

        Assert.False(assessment.Complete);
        Assert.Contains(
            assessment.Findings!,
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

    private static ViewRenderRequest Request(string view) => new()
    {
        View = view,
        MaxParts = 1,
        Purpose = ViewPurpose.Evidence,
    };

    private sealed class RejectingSink : IViewArtifactSink
    {
        public void Write(string relativePath, Action<Stream> contentWriter) =>
            throw new InvalidOperationException("The test view writes no artifacts.");

        public void WriteText(string relativePath, string content) =>
            throw new InvalidOperationException("The test view writes no artifacts.");
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
