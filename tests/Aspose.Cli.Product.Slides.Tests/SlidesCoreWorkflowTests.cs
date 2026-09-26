using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesCoreWorkflowTests
{
    [Fact]
    public void Parse_AcceptsVersionTwoAndRejectsVersionOne()
    {
        SlidesOpsBatch batch = ParseOps(
            """{"schemaVersion":2,"ops":[{"op":"set_properties","title":"Briefing"}]}""");

        Assert.Equal(2, batch.SchemaVersion);
        Assert.Equal(
            ErrorCodes.OpsInvalid,
            Assert.Throws<CliException>(() => ParseOps(
                """{"schemaVersion":1,"ops":[{"op":"set_properties","title":"Briefing"}]}""")).Code);
    }

    [Fact]
    public void View_WritesSlidePartsThroughSinkAndPreservesPassword()
    {
        const string password = "view-password";
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("view.pptx", slides: 2, password);
        var artifacts = new MemoryArtifactSink();

        ViewManifest manifest = fixture.Engine.RenderView(
            input,
            new ViewRenderRequest
            {
                View = SlidesViews.Slides,
                MaxParts = 8,
                Purpose = ViewPurpose.Display,
                Password = password,
            },
            artifacts);

        Assert.Equal(SlidesViews.Slides, manifest.View);
        Assert.Equal("pptx", manifest.SourceFormat);
        Assert.Equal(new FileInfo(input).Length, manifest.SourceSizeBytes);
        Assert.Equal(2, manifest.TotalParts);
        // The evaluation watermark carries the SDK's truncation marker, so evaluation discloses possible truncation.
        string[] expected = fixture.LicenseState == Aspose.Cli.Sdk.Licensing.LicenseState.Licensed
            ? []
            : [WarningCodes.EvalInputTruncated];
        Assert.Equal(expected, manifest.Warnings?.Select(static warning => warning.Code) ?? []);
        Assert.Equal(["slide-0001.png", "slide-0002.png"], artifacts.Paths);
        Assert.Equal(
            ["slide-0001.png", "slide-0002.png"],
            manifest.Parts.Select(static part => part.File));
        Assert.All(manifest.Parts, static part => Assert.Equal(ViewPartKinds.Image, part.Kind));
        Assert.True(IsPng(artifacts.Bytes("slide-0001.png")));
        Assert.True(IsPng(artifacts.Bytes("slide-0002.png")));
    }

    [Fact]
    public void CreateSearchAndExtract_CoverCoreArtifactFlows()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("briefing.md");
        File.WriteAllText(
            markdown,
            "# Revenue briefing\n\nRevenue grew in the current quarter.");
        string presentation = fixture.File("briefing.pptx");

        SlidesCreateResult created = fixture.Engine.Create(new NewPresentationRequest
        {
            MarkdownPath = markdown,
            OutputPath = presentation,
        });

        Assert.True(File.Exists(presentation));
        Assert.True(created.Slides > 0);

        string searchable = fixture.CreatePresentation("searchable.pptx", slides: 1);

        SlidesSearchResult search = fixture.Engine.Search(
            searchable,
            new PresentationSearchRequest
            {
                Query = new SearchQuery(TextSearch.Create("Slide", regex: false, caseSensitive: false), 1, Scope: null),
            });
        SlidesSearchHit hit = Assert.Single(search.Hits);
        Assert.Contains("Slide", hit.Text, StringComparison.Ordinal);

        SlidesExtractResult extracted = fixture.Engine.Extract(
            searchable,
            new PresentationExtractRequest
            {
                What = PresentationExtractKinds.Text,
                OutputDirectory = fixture.File("text"),
            });
        Assert.NotEmpty(extracted.Items);
        Assert.All(extracted.Items, item =>
        {
            Assert.Equal("text", item.Kind);
            Assert.True(File.Exists(item.Path));
        });
        Assert.Contains(
            extracted.Items,
            item => File.ReadAllText(item.Path).Contains("Slide", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("pptx")]
    [InlineData("pdf")]
    [InlineData("svg")]
    [InlineData("md")]
    public void Convert_ProducesOneRepresentativeFromEachExporterFamily(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("convert.pptx", slides: 2);
        string output = fixture.File("converted" + SlidesFormats.Definitions.ExtensionFor(format));

        SlidesConvertResult result = fixture.Engine.Convert(
            input,
            new PresentationConvertRequest
            {
                TargetFormatId = format,
                OutputPath = output,
                Slides = PageRange.Parse("1"),
            });

        OutputInfo artifact = Assert.Single(result.Outputs);
        Assert.Equal(format, artifact.Format);
        Assert.True(File.Exists(artifact.Path));
        Assert.True(artifact.SizeBytes > 0);
        if (fixture.LicenseState != Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Contains(
                result.Warnings ?? [],
                static warning => warning.Code == WarningCodes.EvalMode);
        }
    }

    private static bool IsPng(byte[] content) =>
        content.AsSpan().StartsWith(
            new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });

    private static SlidesOpsBatch ParseOps(string json) =>
        SlidesOp.Catalog.Parse<SlidesOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
