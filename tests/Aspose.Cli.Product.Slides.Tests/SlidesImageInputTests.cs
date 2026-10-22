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

    /// <summary>
    /// An SVG beside the Markdown that names a picture outside the Markdown's directory, by an
    /// absolute path, a parent path or a file URI, is embedded without it: no part of the
    /// presentation holds the picture or its name, and the result reports the removed reference.
    /// </summary>
    [Theory]
    [InlineData("absolute")]
    [InlineData("parent")]
    [InlineData("uri")]
    public void CreateFromMarkdown_AnSvgNamingAPictureOutsideTheDirectory_KeepsThePictureOut(string reference)
    {
        using var workspace = new TempWorkspace();
        string outside = Directory.CreateDirectory(workspace.File("outside")).FullName;
        Directory.CreateDirectory(workspace.File("deck"));
        byte[] sentinel = SlidesEngineFixture.Png(41);
        string sentinelPath = Path.Combine(outside, "sentinel.png");
        File.WriteAllBytes(sentinelPath, sentinel);
        string href = reference switch
        {
            "absolute" => sentinelPath,
            "parent" => "../outside/sentinel.png",
            _ => new Uri(sentinelPath).AbsoluteUri,
        };
        File.WriteAllText(workspace.File("deck/diagram.svg"),
            $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="40"><rect width="40" height="40" fill="#0000ff"/><image xlink:href="{System.Security.SecurityElement.Escape(href)}" width="40" height="40"/></svg>""");
        File.WriteAllText(workspace.File("deck/outline.md"), "## Diagram\n\n![Diagram](diagram.svg)\n");

        CliResult created = workspace.Run("slides", "create", "deck/deck.pptx", "--from-markdown", "deck/outline.md", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        foreach (byte[] part in Parts(workspace.File("deck/deck.pptx")))
        {
            Assert.False(part.AsSpan().SequenceEqual(sentinel));
            Assert.DoesNotContain("sentinel", System.Text.Encoding.Latin1.GetString(part), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(JsonNode.Parse(created.StdOut)!["warnings"]!.AsArray(),
            static warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED");
    }

    /// <summary>
    /// A Markdown SVG with a document type, as editors export it, is embedded; one that does not
    /// parse as XML is left out and reported, and the presentation is still written.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateFromMarkdown_EmbedsAnSvgWithADocumentTypeAndOmitsOneThatDoesNotParse(bool parses)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("diagram.svg"), parses
            ? """<?xml version="1.0"?><!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"><svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><rect width="40" height="40" fill="#0000ff"/></svg>"""
            : """<svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><g></svg>""");
        File.WriteAllText(workspace.File("outline.md"), "## Diagram\n\n![Diagram](diagram.svg)\n");

        CliResult created = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(workspace.File("deck.pptx"));
        Assert.Equal(parses, archive.Entries.Any(static entry => entry.FullName.StartsWith("ppt/media/", StringComparison.Ordinal)));
        if (!parses)
        {
            Assert.Contains(JsonNode.Parse(created.StdOut)!["warnings"]!.AsArray(),
                static warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED");
        }
    }

    private static IEnumerable<byte[]> Parts(string package)
    {
        using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(package);
        foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries.ToArray())
        {
            using Stream stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            yield return buffer.ToArray();
        }
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
