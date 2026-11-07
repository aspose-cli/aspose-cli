using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.TestKit.Scenarios;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using Xunit;

namespace Aspose.Cli.Product.Slides.Tests;

/// <summary>
/// replace_text does not change a chart's data: it warns <c>TEXT_NOT_REPLACED</c>, with
/// <c>details.part</c> <c>chart-data</c>, for each chart whose categories or series names match
/// as the operation matches text, and points to set_chart_data, which changes them. The title is
/// <see cref="SlidesChartTitleReplaceTests"/>'s. Evaluation mode reads text longer than five
/// characters cut short, so every text here is at most five characters long.
/// </summary>
public sealed class SlidesChartDataReplaceTests : IDisposable
{
    private const string TextNotReplaced = "TEXT_NOT_REPLACED";

    private const string ChartData = "chart-data";

    private readonly TempWorkspace _workspace = new();

    public SlidesChartDataReplaceTests() => ScenarioLicense.Project(_workspace.Path);

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// A find text that only a category holds replaces nothing and says where it is: the chart's
    /// categories, at the chart's shape address, with a hint to read them and set them with
    /// set_chart_data, which then changes the category.
    /// </summary>
    [Fact]
    public void ReplaceText_LeavesAMatchingCategoryAndPointsToSetChartData()
    {
        using var fixture = new SlidesEngineFixture();
        long chartId = Deck(_workspace.File("deck.pptx"));

        CliResult edit = _workspace.Run("slides", "edit", "deck.pptx", "--out", "out.pptx", "--output-format", "json",
            "--ops", """[{"op":"replace_text","id":"q","find":"Q2","replace":"Q9","scope":"all"}]""");

        Assert.True(edit.ExitCode == 0, edit.StdErr);
        JsonNode warning = Assert.Single(JsonNode.Parse(edit.StdOut)!["warnings"] as JsonArray ?? [],
            static warning => warning!["code"]!.GetValue<string>() == TextNotReplaced)!;
        Assert.Equal("""{"id":"q","op":"replace_text","part":"chart-data"}""", warning["details"]!.ToJsonString());
        Assert.Equal($"slide/2/shape/{chartId}", warning["location"]!.GetValue<string>());
        Assert.Null(warning["affectsCompleteness"]);
        Assert.Equal(
            $"Operation q (replace_text) found a match in the categories of chart 'Sales' (shapeId {chartId}) on slide 2 and left them as they were: replace_text does not change chart titles or chart data.",
            warning["message"]!.GetValue<string>());
        Assert.Equal(
            $"To change them, read the chart's categories and series with 'slides read --slides 2 --content full', then apply set_chart_data to shapeId {chartId} on slide 2 with the categories, changed where they match.",
            warning["hint"]!.GetValue<string>());
        Assert.Equal(["Q1", "Q2", "Q3"], Categories(_workspace.File("out.pptx")));

        CliResult set = _workspace.Run("slides", "edit", "out.pptx", "--out", "set.pptx", "--output-format", "json",
            "--ops", $$"""[{"op":"set_chart_data","slide":2,"shapeId":{{chartId}},"categories":["Q1","Q9","Q3"]}]""");

        Assert.True(set.ExitCode == 0, set.StdErr);
        Assert.Equal(["Q1", "Q9", "Q3"], Categories(_workspace.File("set.pptx")));
    }

    /// <summary>
    /// The warning names which data holds the match, the categories, the series names or both,
    /// and its hint says what to give set_chart_data; the title does not match, so it is the only
    /// warning about the chart.
    /// </summary>
    [Theory]
    [InlineData("Q2", "categories", "the categories, changed where they match")]
    [InlineData("S2", "series names", "every series as read, values and all, renamed where its name matches")]
    [InlineData("2", "categories and series names", "the categories and every series as read, each changed where it matches")]
    public void ChartDataWarning_NamesTheDataThatHoldsTheMatch(string find, string data, string change)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        long chartId = Deck(input);

        SlidesEditResult result = Replace(fixture, input, new SlidesReplaceTextOp { Id = "d", Find = find, Replace = "X" });

        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == TextNotReplaced);
        Assert.Equal(ChartData, warning.Details?["part"]?.GetValue<string>());
        Assert.Equal(
            $"Operation d (replace_text) found a match in the {data} of chart 'Sales' (shapeId {chartId}) on slide 2 and left them as they were: replace_text does not change chart titles or chart data.",
            warning.Message);
        Assert.EndsWith($"apply set_chart_data to shapeId {chartId} on slide 2 with {change}.", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// The data warning follows the operation's own matching and scope: a notes-only replace, a
    /// find text no data holds, and data that letter case, whole words or the pattern keep from
    /// matching give none, nor does a match in the title alone; data that matches gives one. The
    /// categories stay as they were.
    /// </summary>
    [Theory]
    [InlineData("Q2", false, false, false, "shapes", true)]
    [InlineData("Q2", false, false, false, "notes", false)]
    [InlineData("Q7", false, false, false, "all", false)]
    [InlineData("q2", false, true, false, "all", false)]
    [InlineData("q2", false, false, false, "all", true)]
    [InlineData("Q", false, false, true, "all", false)]
    [InlineData("Q", false, false, false, "all", true)]
    [InlineData(@"S\d", true, false, false, "all", true)]
    [InlineData(@"Z\d", true, false, false, "all", false)]
    [InlineData("Rev", false, false, false, "all", false)]
    public void ChartDataWarning_MatchesAsTheOperationMatchesText(
        string find, bool regex, bool caseSensitive, bool wholeWord, string scope, bool warns)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        Deck(input);

        SlidesEditResult result = Replace(fixture, input, new SlidesReplaceTextOp
        {
            Find = find,
            Replace = "X",
            Regex = regex,
            CaseSensitive = caseSensitive,
            WholeWord = wholeWord,
            Scope = scope,
        });

        Assert.Equal("ok", Assert.Single(result.Applied).Status);
        Assert.Equal(warns ? 1 : 0, (result.Warnings ?? []).Count(static warning =>
            warning.Code.Name == TextNotReplaced && warning.Details?["part"]?.GetValue<string>() == ChartData));
        Assert.Equal(["Q1", "Q2", "Q3"], Categories(fixture.File("replaced.pptx")));
    }

    /// <summary>
    /// The data of a chart set_chart_data refuses to update in place, a stock, surface or bubble
    /// chart or one with multi-level categories, warns with a hint that rebuilds the chart as its
    /// title's hint does, never one to set its data.
    /// </summary>
    [Theory]
    [InlineData(ChartType.OpenHighLowClose, false, "Q2", "categories")]
    [InlineData(ChartType.Surface3D, false, "Q2", "categories")]
    [InlineData(ChartType.Bubble, false, "S1", "series names")]
    [InlineData(ChartType.ClusteredColumn, true, "Q2", "categories")]
    public void ChartDataWarning_OfAChartSetChartDataCannotUpdate_RebuildsIt(ChartType type, bool multiLevel, string find, string data)
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("chart.pptx");
        long chartId = Deck(input, type, multiLevel);
        CliException refused = Assert.Throws<CliException>(() => SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [new SetChartDataOp { Slide = 2, ShapeId = chartId, Series = [new SlidesChartSeriesInput { Name = "S1", Values = [1, 2, 3], XValues = [1, 2, 3] }] }] },
            Output = TestOutput.At(fixture.File("set.pptx")),
        }));
        Assert.Equal(SlidesDiagnostics.ChartDataInvalid, refused.Code);

        SlidesEditResult result = Replace(fixture, input, new SlidesReplaceTextOp { Id = "r", Find = find, Replace = "X" });

        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == TextNotReplaced);
        Assert.Equal(ChartData, warning.Details?["part"]?.GetValue<string>());
        Assert.Contains($"found a match in the {data} of chart 'Sales'", warning.Message, StringComparison.Ordinal);
        Assert.Equal(
            $$"""To change them, rebuild the chart, since its data cannot be updated in place: read its kind, categories and series with 'slides read --slides 2 --content full', apply {"op":"delete_shape","slide":2,"shapeId":{{chartId}}}, then insert_chart on slide 2 with the changed data, its title and "rect":{"x":40,"y":120,"width":300,"height":200}. The new chart takes the automatic chart style.""",
            warning.Hint);
        Assert.DoesNotContain("set_chart_data", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    /// A chart inside a group is checked too: its matching category stays as it was and warns at
    /// the chart's own address, naming the slide's group that holds it, with a hint that no
    /// operation addresses it rather than one to set its data.
    /// </summary>
    [Fact]
    public void ChartInsideAGroup_WarnsThatNoOperationReachesItsData()
    {
        using var fixture = new SlidesEngineFixture();
        string input = fixture.File("grouped.pptx");
        (long chartId, long groupId) = GroupedDeck(input);

        SlidesEditResult result = Replace(fixture, input, new SlidesReplaceTextOp { Id = "g", Find = "Q2", Replace = "Q9" });

        Warning warning = Assert.Single(result.Warnings ?? [], static warning => warning.Code.Name == TextNotReplaced);
        Assert.Equal(ChartData, warning.Details?["part"]?.GetValue<string>());
        Assert.Equal($"slide/1/shape/{chartId}", warning.Location);
        Assert.Equal(
            $"Operation g (replace_text) found a match in the categories of chart 'Sales' (shapeId {chartId}) in group 'Pair' (shapeId {groupId}) on slide 1 and left them as they were: replace_text does not change chart titles or chart data.",
            warning.Message);
        Assert.StartsWith("No operation addresses a shape inside a group", warning.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("set_chart_data", warning.Hint, StringComparison.Ordinal);
    }

    private static SlidesEditResult Replace(SlidesEngineFixture fixture, string input, SlidesReplaceTextOp operation) =>
        SlidesEdit.Run(fixture.Session, new SlidesEditRequest
        {
            Input = input,
            Batch = new SlidesOpsBatch { Ops = [operation] },
            Output = TestOutput.At(fixture.File("replaced.pptx")),
        });

    private static string[] Categories(string path)
    {
        using var presentation = new Presentation(path);
        IChart chart = presentation.Slides.SelectMany(static slide => slide.Shapes).OfType<IChart>().Single();
        return [.. chart.ChartData.Categories.Select(static category => category.Value?.ToString() ?? string.Empty)];
    }

    /// <summary>
    /// A two-slide deck: the second holds a text box named Box with the text Intro and speaker
    /// notes Q2, and a chart of <paramref name="type"/> named Sales titled Rev with the categories
    /// Q1, Q2 and Q3, each under the group G when <paramref name="multiLevel"/>, and the series
    /// S1, S2 and so on; returns the chart's shape id.
    /// </summary>
    private static long Deck(string path, ChartType type = ChartType.ClusteredColumn, bool multiLevel = false)
    {
        using var presentation = new Presentation();
        ISlide slide = presentation.Slides.AddEmptySlide(presentation.LayoutSlides[0]);
        IAutoShape box = slide.Shapes.AddAutoShape(ShapeType.Rectangle, 40, 40, 200, 60);
        box.Name = "Box";
        box.TextFrame.Text = "Intro";
        slide.NotesSlideManager.AddNotesSlide().NotesTextFrame.Text = "Q2";
        IChart chart = Chart(slide.Shapes, type);
        if (multiLevel)
        {
            // A category's group sits in the column before it, so the data moves one column right.
            IChartDataWorkbook book = chart.ChartData.ChartDataWorkbook;
            chart.ChartData.Series.Clear();
            chart.ChartData.Categories.Clear();
            book.Clear(0);
            IChartSeries series = chart.ChartData.Series.Add(book.GetCell(0, 0, 2, "S1"), type);
            for (int index = 0; index < 3; index++)
            {
                IChartCategory category = chart.ChartData.Categories.Add(book.GetCell(0, index + 1, 1, $"Q{index + 1}"));
                category.GroupingLevels.SetGroupingItem(1, "G");
                series.DataPoints.AddDataPointForBarSeries(book.GetCell(0, index + 1, 2, index + 1));
            }

            Assert.True(chart.ChartData.Categories.GroupingLevelCount > 1);
        }

        presentation.Save(path, SaveFormat.Pptx);
        return chart.OfficeInteropShapeId;
    }

    /// <summary>
    /// A one-slide deck whose group named Pair holds the chart <see cref="Chart"/> makes; returns
    /// the chart's and the group's shape ids.
    /// </summary>
    private static (long Chart, long Group) GroupedDeck(string path)
    {
        using var presentation = new Presentation();
        IGroupShape group = presentation.Slides[0].Shapes.AddGroupShape();
        group.Name = "Pair";
        IChart chart = Chart(group.Shapes);
        presentation.Save(path, SaveFormat.Pptx);
        return (chart.OfficeInteropShapeId, group.OfficeInteropShapeId);
    }

    // A chart named Sales titled Rev, with the categories Q1, Q2 and Q3, when the kind has
    // categories, and the series S1, S2 and so on; a column chart unless another type is given.
    private static IChart Chart(IShapeCollection shapes, ChartType type = ChartType.ClusteredColumn)
    {
        IChart chart = shapes.AddChart(type, 40, 120, 300, 200, initWithSample: true);
        chart.Name = "Sales";
        chart.HasTitle = true;
        chart.ChartTitle.AddTextFrameForOverriding("Rev");
        while (chart.ChartData.Categories.Count > 3)
        {
            chart.ChartData.Categories.RemoveAt(chart.ChartData.Categories.Count - 1);
            foreach (IChartSeries series in chart.ChartData.Series)
            {
                series.DataPoints.RemoveAt(series.DataPoints.Count - 1);
            }
        }

        for (int index = 0; index < chart.ChartData.Categories.Count; index++)
        {
            chart.ChartData.Categories[index].AsCell.Value = $"Q{index + 1}";
        }

        for (int index = 0; index < chart.ChartData.Series.Count; index++)
        {
            chart.ChartData.Series[index].Name.AsCells[0].Value = $"S{index + 1}";
        }

        return chart;
    }
}
