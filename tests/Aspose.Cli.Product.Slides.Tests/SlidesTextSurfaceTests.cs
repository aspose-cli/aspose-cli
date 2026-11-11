using System.Drawing;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Aspose.Slides.SmartArt;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// Every part of a presentation that holds text, each with a marker of its own: <c>search
/// --scope all</c> finds each marker; <c>replace_text</c> replaces the markers in the parts it
/// writes and names each other part in a <c>TEXT_NOT_REPLACED</c>; a search after it finds the
/// markers exactly in the parts named; and <c>--verify</c> passes. Each marker is five characters,
/// which evaluation mode reads whole, so the check runs without a license too.
/// </summary>
public sealed class SlidesTextSurfaceTests : IDisposable
{
    private const string Marker = "Zq";

    /// <summary>The parts replace_text writes, by the rest of their marker.</summary>
    private static readonly string[] Written = ["Tit", "Bod", "Box", "Tab", "Grp", "Sma", "Not"];

    /// <summary>The parts replace_text does not change, by the rest of their marker, with the part it names.</summary>
    private static readonly Dictionary<string, string> Named = new(StringComparer.Ordinal)
    {
        ["Com"] = "comment",
        ["Cht"] = "chart-title",
        ["Axi"] = "axis-title",
        ["Cat"] = "chart-data",
        ["Ser"] = "chart-data",
        ["Lab"] = "data-label",
        ["Alt"] = "alt-text",
        ["Lay"] = "layout",
        ["Mas"] = "layout",
    };

    private readonly TempWorkspace _workspace = new();

    public SlidesTextSurfaceTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void EveryPart_IsFound_AndEitherReplacedOrNamed()
    {
        using var fixture = new SlidesEngineFixture();
        _ = fixture.LicenseState;
        Deck(_workspace.File("parts.pptx"), hideMaster: false);

        JsonNode[] before = Hits("parts.pptx", "all");
        Assert.All(Written.Concat(Named.Keys), part => Assert.Contains(before, hit => Holds(hit, part)));

        CliResult edit = _workspace.Run("slides", "edit", "parts.pptx", "--ops", $$"""{"ops":[{"op":"replace_text","find":"{{Marker}}","replace":"Ok"}]}""",
            "--out", "replaced.pptx", "--verify", "--output-format", "json");
        Assert.True(edit.ExitCode == 0, edit.StdOut + edit.StdErr);
        JsonNode result = JsonNode.Parse(edit.StdOut)!;
        Assert.True(result["verification"]!["ok"]!.GetValue<bool>(), result["verification"]!.ToJsonString());
        JsonNode[] named = [.. (result["warnings"] as JsonArray ?? []).OfType<JsonNode>()
            .Where(static warning => warning["code"]!.GetValue<string>() == "TEXT_NOT_REPLACED")];
        Assert.Equal(
            Named.Values.Distinct().Order(StringComparer.Ordinal),
            named.Select(static warning => warning["details"]!["part"]!.GetValue<string>()).Distinct().Order(StringComparer.Ordinal));

        JsonNode[] after = Hits("replaced.pptx", "all");
        Assert.All(Written, part => Assert.DoesNotContain(after, hit => Holds(hit, part)));
        Assert.All(Named.Keys, part => Assert.Contains(after, hit => Holds(hit, part)));
        string[] locations = [.. named.Select(static warning => warning["location"]!.GetValue<string>())];
        Assert.All(after, hit => Assert.Contains(Location(hit), locations));
        Assert.All(locations, location => Assert.Contains(after, hit => Location(hit) == location));
    }

    /// <summary>A layout that hides the master's graphics shows no text of the master, so a search finds none.</summary>
    [Fact]
    public void MasterText_ALayoutHides_IsNotFound()
    {
        using var fixture = new SlidesEngineFixture();
        _ = fixture.LicenseState;
        Deck(_workspace.File("hidden.pptx"), hideMaster: true);

        Assert.Empty(Hits("hidden.pptx", "masters"));
        Assert.Single(Hits("hidden.pptx", "layouts"));
    }

    /// <summary>
    /// A licensed replace_text in a deck an evaluation save marked writes its watermark too, and
    /// --verify works out what it leaves the same way, up to its most replacements.
    /// </summary>
    [Fact]
    public void Verify_OfAReplacementWithAMostCountInAWatermarkedDeck_Passes()
    {
        TestLicense.Require("The watermark is written only by a licensed replacement.");
        File.WriteAllText(_workspace.File("two.md"), "## One\n\n- a\n\n## Two\n\n- Evaluation\n");
        Assert.Equal(0, _workspace.Run("slides", "create", "marked.pptx", "--from-markdown", "two.md", "--license-mode", "evaluation", "--output-format", "json").ExitCode);

        CliResult edit = _workspace.Run("slides", "edit", "marked.pptx", "--ops", """{"ops":[{"op":"replace_text","find":"Evaluation","replace":"E","maxReplacementCount":1}]}""",
            "--out", "replaced.pptx", "--verify", "--output-format", "json");

        Assert.True(edit.ExitCode == 0, edit.StdOut + edit.StdErr);
        Assert.True(JsonNode.Parse(edit.StdOut)!["verification"]!["ok"]!.GetValue<bool>(), edit.StdOut);
    }

    private JsonNode[] Hits(string file, string scope)
    {
        CliResult search = _workspace.Run("slides", "search", file, "--pattern", Marker, "--scope", scope, "--max-hits", "100", "--output-format", "json");
        Assert.True(search.ExitCode == 0, search.StdOut + search.StdErr);
        return [.. JsonNode.Parse(search.StdOut)!["hits"]!.AsArray().OfType<JsonNode>()];
    }

    private static bool Holds(JsonNode hit, string part) =>
        hit["context"]!.GetValue<string>().Contains(Marker + part, StringComparison.Ordinal);

    // The address a warning names for the part a hit is in: its shape, or else its slide.
    private static string Location(JsonNode hit) => hit["shapeId"] is { } shape
        ? $"slide/{hit["slide"]}/shape/{shape}"
        : $"slide/{hit["slide"]}";

    /// <summary>One slide with a marker in each part, on a layout and master that show one too, or a layout that hides the master's.</summary>
    private static void Deck(string path, bool hideMaster)
    {
        using var deck = new Presentation();
        ILayoutSlide layout = deck.LayoutSlides.GetByType(SlideLayoutType.TitleAndObject);
        layout.Shapes.AddAutoShape(ShapeType.Rectangle, 560, 10, 150, 30).TextFrame.Text = Marker + "Lay";
        layout.ShowMasterShapes = !hideMaster;
        deck.Masters[0].Shapes.AddAutoShape(ShapeType.Rectangle, 560, 500, 150, 30).TextFrame.Text = Marker + "Mas";
        ISlide slide = deck.Slides.AddEmptySlide(layout);
        deck.Slides.RemoveAt(0);
        IAutoShape[] placeholders = [.. slide.Shapes.OfType<IAutoShape>()];
        placeholders.First(static shape => shape.Placeholder!.Type is PlaceholderType.Title).TextFrame.Text = Marker + "Tit";
        placeholders.First(static shape => shape.Placeholder!.Type is PlaceholderType.Body or PlaceholderType.Object).TextFrame.Text = Marker + "Bod";
        slide.Shapes.AddAutoShape(ShapeType.Rectangle, 20, 300, 150, 40).TextFrame.Text = Marker + "Box";
        slide.Shapes.AddTable(200, 300, [150], [40])[0, 0].TextFrame.Text = Marker + "Tab";
        slide.Shapes.AddGroupShape().Shapes.AddAutoShape(ShapeType.Rectangle, 380, 300, 150, 40).TextFrame.Text = Marker + "Grp";
        slide.Shapes.AddSmartArt(20, 360, 200, 100, SmartArtLayoutType.BasicBlockList).AllNodes[0].TextFrame.Text = Marker + "Sma";
        slide.Shapes.AddAutoShape(ShapeType.Rectangle, 240, 360, 60, 40).AlternativeText = Marker + "Alt";
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = Marker + "Not";
        deck.CommentAuthors.AddAuthor("Reviewer", "R").Comments.AddComment(Marker + "Com", slide, new PointF(10, 10), DateTime.UtcNow);
        IChart chart = slide.Shapes.AddChart(ChartType.ClusteredColumn, 320, 360, 360, 170);
        chart.HasTitle = true;
        chart.ChartTitle.AddTextFrameForOverriding(Marker + "Cht");
        chart.Axes.HorizontalAxis.HasTitle = true;
        chart.Axes.HorizontalAxis.Title.AddTextFrameForOverriding(Marker + "Axi");
        chart.ChartData.Categories[0].AsCell.Value = Marker + "Cat";
        chart.ChartData.Series[0].Name.AsCells[0].Value = Marker + "Ser";
        chart.ChartData.Series[0].DataPoints[0].Label.AddTextFrameForOverriding(Marker + "Lab");
        deck.Save(path, SaveFormat.Pptx);
    }
}
