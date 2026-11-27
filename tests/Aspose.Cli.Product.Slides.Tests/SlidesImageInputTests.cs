using System.IO.Compression;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Aspose.Slides adds any bytes as a picture, so the CLI checks every image an edit names before
/// the engine sees it, and takes no SVG image.
/// </summary>
public sealed class SlidesImageInputTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A file that is not an intact image, as a picture frame's image or a slide's background
    /// picture, is refused before anything is written.
    /// </summary>
    [Theory]
    [InlineData("text", "pictureFrame", "FILE_CORRUPT")]
    [InlineData("truncated", "background", "FILE_CORRUPT")]
    [InlineData("svgz", "pictureFrame", "FEATURE_UNSUPPORTED")]
    [InlineData("unparsed-svg", "pictureFrame", "FORMAT_UNSUPPORTED")]
    public void AnImageThatIsNotIntact_IsRefusedBeforeAnythingIsWritten(string content, string target, string code)
    {
        uint slide = Deck();
        string picture = content switch
        {
            "svgz" => "diagram.svgz",
            "unparsed-svg" => "cut.svg",
            _ => "picture.png",
        };
        File.WriteAllBytes(_workspace.File(picture), content switch
        {
            "text" => "not an image"u8.ToArray(),
            "truncated" => SlidesEngineFixture.Png(40)[..60],
            "svgz" => Gzip("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"9\" height=\"9\"/>"),
            _ => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\"><g></svg>"u8.ToArray(),
        });
        File.WriteAllText(_workspace.File("ops.json"), Ops(slide, target, picture).ToJsonString());

        CliResult edited = _workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", "out.pptx", "--output-format", "json");

        Assert.NotEqual(0, edited.ExitCode);
        Assert.Equal(code, JsonNode.Parse(edited.StdErr)!["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(_workspace.File("out.pptx")));
    }

    /// <summary>
    /// An SVG image is refused as a picture frame's image or a slide's background picture, one
    /// that names a picture outside the input's folder and a network address too: its bytes
    /// would reach the engine as they are, so the input does not take the format
    /// (<c>FORMAT_UNSUPPORTED</c>). Nothing is written, and no part names the picture.
    /// </summary>
    [Theory]
    [InlineData("pictureFrame")]
    [InlineData("background")]
    public void AnSvgImage_IsRefusedBeforeAnythingIsWritten(string target)
    {
        uint slide = Deck();
        string outside = Directory.CreateDirectory(_workspace.File("outside")).FullName;
        string sentinelPath = Path.Combine(outside, "sentinel.png");
        File.WriteAllBytes(sentinelPath, SlidesEngineFixture.Png(41));
        File.WriteAllText(_workspace.File("diagram.svg"),
            $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="40" height="40"><rect width="40" height="40" fill="#0000ff"/><image xlink:href="{System.Security.SecurityElement.Escape(sentinelPath)}" width="20" height="20"/><image xlink:href="http://sentinel.invalid/pixel.png" width="20" height="20"/></svg>""");
        File.WriteAllText(_workspace.File("ops.json"), Ops(slide, target, "diagram.svg").ToJsonString());

        CliResult edited = _workspace.Run("slides", "edit", "deck.pptx", "--ops", "ops.json", "--out", "out.pptx", "--output-format", "json");

        Assert.Equal(6, edited.ExitCode);
        JsonNode error = JsonNode.Parse(edited.StdErr)!["error"]!;
        Assert.Equal("FORMAT_UNSUPPORTED", error["code"]!.GetValue<string>());
        Assert.Contains("SVG", error["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(_workspace.File("out.pptx")));
    }

    /// <summary>Saves a one-slide deck.pptx; returns the slide's id.</summary>
    private uint Deck()
    {
        using var fixture = new SlidesEngineFixture();
        using var deck = new Presentation();
        deck.Save(_workspace.File("deck.pptx"), SaveFormat.Pptx);
        return deck.Slides[0].SlideId;
    }

    /// <summary>One operation that gives the slide <paramref name="image"/>: a picture frame showing it, or its background picture.</summary>
    private static JsonObject Ops(uint slide, string target, string image) => new()
    {
        ["ops"] = new JsonArray(target == "background"
            ? new JsonObject
            {
                ["set"] = $"slide/{slide}",
                ["props"] = new JsonObject { ["background.type"] = "OwnBackground", ["background.fillType"] = "Picture", ["background.picture"] = image },
            }
            : new JsonObject
            {
                ["add"] = "pictureFrame",
                ["in"] = $"slide/{slide}",
                ["args"] = new JsonObject { ["image"] = image, ["shapeType"] = "Rectangle", ["x"] = 10, ["y"] = 10, ["width"] = 40, ["height"] = 40 },
            }),
    };

    private static byte[] Gzip(string text)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(text));
        }

        return compressed.ToArray();
    }
}
