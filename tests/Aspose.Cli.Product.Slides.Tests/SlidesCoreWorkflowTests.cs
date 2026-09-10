using System.Text;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Preview;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesCoreWorkflowTests
{
    [Fact]
    public void Parse_AcceptsVersionTwoAndRejectsVersionOne()
    {
        SlidesOpsBatch batch = SlidesOpsParser.Parse(
            """{"schemaVersion":2,"ops":[{"op":"set_properties","title":"Briefing"}]}""");

        Assert.Equal(2, batch.SchemaVersion);
        Assert.Equal(
            ErrorCodes.OpsInvalid,
            Assert.Throws<CliException>(() => SlidesOpsParser.Parse(
                """{"schemaVersion":1,"ops":[{"op":"set_properties","title":"Briefing"}]}""")).Code);
    }

    [Fact]
    public void Preview_WritesArtifactsThroughSinkAndPreservesPassword()
    {
        const string password = "preview-password";
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("preview.pptx", slides: 2, password);
        var artifacts = new MemoryArtifactSink();

        PreviewRenderOutcome result = fixture.Engine.RenderPreview(
            input,
            new PresentationPreviewRequest { Password = password },
            artifacts);

        Assert.Equal("presentation.html", result.EntryFileName);
        Assert.Equal("pptx", result.SourceFormatId);
        Assert.Equal(new FileInfo(input).Length, result.SourceSizeBytes);
        Assert.Null(result.Warnings);
        Assert.Equal(
            ["presentation.html", "slide-0001.png", "slide-0002.png"],
            artifacts.Paths);
        string html = artifacts.Text("presentation.html");
        Assert.Contains("/asset/slide-0001.png", html, StringComparison.Ordinal);
        Assert.Contains("/asset/slide-0002.png", html, StringComparison.Ordinal);
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
                Pattern = "Slide",
                MaxHits = 1,
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
        string output = fixture.File("converted" + SlidesFormats.Extension(format));

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

    private sealed class MemoryArtifactSink : IPreviewArtifactSink
    {
        private readonly Dictionary<string, byte[]> _artifacts =
            new(StringComparer.Ordinal);

        public IReadOnlyList<string> Paths =>
            _artifacts.Keys.Order(StringComparer.Ordinal).ToArray();

        public void Write(string relativePath, Action<Stream> contentWriter)
        {
            using var stream = new MemoryStream();
            contentWriter(stream);
            _artifacts.Add(relativePath, stream.ToArray());
        }

        public void WriteText(string relativePath, string content) =>
            _artifacts.Add(relativePath, Encoding.UTF8.GetBytes(content));

        public byte[] Bytes(string relativePath) => _artifacts[relativePath];

        public string Text(string relativePath) =>
            Encoding.UTF8.GetString(Bytes(relativePath));
    }
}
