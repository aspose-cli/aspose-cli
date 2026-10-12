using System.Globalization;
using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// The state <c>slides read --content full</c> reports under the names the operations write it:
/// a shape's fill, outline, paragraphs, table cells and chart data, and a slide's own
/// background, transition and footer. Text it reports counts against the read's budget.
/// </summary>
internal static class SlidesStateProjection
{
    /// <summary>The one solid color every fill of a shape (each cell of a table) states, as #RRGGBB, or null.</summary>
    internal static string? FillColor(IShape shape) =>
        OneColor(SlidesMutationHandlers.Fills(shape).Select(static fill => (fill.FillType, fill.SolidFillColor.Color)));

    /// <summary>The one solid color every outline of a shape (each cell border of a table) states, as #RRGGBB, or null.</summary>
    internal static string? Line(IShape shape) =>
        OneColor(SlidesMutationHandlers.Lines(shape).Select(static line => (line.FillType, line.SolidFillColor.Color)));

    /// <summary>The paragraphs of a shape's own text frame with their 1-based level and whether they show a bullet.</summary>
    internal static IReadOnlyList<SlidesParagraphData>? Paragraphs(IShape shape, ref int remaining, ref bool truncated)
    {
        if (shape is not IAutoShape { TextFrame: { } frame } || string.IsNullOrWhiteSpace(frame.Text))
        {
            return null;
        }

        var paragraphs = new List<SlidesParagraphData>();
        foreach (IParagraph paragraph in frame.Paragraphs)
        {
            IParagraphFormatEffectiveData format = paragraph.ParagraphFormat.GetEffective();
            paragraphs.Add(new SlidesParagraphData
            {
                Text = SlidesEngineSupport.Take(paragraph.Text, ref remaining, ref truncated) ?? string.Empty,
                Level = format.Depth + 1,
                Bullet = format.Bullet.Type != BulletType.None,
            });
        }

        return paragraphs.Count == 0 ? null : paragraphs;
    }

    /// <summary>A table's cell text by row.</summary>
    internal static IReadOnlyList<IReadOnlyList<string>> Values(ITable table, ref int remaining, ref bool truncated)
    {
        var rows = new List<IReadOnlyList<string>>(table.Rows.Count);
        for (int row = 0; row < table.Rows.Count; row++)
        {
            var cells = new string[table.Columns.Count];
            for (int column = 0; column < table.Columns.Count; column++)
            {
                cells[column] = SlidesEngineSupport.Take(table[column, row].TextFrame?.Text, ref remaining, ref truncated) ?? string.Empty;
            }

            rows.Add(cells);
        }

        return rows;
    }

    /// <summary>A chart's kind, title, categories and series.</summary>
    internal static SlidesChartData Chart(IChart chart, ref int remaining, ref bool truncated)
    {
        bool scatter = ChartTypeCharacterizer.IsChartTypeScatter(chart.Type);
        var categories = new List<string>();
        foreach (IChartCategory category in chart.ChartData.Categories)
        {
            categories.Add(SlidesEngineSupport.Take(Text(category.Value), ref remaining, ref truncated) ?? string.Empty);
        }

        var series = new List<SlidesChartSeriesData>();
        foreach (IChartSeries item in chart.ChartData.Series)
        {
            series.Add(new SlidesChartSeriesData
            {
                Name = SlidesEngineSupport.Take(Text(item.Name.AsCells.FirstOrDefault()?.Value), ref remaining, ref truncated) ?? string.Empty,
                Values = [.. item.DataPoints.Select(point => SlidesChartUpdate.Number(scatter ? point.YValue.Data : point.Value.Data))],
                XValues = scatter ? [.. item.DataPoints.Select(static point => SlidesChartUpdate.Number(point.XValue.Data))] : null,
            });
        }

        return new SlidesChartData
        {
            Kind = chart.Type switch
            {
                ChartType.ClusteredBar => SlidesChartKinds.Bar,
                ChartType.ClusteredColumn => SlidesChartKinds.Column,
                ChartType.LineWithMarkers => SlidesChartKinds.Line,
                ChartType.Pie => SlidesChartKinds.Pie,
                ChartType.ScatterWithStraightLinesAndMarkers => SlidesChartKinds.Scatter,
                _ => null,
            },
            Title = chart.HasTitle
                ? SlidesEngineSupport.Take(chart.ChartTitle.TextFrameForOverriding?.Text, ref remaining, ref truncated)
                : null,
            Categories = categories,
            Series = series,
        };
    }

    /// <summary>A slide's own background, or null when it shows its layout's.</summary>
    internal static SlidesBackgroundData? Background(ISlide slide) =>
        slide.Background.Type != BackgroundType.OwnBackground
            ? null
            : new SlidesBackgroundData
            {
                Color = slide.Background.FillFormat.FillType == FillType.Solid
                    ? SlidesContrast.Hex(slide.Background.FillFormat.SolidFillColor.Color)
                    : null,
            };

    /// <summary>The transition into a slide.</summary>
    internal static SlidesTransitionData Transition(ISlide slide) => new()
    {
        Kind = slide.SlideShowTransition.Type switch
        {
            TransitionType.None => SlidesTransitionKinds.None,
            TransitionType.Fade => SlidesTransitionKinds.Fade,
            TransitionType.Push => SlidesTransitionKinds.Push,
            TransitionType.Wipe => SlidesTransitionKinds.Wipe,
            TransitionType.Split => SlidesTransitionKinds.Split,
            TransitionType.Cover => SlidesTransitionKinds.Cover,
            _ => null,
        },
        DurationMs = slide.SlideShowTransition.Duration is >= 0 and var duration ? duration : null,
    };

    /// <summary>The footer text, slide number and date a slide shows.</summary>
    internal static SlidesFooterData Footer(ISlide slide, ref int remaining, ref bool truncated)
    {
        string? text = slide.Shapes
            .Where(static shape => shape.Placeholder?.Type == PlaceholderType.Footer)
            .Select(static shape => (shape as IAutoShape)?.TextFrame?.Text)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
        return new SlidesFooterData
        {
            Text = SlidesEngineSupport.Take(text, ref remaining, ref truncated),
            ShowNumber = slide.HeaderFooterManager.IsSlideNumberVisible,
            ShowDate = slide.HeaderFooterManager.IsDateTimeVisible,
        };
    }

    private static string? OneColor(IEnumerable<(FillType Type, System.Drawing.Color Color)> fills)
    {
        (FillType Type, System.Drawing.Color Color)[] all = [.. fills];
        return all.Length > 0 && all.All(static fill => fill.Type == FillType.Solid)
            && all.Select(static fill => fill.Color.ToArgb()).Distinct().Count() == 1
            ? SlidesContrast.Hex(all[0].Color)
            : null;
    }

    private static string? Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);
}
