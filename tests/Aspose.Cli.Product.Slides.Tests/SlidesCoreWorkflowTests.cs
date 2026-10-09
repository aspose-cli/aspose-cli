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

        ViewManifest manifest = SlidesView.Render(
            fixture.Session,
            input,
            new ViewRenderRequest
            {
                View = SlidesViews.Slides,
                MaxPartCount = 8,
                Purpose = ViewPurpose.Display,
                Password = new Secret(password),
            },
            artifacts);

        Assert.Equal(SlidesViews.Slides, manifest.View);
        Assert.Equal("pptx", manifest.SourceFormat);
        Assert.Equal(new FileInfo(input).Length, manifest.SourceSizeBytes);
        Assert.True(manifest.SourceEncrypted);
        Assert.Equal(2, manifest.TotalPartCount);
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

        SlidesCreateResult created = SlidesCreate.Run(fixture.Session, new NewPresentationRequest
        {
            Output = TestOutput.At(presentation),
            MarkdownPath = markdown,
        });

        Assert.True(File.Exists(presentation));
        Assert.True(created.SlideCount > 0);

        string searchable = fixture.CreatePresentation("searchable.pptx", slides: 1);

        SlidesSearchResult search = SlidesSearch.Run(
            fixture.Session,
            new PresentationSearchRequest
            {
                Input = searchable,
                Query = new SearchQuery(TextSearch.Create("Slide", regex: false, caseSensitive: false), 1, Scope: null),
            });
        SlidesSearchHit hit = Assert.Single(search.Hits);
        Assert.Contains("Slide", hit.Text, StringComparison.Ordinal);

        SlidesExtractResult extracted = SlidesExtract.Run(
            fixture.Session,
            new PresentationExtractRequest
            {
                Input = searchable,
                Output = new ResolvedDirectory(fixture.File("text")),
                What = PresentationExtractKinds.Text,
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

        SlidesConvertResult result = fixture.Disclosed(session => SlidesExport.Convert(
            session,
            new PresentationConvertRequest
            {
                Input = input,
                Output = TestOutput.At(output, format: format),
                Slides = PageRange.Parse("1"),
            }));

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
