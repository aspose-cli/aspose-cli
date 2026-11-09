using System.IO.Compression;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Slides;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// What a verified edit says about the operations it did not read back, and which warnings fail
/// it. The commands run with the test license when the run has one, and in evaluation mode
/// otherwise.
/// </summary>
public sealed class SlidesVerifyCoverageTests : IDisposable
{
    private static readonly string ImplicitTitleDeck =
        Path.Combine(RepositoryPaths.Root, "tests", "Aspose.Cli.Product.Slides.Tests", "Assets", "implicit-chart-title.pptx");

    private readonly SlidesEngineFixture _fixture = new();
    private readonly TempWorkspace _workspace = new();

    public void Dispose()
    {
        _workspace.Dispose();
        _fixture.Dispose();
    }

    /// <summary>
    /// SL-1: inserted shapes and charts have no read-back, so they are named with the reason and
    /// not left to look checked. An edit it reads back whole has no such field.
    /// </summary>
    [Fact]
    public void Verify_NamesTheOperationsItHasNoReadBackFor_AndOmitsTheFieldWhenItReadsEveryOne()
    {
        string input = _fixture.CreatePresentation(slides: 2);

        JsonNode partial = Verification(Edit(input, """
            {"ops":[
              {"id":"page","op":"insert_slide"},
              {"id":"box","op":"insert_shape","slide":1,"geometry":"rectangle","rect":{"x":10,"y":10,"width":100,"height":50},"text":"x"},
              {"id":"bars","op":"insert_chart","slide":1,"kind":"column","rect":{"x":10,"y":100,"width":300,"height":200},"categories":["A"],"series":[{"name":"S","values":[1]}]}
            ]}
            """));
        JsonNode whole = Verification(Edit(input, """{"ops":[{"id":"page","op":"insert_slide"}]}"""));

        Assert.True(partial["ok"]!.GetValue<bool>());
        Assert.Equal(["page"], partial["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonArray items = partial["uncheckedOps"]!.AsArray();
        Assert.Equal(["box", "bars"], items.Select(static item => item!["id"]!.GetValue<string>()));
        Assert.All(items, static item => Assert.Contains("no read-back", item!["reason"]!.GetValue<string>(), StringComparison.Ordinal));
        Assert.Null(whole["uncheckedOps"]);
    }

    [Fact]
    public void Verify_InTheTable_ListsWhatItCheckedAndAnOperationItDoesNotReadBack()
    {
        string input = _fixture.CreatePresentation(slides: 2);
        string[] args =
        [
            "slides", "edit", input, "--out", "table.pptx", "--verify", "--output-format", "table",
            "--ops", """{"ops":[{"id":"page","op":"insert_slide"},{"id":"box","op":"insert_shape","slide":1,"geometry":"rectangle","rect":{"x":10,"y":10,"width":100,"height":50}}]}""",
        ];

        CliResult table = _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);

        Assert.True(table.ExitCode == 0, table.StdErr + table.StdOut);
        Assert.Contains("verification: ok; checked ops: page", table.StdOut, StringComparison.Ordinal);
        Assert.Contains("not checked box: ", table.StdOut, StringComparison.Ordinal);
    }

    /// <summary>An operation a later one of the batch changed again is named for that reason.</summary>
    [Fact]
    public void Verify_NamesAnOperationALaterOneChangedAgain()
    {
        string input = _fixture.CreatePresentation(slides: 2);

        JsonNode verification = Verification(Edit(input, """
            {"ops":[{"id":"hide","op":"set_slide_hidden","slides":"1","hidden":true},{"id":"show","op":"set_slide_hidden","slides":"1","hidden":false}]}
            """));

        Assert.Equal(["show"], verification["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonNode item = Assert.Single(verification["uncheckedOps"]!.AsArray())!;
        Assert.Equal("hide", item["id"]!.GetValue<string>());
        Assert.Contains("later operation", item["reason"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every save draws a chart's implicit title over its plot, whatever the batch does, so the
    /// warning remains, still marked for a review, and the verification passes.
    /// </summary>
    [Fact]
    public void Verify_OfADeckWithAnImplicitChartTitle_PassesAndWarns()
    {
        string input = _fixture.File("implicit-title.pptx");
        File.Copy(ImplicitTitleDeck, input);

        CliResult edited = Edit(input, """{"ops":[{"id":"page","op":"insert_slide"}]}""");

        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>());
        Assert.Empty(result["verification"]!["issues"]!.AsArray());
        JsonNode warning = Assert.Single(result["warnings"]!.AsArray(), static item => item!["code"]!.GetValue<string>() == "CHART_TITLE_OVERLAID")!;
        Assert.True(warning["affectsCompleteness"]!.GetValue<bool>());
        Assert.Equal("slide/2", warning["location"]!.GetValue<string>());
    }

    /// <summary>
    /// A linked picture or video the CLI did not load stays a link in the output, so the warning
    /// about it remains and the verification passes.
    /// </summary>
    [Fact]
    public void Verify_OfADeckThatLinksAPictureItDidNotLoad_PassesAndWarns()
    {
        string input = _fixture.File("linked.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            IPPImage image = presentation.Images.AddImage(ResourceHttpServer.Image);
            slide.Shapes.AddPictureFrame(ShapeType.Rectangle, 20, 20, 120, 120, image)
                .PictureFormat.Picture.LinkPathLong = "http://127.0.0.1:9/linked.png";
            slide.Shapes.AddVideoFrame(200, 20, 160, 120, "http://127.0.0.1:9/clip.mp4");
            presentation.Save(input, SaveFormat.Pptx);
        }

        CliResult edited = Edit(input, """{"ops":[{"id":"page","op":"insert_slide"}]}""");

        JsonNode result = JsonNode.Parse(edited.StdOut)!;
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>());
        Assert.Empty(result["verification"]!["issues"]!.AsArray());
        Assert.Contains(result["warnings"]!.AsArray(), static warning => warning!["code"]!.GetValue<string>() == "REMOTE_RESOURCES_BLOCKED");
        using ZipArchive package = ZipFile.OpenRead(_workspace.File("out.pptx"));
        using var relationships = new StreamReader(package.GetEntry("ppt/slides/_rels/slide1.xml.rels")!.Open());
        string links = relationships.ReadToEnd();
        Assert.Contains("http://127.0.0.1:9/linked.png", links, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:9/clip.mp4", links, StringComparison.Ordinal);
    }

    private CliResult Edit(string input, string ops)
    {
        string[] args = ["slides", "edit", input, "--ops", ops, "--out", "out.pptx", "--overwrite", "--verify", "--output-format", "json"];
        CliResult edited = _workspace.Run(TestLicense.Path is { } license ? [.. args, "--license", license] : args);
        Assert.True(edited.ExitCode == 0, edited.StdErr + edited.StdOut);
        return edited;
    }

    private static JsonNode Verification(CliResult result) => JsonNode.Parse(result.StdOut)!["verification"]!;
}
