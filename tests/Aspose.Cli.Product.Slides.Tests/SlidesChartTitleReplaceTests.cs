using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// replace_text does not change a chart's title: it warns <c>CHART_TITLE_NOT_REPLACED</c> for
/// each chart whose title matches as the operation matches text, and says how to rebuild it.
/// Evaluation mode reads text longer than five characters cut short, so every text here is at
/// most five characters long.
/// </summary>
public sealed class SlidesChartTitleReplaceTests : IDisposable
{
    private const string ChartTitleNotReplaced = "CHART_TITLE_NOT_REPLACED";

    private readonly TempWorkspace _workspace = new();

    public SlidesChartTitleReplaceTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A replace over every scope changes the text box and leaves the chart title, and warns once,
    /// at the chart's shape address, with a hint that rebuilds the chart at its rect; the edit
    /// still exits 0 and its verification passes.
    /// </summary>
    [Fact]
    public void ReplaceText_LeavesAMatchingChartTitleAndWarnsHowToRebuildIt()
    {
        // The fixture applies the test license state before the deck is authored.
        using var fixture = new SlidesEngineFixture();
        long chartId = Deck(_workspace.File("deck.pptx"), title: "2025", text: "2025");

        CliResult edit = _workspace.Run("slides", "edit", "deck.pptx", "--out", "out.pptx", "--verify", "--output-format", "json",
            "--ops", """[{"op":"replace_text","id":"year","find":"2025","replace":"2026","scope":"all"}]""");

        Assert.True(edit.ExitCode == 0, edit.StdErr);
        JsonNode result = JsonNode.Parse(edit.StdOut)!;
        JsonNode verification = result["verification"]!;
        Assert.True(verification["ok"]!.GetValue<bool>());
        Assert.Empty(verification["issues"]!.AsArray());
        // Evaluation mode reads text cut short, so it checks no text replacement.
        string[] checkedOps = fixture.LicenseState == Sdk.Licensing.LicenseState.Licensed ? ["year"] : [];
        Assert.Equal(checkedOps, verification["checkedOps"]!.AsArray().Select(static id => id!.GetValue<string>()));
        JsonNode warning = Assert.Single(result["warnings"] as JsonArray ?? [], static warning => warning!["code"]!.GetValue<string>() == ChartTitleNotReplaced)!;
        string message = warning["message"]!.GetValue<string>();
        Assert.Contains("Operation year (replace_text)", message, StringComparison.Ordinal);
        Assert.Contains("chart 'Sales'", message, StringComparison.Ordinal);
        Assert.Contains("on slide 2", message, StringComparison.Ordinal);
        Assert.DoesNotContain("2025", message, StringComparison.Ordinal);
        Assert.Equal($"slide/2/shape/{chartId}", warning["location"]!.GetValue<string>());
        Assert.Equal("year", warning["details"]!["id"]!.GetValue<string>());
        Assert.Equal("replace_text", warning["details"]!["op"]!.GetValue<string>());
        Assert.Null(warning["affectsCompleteness"]);
        string hint = warning["hint"]!.GetValue<string>();
        Assert.Contains($$"""{"op":"delete_shape","slide":2,"shapeId":{{chartId}}}""", hint, StringComparison.Ordinal);
        Assert.Contains("insert_chart", hint, StringComparison.Ordinal);
        Assert.Contains("new title", hint, StringComparison.Ordinal);
        Assert.Contains("""
            "rect":{"x":40,"y":120,"width":300,"height":200}
            """, hint, StringComparison.Ordinal);

        using var output = new Presentation(_workspace.File("out.pptx"));
        ISlide slide = output.Slides[1];
        Assert.Equal("2026", slide.Shapes.OfType<IAutoShape>().Single(static shape => shape.Name == "Box").TextFrame.Text);
        IChart chart = slide.Shapes.OfType<IChart>().Single();
        Assert.Equal("2025", chart.ChartTitle.TextFrameForOverriding.Text);
        // The chart's data is not text replace_text reaches either.
        Assert.Equal("2025", chart.ChartData.Categories[0].Value?.ToString());
    }

    /// <summary>
    /// The warning follows the operation's own matching and scope: a notes-only replace, a title
    /// without the find text, and a title that letter case, whole words or the pattern keep from
    /// matching give none; a title that matches gives one.
    /// </summary>
    [Theory]
    [InlineData("2025", "2025", false, false, false, "shapes", true)]
    [InlineData("2025", "2025", false, false, false, "notes", false)]
    [InlineData("2024", "2025", false, false, false, "all", false)]
    [InlineData("Rev", "rev", false, true, false, "all", false)]
    [InlineData("Rev", "rev", false, false, false, "all", true)]
    [InlineData("20250", "2025", false, false, true, "all", false)]
    [InlineData("20250", "2025", false, false, false, "all", true)]
    [InlineData("2025", @"20\d5", true, false, false, "all", true)]
    [InlineData("2025", @"20\d6", true, false, false, "all", false)]
    public void ChartTitleWarning_MatchesAsTheOperationMatchesText(
        string title, string find, bool regex, bool caseSensitive, bool wholeWord, string scope, bool warns)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        Deck(input, title, text: find == "rev" ? "rev" : "2025");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch
            {
                Ops = [new SlidesReplaceTextOp
                {
                    Find = find,
                    Replace = "X",
                    Regex = regex,
                    CaseSensitive = caseSensitive,
                    WholeWord = wholeWord,
                    Scope = scope,
                }],
            },
            Output = TestOutput.At(fixture.File("replaced.pptx")),
        });

        Assert.Equal("ok", Assert.Single(result.Applied).Status);
        Assert.Equal(warns ? 1 : 0, (result.Warnings ?? []).Count(static warning => warning.Code.Name == ChartTitleNotReplaced));
        using var output = new Presentation(fixture.File("replaced.pptx"));
        Assert.Equal(title, output.Slides[1].Shapes.OfType<IChart>().Single().ChartTitle.TextFrameForOverriding.Text);
    }

    /// <summary>
    /// Only a chart placed directly on a slide is checked: a matching title of a chart inside a
    /// group gives no warning, and the title stays as it was.
    /// </summary>
    [Fact]
    public void ChartInsideAGroup_IsNotChecked()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("grouped.pptx");
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60).TextFrame.Text = "2025";
            IChart chart = slide.Shapes.AddGroupShape().Shapes.AddChart(ChartType.ClusteredColumn, 40, 120, 300, 200);
            chart.HasTitle = true;
            chart.ChartTitle.AddTextFrameForOverriding("2025");
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SlidesReplaceTextOp { Find = "2025", Replace = "2026" }] },
            Output = TestOutput.At(fixture.File("grouped.out.pptx")),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
        Assert.DoesNotContain(result.Warnings ?? [], static warning => warning.Code.Name == ChartTitleNotReplaced);
        using var output = new Presentation(fixture.File("grouped.out.pptx"));
        IChart grouped = output.Slides[0].Shapes.OfType<IGroupShape>().Single().Shapes.OfType<IChart>().Single();
        Assert.Equal("2025", grouped.ChartTitle.TextFrameForOverriding.Text);
    }

    /// <summary>
    /// A two-slide deck: the second holds a text box named Box with <paramref name="text"/> and
    /// speaker notes of the same text, and a column chart named Sales titled
    /// <paramref name="title"/> whose first category is <paramref name="text"/>; returns the
    /// chart's shape id.
    /// </summary>
    private static long Deck(string path, string title, string text)
    {
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60);
        box.Name = "Box";
        box.TextFrame.Text = text;
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = text;
        IChart chart = slide.Shapes.AddChart(ChartType.ClusteredColumn, 40, 120, 300, 200);
        chart.Name = "Sales";
        chart.HasTitle = true;
        chart.ChartTitle.AddTextFrameForOverriding(title);
        chart.ChartData.Categories[0].AsCell.Value = text;
        presentation.Save(path, SaveFormat.Pptx);
        return chart.OfficeInteropShapeId;
    }
}
