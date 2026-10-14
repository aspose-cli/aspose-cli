using System.Globalization;
using System.Security.Cryptography;
using Aspose.Cli.Product.Slides.Engine.Editing;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// The state of a presentation that operations write, read in one place: what <c>slides read</c>
/// and <c>slides inspect</c> report under the names the operations write it, the same state as
/// the engine stores it, which the edit snapshots and <c>--verify</c> compare because a read
/// reports some of it coarser, and the address of every object an edit targets. Text a read
/// reports counts against its budget.
/// </summary>
internal static class SlidesStateProjection
{
    /// <summary>The unit of a slide's address, whose changed ones are the slides a batch touched.</summary>
    internal const string SlideUnit = "slide";

    // Addresses: slide/3, slide/3/notes, slide/3/shape/7 (by shape id), section/Q3 plan,
    // property/title (by escaped name), presentation/slide-size and <unit>/all.
    internal const string SlideSizeAddress = "presentation/slide-size";

    internal static string SlideAddress(int number) => string.Create(CultureInfo.InvariantCulture, $"{SlideUnit}/{number}");

    internal static string NotesAddress(int number) => string.Create(CultureInfo.InvariantCulture, $"{SlideUnit}/{number}/notes");

    internal static string ShapeAddress(int number, long shapeId) =>
        string.Create(CultureInfo.InvariantCulture, $"{SlideUnit}/{number}/shape/{shapeId}");

    internal static string SectionAddress(string name) => $"section/{TargetName.Escape(name)}";

    internal static string PropertyAddress(string name) => $"property/{TargetName.Escape(name)}";

    internal static string AllAddress(string unit) => $"{unit}/all";

    /// <summary>The id of each slide in presentation order: inserting, deleting, copying or moving slides changes it.</summary>
    internal static IEnumerable<string> SlideIds(Presentation presentation) =>
        presentation.Slides.Select(static slide => slide.SlideId.ToString(CultureInfo.InvariantCulture));

    /// <summary>Each section by its address and the id of its first slide, <c>-</c> without one.</summary>
    internal static IEnumerable<(string Address, string State)> Sections(Presentation presentation) =>
        presentation.Sections.Select(static section => (
            SectionAddress(section.Name),
            section.StartedFromSlide?.SlideId.ToString(CultureInfo.InvariantCulture) ?? "-"));

    /// <summary>The slide size and the preset it matches.</summary>
    internal static SlidesSlideSizeInfo SlideSize(Presentation presentation) =>
        SlidesSlideSizeInfo.Of(presentation.SlideSize.Size.Width, presentation.SlideSize.Size.Height);

    /// <summary>Which layout a slide follows, by its master's name and its own, or null without one.</summary>
    internal static string? Layout(ISlide slide) =>
        slide.LayoutSlide is { } layout ? $"{layout.MasterSlide?.Name}|{layout.Name}" : null;

    /// <summary>
    /// Where a slide's layout is among the presentation's layouts, which tells apart layouts of the
    /// same name under masters of the same name.
    /// </summary>
    internal static string LayoutPosition(ISlide slide) =>
        slide.LayoutSlide is { Presentation: { } owner } layout
            ? owner.LayoutSlides.TakeWhile(item => !ReferenceEquals(item, layout)).Count().ToString(CultureInfo.InvariantCulture)
            : "-";

    /// <summary>
    /// One top-level shape as <c>slides read</c> reports it, with the <paramref name="text"/> the
    /// caller read; with <paramref name="full"/> also its runs, paragraphs, fill, outline, table
    /// and chart, as <c>--content full</c> reports them.
    /// </summary>
    internal static SlidesShapeData Shape(IShape shape, string? text, bool full, ref int remaining, ref bool truncated) => new()
    {
        ShapeId = shape.OfficeInteropShapeId,
        ShapeName = SlidesEngineSupport.EmptyToNull(shape.Name),
        Kind = SlidesEngineSupport.ShapeKind(shape),
        Placeholder = SlidesPlaceholders.Role(shape.Placeholder?.Type),
        Geometry = SlidesEngineSupport.Geometry(shape),
        AltText = SlidesEngineSupport.EmptyToNull(shape.AlternativeText),
        Text = text,
        Runs = full ? SlidesEngineSupport.Runs(shape, ref remaining, ref truncated) : null,
        Paragraphs = full ? Paragraphs(shape, ref remaining, ref truncated) : null,
        FillColor = full ? FillColor(shape) : null,
        Line = full ? Line(shape) : null,
        RowCount = full && shape is ITable rows ? rows.Rows.Count : null,
        ColumnCount = full && shape is ITable columns ? columns.Columns.Count : null,
        Values = full && shape is ITable table ? Values(table, ref remaining, ref truncated) : null,
        Chart = full && shape is IChart chart ? Chart(chart, ref remaining, ref truncated) : null,
        Rect = new SlidesRect { X = shape.X, Y = shape.Y, Width = shape.Width, Height = shape.Height },
    };

    /// <summary>The one solid color every fill of a shape (each cell of a table) states, as #RRGGBB, or null.</summary>
    internal static string? FillColor(IShape shape) =>
        OneColor(SlidesMutationHandlers.Fills(shape).Select(static fill => (fill.FillType, fill.SolidFillColor.Color)));

    /// <summary>The one solid color every outline of a shape (each cell border of a table) states, as #RRGGBB, or null.</summary>
    internal static string? Line(IShape shape) =>
        OneColor(SlidesMutationHandlers.Lines(shape).Select(static line => (line.FillType, line.SolidFillColor.Color)));

    /// <summary>The paragraphs of a shape's own text frame with the list, bullets or numbers at a 1-based level, each one belongs to.</summary>
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
                List = format.Bullet.Type switch
                {
                    BulletType.None or BulletType.NotDefined => null,
                    BulletType.Numbered => new SlidesListData { Kind = "number", Level = format.Depth + 1 },
                    _ => new SlidesListData { Kind = "bullet", Level = format.Depth + 1 },
                },
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

    /// <summary>
    /// What operations write on a shape as the engine stores it, which a read reports coarser or
    /// not at all: its kind, name, alternative text, geometry, fills, outlines, stated paragraph
    /// levels and bullets, run text and stated run formats, picture bytes and chart data.
    /// </summary>
    internal static IEnumerable<string> StoredShape(IShape shape)
    {
        yield return string.Create(CultureInfo.InvariantCulture,
            $"{shape.GetType().Name}|{shape.Name}|{shape.AlternativeText}|{shape.X:R},{shape.Y:R},{shape.Width:R},{shape.Height:R}");
        foreach (IFillFormat fill in SlidesMutationHandlers.Fills(shape))
        {
            yield return "fill|" + Fill(fill);
        }

        foreach (ILineFillFormat line in SlidesMutationHandlers.Lines(shape))
        {
            yield return $"line|{line.FillType}|{Color(line.FillType, line.SolidFillColor.Color)}";
        }

        foreach (IParagraph paragraph in SlidesEngineSupport.TextFrames(shape).SelectMany(static frame => frame.Paragraphs))
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"paragraph|{paragraph.ParagraphFormat.Depth}|{paragraph.ParagraphFormat.Bullet.Type}");
            foreach (IPortion portion in paragraph.Portions)
            {
                yield return "run|" + portion.Text;
            }
        }

        foreach (IBasePortionFormat format in SlidesMutationHandlers.TextFormats(shape) ?? [])
        {
            yield return string.Create(CultureInfo.InvariantCulture,
                $"format|{format.LatinFont?.FontName}|{format.EastAsianFont?.FontName}|{format.ComplexScriptFont?.FontName}|{format.FontHeight:R}|{format.FontBold}|{Fill(format.FillFormat)}");
        }

        if (shape is IPictureFrame { PictureFormat.Picture.Image: { } image })
        {
            yield return "image|" + Digest(image);
        }

        if (shape is IChart chart)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"chart|{chart.Type}|{chart.HasTitle}|{chart.ChartTitle?.TextFrameForOverriding?.Text}");
            foreach (IChartCategory category in chart.ChartData.Categories)
            {
                yield return "category|" + category.Value;
            }

            foreach (IChartSeries series in chart.ChartData.Series)
            {
                yield return "series|" + series.Name.AsCells.FirstOrDefault()?.Value;
                foreach (IChartDataPoint point in series.DataPoints)
                {
                    yield return string.Create(CultureInfo.InvariantCulture, $"point|{point.XValue?.Data}|{point.YValue?.Data}|{point.Value?.Data}");
                }
            }
        }
    }

    /// <summary>A slide's background as stored, which a read reports coarser: whether it is the slide's own, and its whole fill.</summary>
    internal static string StoredBackground(ISlide slide) => $"{slide.Background.Type}|{Fill(slide.Background.FillFormat)}";

    /// <summary>The transition into a slide as stored: its type and duration, also those no operation sets.</summary>
    internal static string TransitionState(ISlide slide) =>
        string.Create(CultureInfo.InvariantCulture, $"{slide.SlideShowTransition.Type}|{slide.SlideShowTransition.Duration}");

    /// <summary>A slide's footer as stored: whether it shows the footer, number and date, and the text of every footer placeholder.</summary>
    internal static string FooterState(ISlide slide) =>
        $"{slide.HeaderFooterManager.IsFooterVisible}|{slide.HeaderFooterManager.IsSlideNumberVisible}|{slide.HeaderFooterManager.IsDateTimeVisible}|"
        + string.Join("\n", slide.Shapes
            .Where(static shape => shape.Placeholder?.Type == PlaceholderType.Footer)
            .Select(static shape => (shape as IAutoShape)?.TextFrame?.Text));

    /// <summary>The paragraphs of a shape's own text frame as stored, each with its effective level and bullet, or null without a frame.</summary>
    internal static string? ParagraphState(IShape shape) => shape is IAutoShape { TextFrame: { } frame }
        ? string.Join("\n", frame.Paragraphs.Select(static paragraph => string.Create(CultureInfo.InvariantCulture,
            $"{paragraph.Text}|{paragraph.ParagraphFormat.GetEffective().Depth}|{paragraph.ParagraphFormat.GetEffective().Bullet.Type}")))
        : null;

    /// <summary>Each paragraph's text as stored in a slide's shapes, but the evaluation watermark, and notes.</summary>
    internal static IEnumerable<string> ParagraphTexts(ISlide slide, bool shapes, bool notes) =>
        (shapes ? slide.Shapes.Where(static shape => !SlidesEngineSupport.IsEvaluationWatermark(shape)).SelectMany(SlidesEngineSupport.TextFrames) : [])
            .Concat(notes && slide.NotesSlideManager.NotesSlide?.NotesTextFrame is { } frame ? [frame] : [])
            .SelectMany(static frame => frame.Paragraphs)
            .Select(static paragraph => paragraph.Text)
            .OfType<string>();

    private static string Fill(IFillFormat fill) =>
        fill.FillType == FillType.Picture
            ? "Picture|" + (fill.PictureFillFormat.Picture.Image is { } image ? Digest(image) : "-")
            : $"{fill.FillType}|{Color(fill.FillType, fill.SolidFillColor.Color)}";

    /// <summary>A digest of a picture's bytes: two pictures of the same size differ by their content.</summary>
    private static string Digest(IPPImage image) => Convert.ToHexStringLower(SHA256.HashData(image.BinaryData));

    private static string Color(FillType type, System.Drawing.Color color) =>
        type == FillType.Solid ? color.ToArgb().ToString("X8", CultureInfo.InvariantCulture) : "-";

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
