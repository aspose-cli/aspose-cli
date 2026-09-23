using System.Drawing;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Applies shape, footer, transition, property, and size operations.</summary>
internal static class SlidesStyleHandlers
{
    internal static long DeleteShape(ISlide slide, IShape shape, ISet<uint> touched)
    {
        slide.Shapes.Remove(shape);
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long SetShapeStyle(
        ISlide slide,
        IShape shape,
        SlidesShapeStyleInput style,
        ISet<uint> touched)
    {
        ApplyStyle(shape, style);
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static void ApplyStyle(IShape shape, SlidesShapeStyleInput style)
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

    internal static long SetFooter(
        IReadOnlyList<ISlide> slides,
        SetFooterOp op,
        ISet<uint> touched)
    {
        foreach (ISlide slide in slides)
        {
            IBaseSlideHeaderFooterManager manager = slide.HeaderFooterManager.AsIBaseSlideHeaderFooterManager;
            if (op.Text is not null)
            {
                manager.SetFooterText(op.Text);
                manager.SetFooterVisibility(true);
            }

            if (op.ShowNumber is not null)
            {
                manager.SetSlideNumberVisibility(op.ShowNumber.Value);
            }

            if (op.ShowDate is not null)
            {
                manager.SetDateTimeVisibility(op.ShowDate.Value);
            }

            FillEmptyFooters(slide, op);
            touched.Add(slide.SlideId);
        }

        return slides.Count;
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

    internal static long SetTransition(
        IReadOnlyList<ISlide> slides,
        SetTransitionOp op,
        ISet<uint> touched)
    {
        foreach (ISlide slide in slides)
        {
            if (op.Kind is not null)
            {
                slide.SlideShowTransition.Type = op.Kind switch
                {
                    "none" => TransitionType.None,
                    "fade" => TransitionType.Fade,
                    "push" => TransitionType.Push,
                    "wipe" => TransitionType.Wipe,
                    "split" => TransitionType.Split,
                    "cover" => TransitionType.Cover,
                    _ => throw new OperationInvalidException($"Unknown transition '{op.Kind}'."),
                };
            }

            if (op.DurationMs is not null)
            {
                slide.SlideShowTransition.Duration = op.DurationMs.Value;
            }
            touched.Add(slide.SlideId);
        }

        return slides.Count;
    }

    internal static long SetProperties(Presentation presentation, SlidesSetPropertiesOp op)
    {
        if (op.Title is not null)
        {
            presentation.DocumentProperties.Title = op.Title;
        }

        if (op.Author is not null)
        {
            presentation.DocumentProperties.Author = op.Author;
        }

        if (op.Subject is not null)
        {
            presentation.DocumentProperties.Subject = op.Subject;
        }

        if (op.Keywords is not null)
        {
            presentation.DocumentProperties.Keywords = op.Keywords;
        }

        if (op.Company is not null)
        {
            presentation.DocumentProperties.Company = op.Company;
        }
        return 1;
    }

    internal static long SetSlideSize(
        Presentation presentation,
        SetSlideSizeOp op,
        ISet<uint> touched)
    {
        SlideSizeScaleType scale = op.ScaleContent
            ? SlideSizeScaleType.EnsureFit
            : SlideSizeScaleType.DoNotScale;
        if (op.Size is "16x9" or "4x3")
        {
            presentation.SlideSize.SetSize(
                op.Size == "16x9" ? SlideSizeType.OnScreen16x9 : SlideSizeType.OnScreen,
                scale);
        }
        else
        {
            string[] parts = op.Size[..^2].Split('x', StringSplitOptions.TrimEntries);
            presentation.SlideSize.SetSize(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                scale);
        }

        foreach (ISlide slide in presentation.Slides)
        {
            touched.Add(slide.SlideId);
        }
        return presentation.Slides.Count;
    }

}

