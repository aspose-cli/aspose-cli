using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

// Shape geometry and styles, footers, transitions, document properties and slide size.
internal sealed partial class SlidesMutationHandlers
{
    public long Apply(DeleteShapeOp operation)
    {
        Slide.Shapes.Remove(Shape);
        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(SetShapeStyleOp operation)
    {
        ApplyStyle(Shape, operation.Style);
        _touched.Add(Slide.SlideId);
        return 1;
    }

    public long Apply(SetShapeBoundsOp operation)
    {
        Shape.X = (float)(operation.X ?? Shape.X);
        Shape.Y = (float)(operation.Y ?? Shape.Y);
        Shape.Width = (float)(operation.Width ?? Shape.Width);
        Shape.Height = (float)(operation.Height ?? Shape.Height);
        _touched.Add(Slide.SlideId);
        return 1;
    }

    /// <summary>
    /// Styles a shape and all of its text. A table's fill and line are its cells' fills and
    /// borders; a chart's are its chart area's. A shape that shows no text, such as a picture,
    /// refuses a text style rather than reporting a change it cannot make.
    /// </summary>
    private static void ApplyStyle(IShape shape, SlidesShapeStyleInput style)
    {
        IBasePortionFormat[]? text = TextFormats(shape);
        bool styleText = style.Font is not null || style.LatinFont is not null || style.EastAsianFont is not null
            || style.Size is not null || style.Bold is not null || style.Color is not null;
        if (styleText && text is null)
        {
            throw new OperationInvalidException(
                $"Shape {shape.OfficeInteropShapeId} has no text to style.",
                "Give a picture or other object only 'fill' and 'line'; text styles apply to shapes with text, tables and charts.");
        }

        if (style.Fill is not null)
        {
            SetSolid(Fills(shape), style.Fill);
        }

        if (style.Line is not null)
        {
            SetSolid(Lines(shape), style.Line);
        }

        foreach (IBasePortionFormat format in text ?? [])
        {
            // PowerPoint picks a portion's font per character script, so font sets every
            // script's font: setting the Latin font alone would leave East Asian and
            // complex-script text on the theme font. A theme reference such as "+mn-lt"
            // is stored as written and resolved through the theme.
            if ((style.LatinFont ?? style.Font) is { } latin)
            {
                format.LatinFont = new FontData(latin);
            }

            if ((style.EastAsianFont ?? style.Font) is { } eastAsian)
            {
                format.EastAsianFont = new FontData(eastAsian);
            }

            if (style.Font is not null)
            {
                format.ComplexScriptFont = new FontData(style.Font);
            }

            if (style.Size is not null)
            {
                format.FontHeight = (float)style.Size.Value;
            }

            if (style.Bold is not null)
            {
                format.FontBold = style.Bold.Value ? NullableBool.True : NullableBool.False;
            }

            if (style.Color is not null)
            {
                SetSolid([format.FillFormat], style.Color);
            }
        }
    }

    /// <summary>
    /// The formats of every run of text a shape shows: a text frame's runs, every table cell's
    /// runs, or a chart's text with that of its title, legend, axes and shown data labels, which
    /// can each override the chart's own. Null for a shape that shows no text.
    /// </summary>
    private static IBasePortionFormat[]? TextFormats(IShape shape) => shape switch
    {
        IAutoShape { TextFrame: { } frame } => Runs(frame).ToArray(),
        ITable table => Cells(table).SelectMany(static cell => Runs(cell.TextFrame)).ToArray(),
        IChart chart => ChartTextFormats(chart).ToArray(),
        _ => null,
    };

    private static IEnumerable<IBasePortionFormat> ChartTextFormats(IChart chart)
    {
        yield return chart.TextFormat.PortionFormat;
        if (chart.HasTitle)
        {
            yield return chart.ChartTitle.TextFormat.PortionFormat;
            foreach (IBasePortionFormat run in chart.ChartTitle.TextFrameForOverriding is { } title ? Runs(title) : [])
            {
                yield return run;
            }
        }

        if (chart.HasLegend)
        {
            yield return chart.Legend.TextFormat.PortionFormat;
        }

        // A pie has no axes.
        IAxis?[] axes =
        [
            chart.Axes?.HorizontalAxis, chart.Axes?.VerticalAxis,
            chart.Axes?.SecondaryHorizontalAxis, chart.Axes?.SecondaryVerticalAxis,
        ];
        foreach (IAxis axis in axes.OfType<IAxis>().Where(static axis => axis.IsVisible))
        {
            yield return axis.TextFormat.PortionFormat;
        }

        foreach (IDataLabelFormat labels in chart.ChartData.Series
            .Select(static series => series.Labels.DefaultDataLabelFormat)
            .Where(static labels => labels.ShowValue || labels.ShowCategoryName || labels.ShowSeriesName || labels.ShowPercentage))
        {
            yield return labels.TextFormat.PortionFormat;
        }
    }

    private static IEnumerable<IBasePortionFormat> Runs(ITextFrame frame) =>
        frame.Paragraphs.SelectMany(static paragraph => paragraph.Portions).Select(static portion => portion.PortionFormat);

    private static IEnumerable<ICell> Cells(ITable table) => table.Rows.SelectMany(static row => row);

    private static IEnumerable<IFillFormat> Fills(IShape shape) => shape is ITable table
        ? Cells(table).Select(static cell => cell.CellFormat.FillFormat)
        : [shape.FillFormat];

    private static IEnumerable<ILineFillFormat> Lines(IShape shape) => shape is ITable table
        ? Cells(table).SelectMany(static cell => new[]
        {
            cell.CellFormat.BorderTop, cell.CellFormat.BorderBottom, cell.CellFormat.BorderLeft, cell.CellFormat.BorderRight,
        }).Select(static border => border.FillFormat)
        : [shape.LineFormat.FillFormat];

    private static void SetSolid(IEnumerable<IFillFormat> fills, string color)
    {
        foreach (IFillFormat fill in fills)
        {
            fill.FillType = FillType.Solid;
            fill.SolidFillColor.Color = ParseColor(color);
        }
    }

    private static void SetSolid(IEnumerable<ILineFillFormat> fills, string color)
    {
        foreach (ILineFillFormat fill in fills)
        {
            fill.FillType = FillType.Solid;
            fill.SolidFillColor.Color = ParseColor(color);
        }
    }

    public long Apply(SetFooterOp operation)
    {
        long changed = 0;
        foreach (ISlide slide in Slides)
        {
            string before = FooterState(slide);
            IBaseSlideHeaderFooterManager manager = slide.HeaderFooterManager.AsIBaseSlideHeaderFooterManager;
            if (operation.Text is not null)
            {
                manager.SetFooterText(operation.Text);
                manager.SetFooterVisibility(true);
            }

            if (operation.ShowNumber is not null)
            {
                manager.SetSlideNumberVisibility(operation.ShowNumber.Value);
            }

            if (operation.ShowDate is not null)
            {
                manager.SetDateTimeVisibility(operation.ShowDate.Value);
            }

            FillEmptyFooters(slide, operation);
            if (FooterState(slide) != before)
            {
                _touched.Add(slide.SlideId);
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// The footer, number and date placeholders a slide shows, with their text. A slide whose
    /// layout has none of them, as a title layout often hides footers, shows nothing new.
    /// </summary>
    private static string FooterState(ISlide slide) => string.Join('\n', slide.Shapes
        .Where(static shape => shape.Placeholder?.Type
            is PlaceholderType.Footer or PlaceholderType.SlideNumber or PlaceholderType.DateAndTime)
        .Select(static shape => $"{shape.Placeholder!.Type}:{(shape as IAutoShape)?.TextFrame?.Text}"));

    // A footer placeholder activated from the layout starts empty; give it the requested text.
    private static void FillEmptyFooters(ISlide slide, SetFooterOp op)
    {
        if (op.Text is null)
        {
            return;
        }

        foreach (IShape shape in slide.Shapes.Where(static shape => shape.Placeholder?.Type == PlaceholderType.Footer))
        {
            if (shape is IAutoShape { TextFrame: not null } footer
                && string.IsNullOrEmpty(footer.TextFrame.Text))
            {
                footer.TextFrame.Text = op.Text;
            }
        }
    }


    public long Apply(SetTransitionOp operation)
    {
        foreach (ISlide slide in Slides)
        {
            if (operation.Kind is not null)
            {
                slide.SlideShowTransition.Type = operation.Kind switch
                {
                    SlidesTransitionKinds.None => TransitionType.None,
                    SlidesTransitionKinds.Fade => TransitionType.Fade,
                    SlidesTransitionKinds.Push => TransitionType.Push,
                    SlidesTransitionKinds.Wipe => TransitionType.Wipe,
                    SlidesTransitionKinds.Split => TransitionType.Split,
                    SlidesTransitionKinds.Cover => TransitionType.Cover,
                    _ => throw new OperationInvalidException($"Unknown transition '{operation.Kind}'."),
                };
            }

            if (operation.DurationMs is not null)
            {
                slide.SlideShowTransition.Duration = operation.DurationMs.Value;
            }
            _touched.Add(slide.SlideId);
        }

        return Slides.Count;
    }

    public long Apply(SlidesSetPropertiesOp operation)
    {
        if (operation.Title is not null)
        {
            _presentation.DocumentProperties.Title = operation.Title;
        }

        if (operation.Author is not null)
        {
            _presentation.DocumentProperties.Author = operation.Author;
        }

        if (operation.Subject is not null)
        {
            _presentation.DocumentProperties.Subject = operation.Subject;
        }

        if (operation.Keywords is not null)
        {
            _presentation.DocumentProperties.Keywords = operation.Keywords;
        }

        if (operation.Company is not null)
        {
            _presentation.DocumentProperties.Company = operation.Company;
        }
        return 1;
    }

    public long Apply(SetSlideSizeOp operation)
    {
        SlideSizeScaleType scale = operation.ScaleContent
            ? SlideSizeScaleType.EnsureFit
            : SlideSizeScaleType.DoNotScale;
        if (operation.Size is "16x9" or "4x3")
        {
            _presentation.SlideSize.SetSize(
                operation.Size == "16x9" ? SlideSizeType.OnScreen16x9 : SlideSizeType.OnScreen,
                scale);
        }
        else if (operation.TryGetPoints(out float width, out float height))
        {
            _presentation.SlideSize.SetSize(width, height, scale);
        }
        else
        {
            throw new OperationInvalidException($"Unsupported slide size '{operation.Size}'.");
        }

        foreach (ISlide slide in _presentation.Slides)
        {
            _touched.Add(slide.SlideId);
        }
        return _presentation.Slides.Count;
    }
}
