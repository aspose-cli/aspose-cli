using System.Text.Json.Nodes;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// replace_text does not change a chart's title: it warns <c>TEXT_NOT_REPLACED</c>, with
/// <c>details.part</c> <c>chart-title</c>, for each chart whose title matches as the operation
/// matches text, and says how to rebuild it. A chart whose data matches as well warns once more,
/// with <c>chart-data</c> (<see cref="SlidesChartDataReplaceTests"/>).
/// Evaluation mode reads text longer than five characters cut short, so every text here is at
/// most five characters long.
/// </summary>
public sealed class SlidesChartTitleReplaceTests : IDisposable
{
    private const string TextNotReplaced = "TEXT_NOT_REPLACED";

    private const string ChartTitle = "chart-title";

    private readonly TempWorkspace _workspace = new();

    public SlidesChartTitleReplaceTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A replace over every scope changes the text box and leaves the chart title, and warns once
    /// for the title, at the chart's shape address, with a hint that rebuilds the chart at its
    /// rect, and once more for the chart's first category, which matches too; the edit still exits
    /// 0 and its verification passes.
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
        JsonNode[] unreplaced = [.. (result["warnings"] as JsonArray ?? [])
            .Where(static warning => warning!["code"]!.GetValue<string>() == TextNotReplaced)
            .Select(static warning => warning!)];
        Assert.Equal([ChartTitle, "chart-data"], unreplaced.Select(static warning => warning["details"]?["part"]?.GetValue<string>()));
        JsonNode warning = unreplaced[0];
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
    /// The warnings name the operation by its id and op and the part of the chart each left,
    /// first the title and then the data, both at the chart's shape address.
    /// </summary>
    [Fact]
    public void ChartWarnings_NameTheTitleAndThenTheDataAsThePartsTheyLeft()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        long chartId = Deck(input, title: "2025", text: "2025");

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SlidesReplaceTextOp { Id = "year", Find = "2025", Replace = "2026", Scope = "all" }] },
            Output = TestOutput.At(fixture.File("replaced.pptx")),
        });

        Warning[] warnings = [.. (result.Warnings ?? []).Where(static warning => warning.Code.Name == TextNotReplaced)];
        Assert.Equal(
            ["""{"id":"year","op":"replace_text","part":"chart-title"}""", """{"id":"year","op":"replace_text","part":"chart-data"}"""],
            warnings.Select(static warning => warning.Details?.ToJsonString()));
        Assert.All(warnings, warning => Assert.Equal($"slide/2/shape/{chartId}", warning.Location));
    }

    /// <summary>
    /// The title warning follows the operation's own matching and scope: a notes-only replace, a
    /// title without the find text, and a title that letter case, whole words or the pattern keep
    /// from matching give none; a title that matches gives one.
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
        Assert.Equal(warns ? 1 : 0, (result.Warnings ?? []).Count(static warning =>
            warning.Code.Name == TextNotReplaced && warning.Details?["part"]?.GetValue<string>() == ChartTitle));
        using var output = new Presentation(fixture.File("replaced.pptx"));
        Assert.Equal(title, output.Slides[1].Shapes.OfType<IChart>().Single().ChartTitle.TextFrameForOverriding.Text);
    }

    /// <summary>
    /// A chart inside a group, or inside a group in a group, is checked too: its matching title
    /// stays as it was and warns at the chart's own address, naming the slide's group that holds
    /// it, with a hint that no operation edits it rather than one to rebuild it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChartInsideAGroup_WarnsThatNoOperationReachesItsTitle(bool nested)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("grouped.pptx");
        long chartId;
        long groupId;
        using (var presentation = new Presentation())
        {
            ISlide slide = presentation.Slides[0];
            slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60).TextFrame.Text = "2025";
            IGroupShape group = slide.Shapes.AddGroupShape();
            group.Name = "Pair";
            IShapeCollection holder = nested ? group.Shapes.AddGroupShape().Shapes : group.Shapes;
            IChart chart = holder.AddChart(ChartType.ClusteredColumn, 40, 120, 300, 200);
            chart.Name = "Sales";
            chart.HasTitle = true;
            chart.ChartTitle.AddTextFrameForOverriding("2025");
            chartId = chart.OfficeInteropShapeId;
            groupId = group.OfficeInteropShapeId;
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesEditResult result = SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SlidesReplaceTextOp { Id = "year", Find = "2025", Replace = "2026" }] },
            Output = TestOutput.At(fixture.File("grouped.out.pptx")),
        });

        Assert.Equal(1, Assert.Single(result.Applied).ItemsAffected);
        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == TextNotReplaced);
        Assert.Equal(ChartTitle, warning.Details?["part"]?.GetValue<string>());
        Assert.Equal($"slide/1/shape/{chartId}", warning.Location);
        Assert.Equal(
            $"Operation year (replace_text) found a match in the title of chart 'Sales' (shapeId {chartId}) in group 'Pair' (shapeId {groupId}) on slide 1 and left the title as it was: replace_text does not change chart titles or chart data.",
            warning.Message);
        Assert.StartsWith("No operation addresses a shape inside a group", warning.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("delete_shape", warning.Hint, StringComparison.Ordinal);
        using var output = new Presentation(fixture.File("grouped.out.pptx"));
        IGroupShape outer = output.Slides[0].Shapes.OfType<IGroupShape>().Single();
        IChart grouped = (nested ? outer.Shapes.OfType<IGroupShape>().Single().Shapes : outer.Shapes).OfType<IChart>().Single();
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
