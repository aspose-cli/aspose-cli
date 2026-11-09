using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// replace_text does not change a chart's axis titles: it warns <c>TEXT_NOT_REPLACED</c>, with
/// <c>details.part</c> <c>axis-title</c>, once for each chart with an axis title that matches as
/// the operation matches text, and says that no operation changes one. The chart's title and data
/// are <see cref="SlidesChartTitleReplaceTests"/>' and <see cref="SlidesChartDataReplaceTests"/>'.
/// Evaluation mode reads text longer than five characters cut short, so every text here is at
/// most five characters long.
/// </summary>
public sealed class SlidesChartAxisTitleReplaceTests : IDisposable
{
    private const string TextNotReplaced = "TEXT_NOT_REPLACED";

    private const string AxisTitle = "axis-title";

    private const string NoOperationHint =
        "No operation changes a chart's axis titles, so the chart keeps that text: report it as unchanged, or change it in a presentation editor.";

    private readonly TempWorkspace _workspace = new();

    public SlidesChartAxisTitleReplaceTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A find text an axis title holds leaves the title as it was and warns at the chart's shape
    /// address, with a hint that gives no address or operation to run, while the text box with the
    /// same text changes.
    /// </summary>
    [Fact]
    public void ReplaceText_LeavesAMatchingAxisTitleAndSaysNoOperationChangesIt()
    {
        using var fixture = new SlidesEngineFixture();
        long chartId = Deck(_workspace.File("deck.pptx"), horizontal: "2025", vertical: "Sales");

        CliResult edit = _workspace.Run("slides", "edit", "deck.pptx", "--out", "out.pptx", "--output-format", "json",
            "--ops", """[{"op":"replace_text","id":"year","find":"2025","replace":"2026","scope":"all"}]""");

        Assert.True(edit.ExitCode == 0, edit.StdErr);
        JsonNode warning = Assert.Single(JsonNode.Parse(edit.StdOut)!["warnings"] as JsonArray ?? [],
            static warning => warning!["code"]!.GetValue<string>() == TextNotReplaced)!;
        Assert.Equal("""{"id":"year","op":"replace_text","part":"axis-title"}""", warning["details"]!.ToJsonString());
        Assert.Equal($"slide/2/shape/{chartId}", warning["location"]!.GetValue<string>());
        Assert.Null(warning["affectsCompleteness"]);
        Assert.Equal(
            $"Operation year (replace_text) found a match in an axis title of chart 'Sales' (shapeId {chartId}) on slide 2 and left it as it was: replace_text does not change a chart's axis titles.",
            warning["message"]!.GetValue<string>());
        Assert.Equal(NoOperationHint, warning["hint"]!.GetValue<string>());

        using var output = new Presentation(_workspace.File("out.pptx"));
        ISlide slide = output.Slides[1];
        Assert.Equal("2026", slide.Shapes.OfType<IAutoShape>().Single(static shape => shape.Name == "Box").TextFrame.Text);
        Assert.Equal("2025", slide.Shapes.OfType<IChart>().Single().Axes.HorizontalAxis.Title.TextFrameForOverriding.Text);
    }

    /// <summary>The warning comes once per chart, for the horizontal or the vertical axis or both.</summary>
    [Theory]
    [InlineData("Year", "Amt", "Year")]
    [InlineData("Year", "Amt", "Amt")]
    [InlineData("Same", "Same", "Same")]
    public void AnAxisTitleOfEitherAxis_WarnsOncePerChart(string horizontal, string vertical, string find)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        Deck(input, horizontal, vertical);

        Assert.Equal([AxisTitle], Parts(Replace(fixture, input, new SlidesReplaceTextOp { Find = find, Replace = "X" })));
    }

    /// <summary>A title of the depth axis of a 3-D chart counts as one of its other axes does.</summary>
    [Fact]
    public void ATitleOfTheDepthAxis_Warns()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("depth.pptx");
        using (var presentation = new Presentation())
        {
            IChart chart = presentation.Slides[0].Shapes.AddChart(ChartType.Column3D, 40, 120, 300, 200, initWithSample: true);
            chart.Axes.SeriesAxis.HasTitle = true;
            chart.Axes.SeriesAxis.Title.AddTextFrameForOverriding("Depth");
            presentation.Save(input, SaveFormat.Pptx);
        }

        Assert.Equal([AxisTitle], Parts(Replace(fixture, input, new SlidesReplaceTextOp { Find = "Depth", Replace = "X" })));
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
    public void AxisTitleWarning_MatchesAsTheOperationMatchesText(
        string title, string find, bool regex, bool caseSensitive, bool wholeWord, string scope, bool warns)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        Deck(input, title, vertical: null);

        SlidesEditResult result = Replace(fixture, input, new SlidesReplaceTextOp
        {
            Find = find,
            Replace = "X",
            Regex = regex,
            CaseSensitive = caseSensitive,
            WholeWord = wholeWord,
            Scope = scope,
        });

        Assert.Equal(warns ? [AxisTitle] : [], Parts(result));
        using var output = new Presentation(fixture.File("replaced.pptx"));
        Assert.Equal(title, output.Slides[1].Shapes.OfType<IChart>().Single().Axes.HorizontalAxis.Title.TextFrameForOverriding.Text);
    }

    /// <summary>
    /// A chart inside a group, or inside a group in a group, is checked too: the warning is at the
    /// chart's own address and names the slide's group that holds it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChartInsideAGroup_WarnsWithItsGroup(bool nested)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("grouped.pptx");
        long chartId;
        long groupId;
        using (var presentation = new Presentation())
        {
            IGroupShape group = presentation.Slides[0].Shapes.AddGroupShape();
            group.Name = "Pair";
            IShapeCollection holder = nested ? group.Shapes.AddGroupShape().Shapes : group.Shapes;
            IChart chart = holder.AddChart(ChartType.ClusteredColumn, 40, 120, 300, 200);
            chart.Name = "Sales";
            chart.Axes.VerticalAxis.HasTitle = true;
            chart.Axes.VerticalAxis.Title.AddTextFrameForOverriding("2025");
            chartId = chart.OfficeInteropShapeId;
            groupId = group.OfficeInteropShapeId;
            presentation.Save(input, SaveFormat.Pptx);
        }

        SlidesEditResult result = Replace(fixture, input, new SlidesReplaceTextOp { Id = "year", Find = "2025", Replace = "2026" });

        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == TextNotReplaced);
        Assert.Equal(AxisTitle, warning.Details?["part"]?.GetValue<string>());
        Assert.Equal($"slide/1/shape/{chartId}", warning.Location);
        Assert.Equal(
            $"Operation year (replace_text) found a match in an axis title of chart 'Sales' (shapeId {chartId}) in group 'Pair' (shapeId {groupId}) on slide 1 and left it as it was: replace_text does not change a chart's axis titles.",
            warning.Message);
        Assert.Equal(NoOperationHint, warning.Hint);
    }

    /// <summary>
    /// Where the chart title and data match as well, each part warns once, in the order title,
    /// axis title, data.
    /// </summary>
    [Fact]
    public void EachMatchingPart_WarnsInItsOwnWarning()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        Deck(input, horizontal: "2025", vertical: null, title: "2025", category: "2025");

        Assert.Equal(["chart-title", AxisTitle, "chart-data"], Parts(Replace(fixture, input, new SlidesReplaceTextOp { Find = "2025", Replace = "X" })));
    }

    /// <summary>
    /// Guards for the charts around an axis title: one whose title and data hold the match but no
    /// axis title of its own text, a pie, which has no axes, and an axis not drawn, whose title
    /// shows nothing, warn about no axis title.
    /// </summary>
    [Theory]
    [InlineData("no axis title")]
    [InlineData("pie")]
    [InlineData("hidden axis")]
    public void ChartsWithoutADrawnAxisTitleThatMatches_WarnAboutNoAxisTitle(string kind)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        using (var presentation = new Presentation())
        {
            IChart chart = presentation.Slides[0].Shapes.AddChart(kind == "pie" ? ChartType.Pie : ChartType.ClusteredColumn, 40, 120, 300, 200);
            chart.HasTitle = true;
            chart.ChartTitle.AddTextFrameForOverriding("2025");
            if (kind == "hidden axis")
            {
                chart.Axes.HorizontalAxis.HasTitle = true;
                chart.Axes.HorizontalAxis.Title.AddTextFrameForOverriding("2025");
                chart.Axes.HorizontalAxis.IsVisible = false;
            }
            else if (kind == "no axis title")
            {
                chart.Axes.HorizontalAxis.HasTitle = true;
            }

            presentation.Save(input, SaveFormat.Pptx);
        }

        Assert.Equal(["chart-title"], Parts(Replace(fixture, input, new SlidesReplaceTextOp { Find = "2025", Replace = "X" })));
    }

    private static SlidesEditResult Replace(SlidesEngineFixture fixture, string input, SlidesReplaceTextOp operation) =>
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [operation] },
            Output = TestOutput.At(fixture.File("replaced.pptx")),
        });

    private static string[] Parts(SlidesEditResult result) =>
        [.. (result.Warnings ?? [])
            .Where(static warning => warning.Code.Name == TextNotReplaced)
            .Select(static warning => warning.Details!["part"]!.GetValue<string>())];

    /// <summary>
    /// A two-slide deck: the second holds a text box named Box with the text 2025 and a column
    /// chart named Sales with the given axis titles, chart title and first category; returns the
    /// chart's shape id.
    /// </summary>
    private static long Deck(string path, string? horizontal, string? vertical, string? title = null, string? category = null)
    {
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60);
        box.Name = "Box";
        box.TextFrame.Text = "2025";
        IChart chart = slide.Shapes.AddChart(ChartType.ClusteredColumn, 40, 120, 300, 200);
        chart.Name = "Sales";
        chart.HasTitle = title is not null;
        if (title is not null)
        {
            chart.ChartTitle.AddTextFrameForOverriding(title);
        }

        if (category is not null)
        {
            chart.ChartData.Categories[0].AsCell.Value = category;
        }

        if (horizontal is not null)
        {
            chart.Axes.HorizontalAxis.HasTitle = true;
            chart.Axes.HorizontalAxis.Title.AddTextFrameForOverriding(horizontal);
        }

        if (vertical is not null)
        {
            chart.Axes.VerticalAxis.HasTitle = true;
            chart.Axes.VerticalAxis.Title.AddTextFrameForOverriding(vertical);
        }

        presentation.Save(path, SaveFormat.Pptx);
        return chart.OfficeInteropShapeId;
    }
}
