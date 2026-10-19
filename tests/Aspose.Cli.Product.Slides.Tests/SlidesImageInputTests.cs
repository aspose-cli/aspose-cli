using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Aspose.Slides adds any bytes as a picture, so the CLI checks every image before the engine
/// sees it.
/// </summary>
public sealed class SlidesImageInputTests
{
    [Fact]
    public void InsertImage_RefusesAFileThatIsNotAnImageAsAnInvalidOperation()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("deck.pptx", slides: 1);
        string picture = fixture.File("notes.png");
        File.WriteAllText(picture, "not an image");
        string output = fixture.File("deck.out.pptx");

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SetNotesOp { Slide = 1, Text = "First." }, new SlidesInsertImageOp { Slide = 1, Path = picture }],
            },
            Output = TestOutput.At(output),
        }));

        OperationErrors.AssertInvalid(error, 1, "insert_image", picture);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void SetBackground_RefusesATruncatedImageAsAnInvalidOperation()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("deck.pptx", slides: 2);
        string picture = fixture.File("cut.png");
        byte[] png = SlidesEngineFixture.Png(40);
        File.WriteAllBytes(picture, png[..(png.Length / 2)]);
        string output = fixture.File("deck.out.pptx");

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetBackgroundOp { Path = picture }] },
            Output = TestOutput.At(output),
        }));

        OperationErrors.AssertInvalid(error, 0, "set_background", picture);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void CreateFromMarkdown_RefusesATruncatedImageAsACorruptFileNamingIt()
    {
        using var workspace = new TempWorkspace();
        byte[] png = SlidesEngineFixture.Png(40);
        File.WriteAllBytes(workspace.File("chart.png"), png[..(png.Length / 2)]);
        File.WriteAllText(workspace.File("outline.md"), "# Results\n\n![Chart](chart.png)\n");

        CliResult created = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--output-format", "json");

        Assert.True(created.ExitCode == 3, created.StdErr);
        JsonNode error = JsonNode.Parse(created.StdErr)!["error"]!;
        Assert.Equal("FILE_CORRUPT", error["code"]!.GetValue<string>());
        Assert.Equal(workspace.File("chart.png"), error["details"]!["path"]!.GetValue<string>(), ignoreCase: true);
        Assert.False(File.Exists(workspace.File("deck.pptx")));
    }

    [Fact]
    public void InsertImage_RefusesACompressedSvgAsPdfAndCellsDo()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation("deck.pptx", slides: 1);
        string picture = fixture.File("diagram.svgz");
        File.WriteAllBytes(picture, Gzip("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"9\" height=\"9\"/>"));
        string output = fixture.File("deck.out.pptx");

        CliException error = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SlidesInsertImageOp { Slide = 1, Path = picture }] },
            Output = TestOutput.At(output),
        }));

        Assert.Equal(ErrorCodes.FeatureUnsupported, error.Code);
        Assert.Equal("op-0001", error.Details!["id"]!.GetValue<string>());
        Assert.Contains("SVGZ", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    private static byte[] Gzip(string text)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(text));
        }

        return compressed.ToArray();
    }
}
