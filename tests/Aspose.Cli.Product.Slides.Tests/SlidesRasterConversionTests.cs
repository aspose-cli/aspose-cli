using System.Text.Json.Nodes;
using Aspose.Slides.Export;
using Aspose.Slides;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

public sealed class SlidesRasterConversionTests(SlidesRasterConversionTests.CreatedDeck created) : IClassFixture<SlidesRasterConversionTests.CreatedDeck>
{
    /// <summary>
    /// An image output without an extension, such as deliver\png that reads as a folder, is
    /// refused before anything is read or written.
    /// </summary>
    [Fact]
    public void ImageOutput_WithoutAnExtensionIsRefused()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 2);
        using var workspace = new TempWorkspace();
        File.Copy(input, workspace.File("deck.pptx"));

        CliResult refused = workspace.Run("slides", "render", "deck.pptx", "--to", "png", "--out", "png", "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(
            $"Give --out the .png extension, as in png.png, or name a file inside the png folder, as in {Path.Combine("png", "page.png")}; "
                + "an output of several parts is written beside that file as numbered files.",
            error["hint"]!.GetValue<string>());
        Assert.Equal(["deck.pptx"], Directory.GetFileSystemEntries(workspace.Path).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("--dpi", "96")]
    [InlineData("--width", "640")]
    public void Render_RefusesAPixelSizeForSvgAsAnInvalidOption(string option, string value)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 1);
        using var workspace = new TempWorkspace();
        File.Copy(input, workspace.File("deck.pptx"));

        CliResult refused = workspace.Run(
            "slides", "render", "deck.pptx", option, value, "--out", "slide.svg", "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal(option, error["details"]!["option"]!.GetValue<string>());
        Assert.Equal(["deck.pptx"], Directory.GetFileSystemEntries(workspace.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void Render_RefusesWidthTogetherWithDpiAsAnInvalidWidth()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 1);
        using var workspace = new TempWorkspace();
        File.Copy(input, workspace.File("deck.pptx"));

        CliResult refused = workspace.Run(
            "slides", "render", "deck.pptx", "--dpi", "96", "--width", "640", "--out", "slide.png", "--output-format", "json");

        Assert.Equal(2, refused.ExitCode);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--width", error["details"]!["option"]!.GetValue<string>());
        Assert.Contains("cannot be combined with --dpi", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(
            "Drop --dpi to render at an exact pixel width, or drop --width to render at a DPI.",
            error["hint"]!.GetValue<string>());
        Assert.Equal(["deck.pptx"], Directory.GetFileSystemEntries(workspace.Path).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    public void RasterOutput_UsesManagedStreamsForLongPublicationPaths(string format)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.CreatePresentation(slides: 1);
        string directory = fixture.File(Path.Combine(new string('a', 90), new string('b', 90), new string('c', 90)));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "slide" + SlidesFormats.Definitions.ExtensionFor(format));
        Assert.True(output.Length > 260);

        SlidesExport.Render(fixture.Session, new SlidesRenderRequest
        {
            Input = input,
            Output = TestOutput.At(output, format: format),
        });

        using FileStream stream = File.OpenRead(output);
        using IImage image = Images.FromStream(stream);
        Assert.Equal(1920, image.Width);
        Assert.Equal(1440, image.Height);
        Assert.Empty(Directory.GetFiles(directory, "*.stage"));
    }
    [Fact]
    public void Edit_WritesLongPublicationPathsThroughRealCli()
    {
        using var workspace = new TempWorkspace();
        created.CopyTo(workspace.File("deck.pptx"));
        File.WriteAllText(workspace.File("ops.json"), """{"ops":[{"op":"set_notes","slide":1,"text":"Review note"}]}""");
        string directory = workspace.File(Path.Combine(new string('a', 90), new string('b', 90), new string('c', 90)));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "edited.pptx");
        Assert.True(output.Length > 260);
        CliResult edited = workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", output, "--output-format", "json");
        Assert.True(edited.ExitCode == 0, edited.StdErr);
        Assert.Equal(output, JsonNode.Parse(edited.StdOut)!["output"]!["path"]!.GetValue<string>());
        Assert.True(File.Exists(output));
        Assert.Empty(Directory.GetFiles(directory, "*.stage"));
    }
    [Fact]
    public void Review_RendersLongPublicationPathsThroughRealCli()
    {
        using var workspace = new TempWorkspace();
        created.CopyTo(workspace.File("deck.pptx"));
        string directory = workspace.File(Path.Combine(new string('a', 90), new string('b', 90), new string('c', 90)));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "review");
        Assert.True(output.Length > 260);
        CliResult reviewed = workspace.Run("review", "deck.pptx", "--out", output, "--max-items", "1", "--output-format", "json");
        Assert.True(reviewed.ExitCode == 0, reviewed.StdErr);
        JsonNode manifest = JsonNode.Parse(reviewed.StdOut)!;
        string relativeImage = Assert.Single(manifest["artifacts"]!.AsArray(),
            static artifact => artifact!["mediaType"]!.GetValue<string>() == "image/png")!["path"]!.GetValue<string>();
        string imagePath = Path.Combine(output, relativeImage);
        using FileStream stream = File.OpenRead(imagePath);
        using IImage image = Images.FromStream(stream);
        Assert.Equal(1600, image.Width);
        Assert.Equal(900, image.Height);
    }
    /// <summary>
    /// The deck the real-CLI tests start from, created once through the built CLI from a
    /// Markdown outline; each test works on its own copy.
    /// </summary>
    public sealed class CreatedDeck : IDisposable
    {
        private readonly TempWorkspace _workspace = new();
        private readonly Lazy<string> _deck;

        public CreatedDeck() => _deck = new(() =>
        {
            File.WriteAllText(_workspace.File("outline.md"), "# Quarterly review\n\nGrowth and retention");
            CliResult create = _workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--slide-size", "16x9", "--output-format", "json");
            Assert.True(create.ExitCode == 0, create.StdErr);
            return _workspace.File("deck.pptx");
        });

        public void CopyTo(string path) => File.Copy(_deck.Value, path);

        public void Dispose() => _workspace.Dispose();
    }
}
