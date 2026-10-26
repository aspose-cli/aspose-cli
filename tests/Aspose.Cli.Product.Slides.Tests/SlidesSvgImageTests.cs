using System.IO.Compression;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using SkiaSharp;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// An SVG picture is imported as an SVG, so it renders: Markdown, insert_image and set_background
/// draw a red SVG in red, and the package holds no picture of an unknown type.
/// </summary>
public sealed class SlidesSvgImageTests
{
    private const string RedSvg = """<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100"><rect width="200" height="100" fill="#ff0000"/></svg>""";

    // Enough red pixels that the picture is drawn, not a stray edge.
    private const int Drawn = 1000;

    [Fact]
    public void CreateFromMarkdown_RendersAnSvgPicture()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("red.svg"), RedSvg);
        File.WriteAllText(workspace.File("outline.md"), "## Logo\n\n![logo](red.svg)\n");

        CliResult created = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        Assert.DoesNotContain(Warnings(created), static code => code == "REMOTE_RESOURCES_BLOCKED");
        AssertNoUnknownMedia(workspace.File("deck.pptx"));
        Assert.True(RedPixels(workspace, "deck.pptx") > Drawn, "the SVG picture is not drawn");
    }

    [Theory]
    [InlineData("""{"ops":[{"op":"insert_image","slide":1,"path":"red.svg"}]}""")]
    [InlineData("""{"ops":[{"op":"set_background","path":"red.svg"}]}""")]
    public void Edit_RendersAnSvgPicture(string ops)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("red.svg"), RedSvg);
        File.WriteAllText(workspace.File("outline.md"), "## Logo\n");
        File.WriteAllText(workspace.File("ops.json"), ops);
        CliResult created = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md");
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult edited = workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", "out.pptx", "--output-format", "json");

        Assert.True(edited.ExitCode == 0, edited.StdErr);
        AssertNoUnknownMedia(workspace.File("out.pptx"));
        Assert.True(RedPixels(workspace, "out.pptx") > Drawn, "the SVG picture is not drawn");
    }

    /// <summary>
    /// An SVG that names a network address still loses it: the picture is drawn without it, no
    /// part of the presentation names the address, and the result reports the removed reference.
    /// </summary>
    [Fact]
    public void CreateFromMarkdown_AnSvgNamingANetworkAddress_IsDrawnWithoutIt()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("red.svg"),
            """<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="200" height="100"><rect width="200" height="100" fill="#ff0000"/><image xlink:href="http://sentinel.invalid/pixel.png" width="20" height="20"/></svg>""");
        File.WriteAllText(workspace.File("outline.md"), "## Logo\n\n![logo](red.svg)\n");

        CliResult created = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md", "--output-format", "json");

        Assert.True(created.ExitCode == 0, created.StdErr);
        Assert.Contains(Warnings(created), static code => code == "REMOTE_RESOURCES_BLOCKED");
        using (ZipArchive archive = ZipFile.OpenRead(workspace.File("deck.pptx")))
        {
            foreach (ZipArchiveEntry entry in archive.Entries.ToArray())
            {
                using var reader = new StreamReader(entry.Open(), System.Text.Encoding.Latin1);
                Assert.DoesNotContain("sentinel", reader.ReadToEnd(), StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.True(RedPixels(workspace, "deck.pptx") > Drawn, "the SVG picture is not drawn");
    }

    /// <summary>An SVG that does not parse is an invalid insert_image, not a blank picture frame.</summary>
    [Fact]
    public void InsertImage_RefusesAnSvgThatDoesNotParse()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.File("cut.svg"), RedSvg[..60]);
        File.WriteAllText(workspace.File("outline.md"), "## Logo\n");
        File.WriteAllText(workspace.File("ops.json"), """{"ops":[{"op":"insert_image","slide":1,"path":"cut.svg"}]}""");
        CliResult created = workspace.Run("slides", "create", "deck.pptx", "--from-markdown", "outline.md");
        Assert.True(created.ExitCode == 0, created.StdErr);

        CliResult edited = workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", "out.pptx", "--output-format", "json");

        Assert.NotEqual(0, edited.ExitCode);
        Assert.Equal("OPS_INVALID", JsonNode.Parse(edited.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(workspace.File("out.pptx")));
    }

    private static IEnumerable<string> Warnings(CliResult result) =>
        (JsonNode.Parse(result.StdOut)!["warnings"] as JsonArray ?? [])
            .Select(static warning => warning!["code"]!.GetValue<string>());

    private static void AssertNoUnknownMedia(string package)
    {
        using ZipArchive archive = ZipFile.OpenRead(package);
        string[] media = [.. archive.Entries
            .Select(static entry => entry.FullName)
            .Where(static name => name.StartsWith("ppt/media/", StringComparison.Ordinal))];
        Assert.NotEmpty(media);
        Assert.DoesNotContain(media, static name => name.EndsWith(".image", StringComparison.Ordinal));
    }

    // The red pixels of slide 1 as rendered.
    private static int RedPixels(TempWorkspace workspace, string deck)
    {
        CliResult rendered = workspace.Run("slides", "render", deck, "--slides", "1", "--out", "slide.png", "--output-format", "json");
        Assert.True(rendered.ExitCode == 0, rendered.StdErr);
        string png = JsonNode.Parse(rendered.StdOut)!["outputs"]!.AsArray()[0]!["path"]!.GetValue<string>();
        using SKBitmap bitmap = SKBitmap.Decode(png);
        return bitmap.Pixels.Count(static pixel => pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60);
    }
}
