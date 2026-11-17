using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
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
    public void CreateSearchAndExtract_CoverCoreArtifactFlows()
    {
        using var fixture = new SlidesEngineFixture();
        string markdown = fixture.File("briefing.md");
        File.WriteAllText(
            markdown,
            "# Revenue briefing\n\nRevenue grew in the current quarter.");
        string presentation = fixture.File("briefing.pptx");

        SlidesCreateResult created = SlidesCreate.Run(fixture.Session, new SlidesCreateRequest
        {
            Output = TestOutput.At(presentation),
            Markdown = new SourceFile("--from-markdown", markdown),
        });

        Assert.True(File.Exists(presentation));
        Assert.True(created.SlideCount > 0);

        string searchable = fixture.CreatePresentation("searchable.pptx", slides: 1);

        SlidesSearchResult search = SlidesSearch.Run(
            fixture.Session,
            new SlidesSearchRequest
            {
                Input = searchable,
                Query = new SearchQuery(TextSearch.Create("Slide", regex: false, caseSensitive: false), 1, Scope: null),
            });
        SlidesSearchHit hit = Assert.Single(search.Hits);
        Assert.Contains("Slide", hit.Text, StringComparison.Ordinal);

        SlidesExtractResult extracted = SlidesExtract.Run(
            fixture.Session,
            new SlidesExtractRequest
            {
                Input = searchable,
                Output = new ResolvedDirectory(fixture.File("text")),
                What = SlidesExtractKinds.Text,
            });
        Assert.NotEmpty(extracted.Outputs);
        Assert.All(extracted.Outputs, item =>
        {
            Assert.NotNull(item.Slide);
            Assert.True(File.Exists(item.Path));
        });
        Assert.Contains(
            extracted.Outputs,
            item => File.ReadAllText(item.Path).Contains("Slide", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("pptx")]
    [InlineData("pdf")]
    [InlineData("gif")]
    [InlineData("md")]
    public void Convert_ProducesOneRepresentativeFromEachExporterFamily(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("convert.pptx", slides: 2);
        string output = fixture.File("converted" + SlidesFormats.Definitions.ExtensionOf(format));

        SlidesConvertResult result = fixture.Disclosed(session => SlidesExport.Convert(
            session,
            new SlidesConvertRequest
            {
                Input = input,
                Output = TestOutput.At(output, format: format),
                Slides = PageRange.Parse("1"),
            }));

        SlidesConvertOutput artifact = Assert.Single(result.Outputs);
        Assert.Equal(format, artifact.Format);
        Assert.True(File.Exists(artifact.Path));
        Assert.True(artifact.SizeBytes > 0);
        if (fixture.LicenseState != Sdk.Licensing.LicenseState.Licensed)
        {
            Assert.Contains(
                result.Warnings ?? [],
                static warning => warning.Code == WarningCodes.EvaluationMode);
        }
    }

    private static bool IsPng(byte[] content) =>
        content.AsSpan().StartsWith(
            new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });

    private static SlidesOpsBatch ParseOps(string json) =>
        SlidesOp.Catalog.Parse<SlidesOpsBatch>(json, Aspose.Cli.Generated.ProductJsonContext.Definition);
}
