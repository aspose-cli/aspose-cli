using Aspose.Slides;
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

    private static void ApplyStyle(IShape shape, SlidesShapeStyleInput style)
    {
        if (style.Fill is not null)
        {
            shape.FillFormat.FillType = FillType.Solid;
            shape.FillFormat.SolidFillColor.Color = ParseColor(style.Fill);
        }

        if (style.Line is not null)
        {
            shape.LineFormat.FillFormat.FillType = FillType.Solid;
            shape.LineFormat.FillFormat.SolidFillColor.Color = ParseColor(style.Line);
        }

        if (shape is IAutoShape { TextFrame: not null } auto)
        {
            foreach (IPortion portion in auto.TextFrame.Paragraphs.SelectMany(paragraph => paragraph.Portions))
            {
                if (style.Font is not null)
                {
                    // PowerPoint picks a portion's font per character script. Setting
                    // the Latin font alone leaves East Asian and complex-script text
                    // on the theme font, so the requested change never appears.
                    var font = new FontData(style.Font);
                    portion.PortionFormat.LatinFont = font;
                    portion.PortionFormat.EastAsianFont = font;
                    portion.PortionFormat.ComplexScriptFont = font;
                }

                if (style.Size is not null)
                {
                    portion.PortionFormat.FontHeight = (float)style.Size.Value;
                }

                if (style.Bold is not null)
                {
                    portion.PortionFormat.FontBold = style.Bold.Value
                        ? NullableBool.True
                        : NullableBool.False;
                }
                if (style.Color is not null)
                {
                    portion.PortionFormat.FillFormat.FillType = FillType.Solid;
                    portion.PortionFormat.FillFormat.SolidFillColor.Color = ParseColor(style.Color);
                }
            }
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
