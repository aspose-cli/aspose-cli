using Aspose.Slides;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.Editing.SlidesMutationSupport;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesShapeFormats;

namespace Aspose.Cli.Product.Slides.Engine.Editing;

// Shape geometry and styles, footers, transitions, document properties and slide size.
internal sealed partial class SlidesMutationHandlers
{
    public ValueTuple Apply(DeleteShapeOp operation)
    {
        Slide.Shapes.Remove(Shape);
        return default;
    }

    public ValueTuple Apply(FormatShapeOp operation)
    {
        ApplyStyle(Shape, operation.Style);
        return default;
    }

    public ValueTuple Apply(SetShapeBoundsOp operation)
    {
        Shape.X = (float)(operation.X ?? Shape.X);
        Shape.Y = (float)(operation.Y ?? Shape.Y);
        Shape.Width = (float)(operation.Width ?? Shape.Width);
        Shape.Height = (float)(operation.Height ?? Shape.Height);
        return default;
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
            || style.FontSize is not null || style.Bold is not null || style.Color is not null;
        if (styleText && text is null)
        {
            throw new OperationInvalidException(
                $"Shape {shape.OfficeInteropShapeId} has no text to style.",
                "Give a picture or other object only 'fillColor' and 'line'; text styles apply to shapes with text, tables and charts.");
        }

        if (style.FillColor is not null)
        {
            SetSolid(Fills(shape), style.FillColor);
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

            if (style.FontSize is not null)
            {
                format.FontHeight = (float)style.FontSize.Value;
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

    public ValueTuple Apply(SetFooterOp operation)
    {
        foreach (ISlide slide in Slides)
        {
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
        }

        return default;
    }

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


    public ValueTuple Apply(SetTransitionOp operation)
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
        }

        return default;
    }

    public ValueTuple Apply(SlidesSetPropertiesOp operation)
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
        return default;
    }

    public ValueTuple Apply(SetSlideSizeOp operation)
    {
        SlideSizeScaleType scale = operation.ScaleContent
            ? SlideSizeScaleType.EnsureFit
            : SlideSizeScaleType.DoNotScale;
        if (operation.SlideSize.Preset is { } preset)
        {
            _presentation.SlideSize.SetSize(
                preset == SlidesSlideSizePresets.Widescreen ? SlideSizeType.OnScreen16x9 : SlideSizeType.OnScreen,
                scale);
        }
        else
        {
            _presentation.SlideSize.SetSize(
                (float)operation.SlideSize.Width!.Value,
                (float)operation.SlideSize.Height!.Value,
                scale);
        }

        return default;
    }
}
