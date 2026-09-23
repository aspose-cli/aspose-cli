using System.Drawing;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Applies slide and section structure operations.</summary>
internal static class SlidesStructuralHandlers
{
    internal static long AddSlide(
        Presentation presentation,
        AddSlideOp op,
        ILayoutSlide? layout,
        ISet<uint> touched)
    {
        int at = op.At ?? presentation.Slides.Count + 1;
        if (at > presentation.Slides.Count + 1)
        {
            throw SlideNotFound(at, presentation.Slides.Count + 1);
        }

        ISlide slide = presentation.Slides.AddEmptySlide(layout ?? presentation.LayoutSlides[0]);
        if (at <= presentation.Slides.Count)
        {
            presentation.Slides.Reorder(at - 1, slide);
        }

        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long DeleteSlides(Presentation presentation, IReadOnlyList<ISlide> slides)
    {

        if (slides.Count == presentation.Slides.Count)
        {
            throw new InvalidOperationException("A presentation must retain at least one slide.");
        }

        foreach (ISlide slide in slides)
        {
            presentation.Slides.Remove(slide);
        }

        return slides.Count;
    }

    internal static long MoveSlide(
        Presentation presentation,
        MoveSlideOp op,
        ISlide slide,
        ISet<uint> touched)
    {
        if (op.To > presentation.Slides.Count)
        {
            throw SlideNotFound(op.To, presentation.Slides.Count);
        }

        presentation.Slides.Reorder(op.To - 1, slide);
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long DuplicateSlide(
        Presentation presentation,
        DuplicateSlideOp op,
        ISlide slide,
        ISet<uint> touched)
    {
        int at = op.At ?? presentation.Slides.Count + 1;
        if (at > presentation.Slides.Count + 1)
        {
            throw SlideNotFound(at, presentation.Slides.Count + 1);
        }

        ISlide clone = at == presentation.Slides.Count + 1
            ? presentation.Slides.AddClone(slide)
            : presentation.Slides.InsertClone(at - 1, slide);
        touched.Add(clone.SlideId);
        return 1;
    }

    internal static long SetHidden(
        IReadOnlyList<ISlide> slides,
        bool hidden,
        ISet<uint> touched)
    {
        foreach (ISlide slide in slides)
        {
            slide.Hidden = hidden;
            touched.Add(slide.SlideId);
        }

        return slides.Count;
    }

    internal static long ApplyLayout(
        IReadOnlyList<ISlide> slides,
        ILayoutSlide layout,
        ISet<uint> touched)
    {
        foreach (ISlide slide in slides)
        {
            slide.LayoutSlide = layout;
            touched.Add(slide.SlideId);
        }

        return slides.Count;
    }

    internal static long SetBackground(
        InputSource inputs,
        Presentation presentation,
        IReadOnlyList<ISlide> slides,
        SetBackgroundOp op,
        ISet<uint> touched)
    {
        IPPImage? image = null;
        if (op.ImagePath is not null)
        {
            EnsureFile(op.ImagePath);
            image = presentation.Images.AddImage(
                inputs.ReadAllBytes(op.ImagePath));
        }

        foreach (ISlide slide in slides)
        {
            slide.Background.Type = BackgroundType.OwnBackground;
            if (op.Color is not null)
            {
                slide.Background.FillFormat.FillType = FillType.Solid;
                slide.Background.FillFormat.SolidFillColor.Color = ParseColor(op.Color);
            }
            else
            {
                slide.Background.FillFormat.FillType = FillType.Picture;
                slide.Background.FillFormat.PictureFillFormat.PictureFillMode = PictureFillMode.Stretch;
                slide.Background.FillFormat.PictureFillFormat.Picture.Image = image;
            }

            touched.Add(slide.SlideId);
        }

        return slides.Count;
    }

    internal static long AddSection(Presentation presentation, AddSectionOp op, ISlide start)
    {
        if (presentation.Sections.Any(section => string.Equals(section.Name, op.Name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Section '{op.Name}' already exists.");
        }

        presentation.Sections.AddSection(op.Name, start);
        return 1;
    }

    internal static long AppendPresentation(
        SlidesPresentationLoader loader,
        Presentation destination,
        AppendPresentationOp op,
        ISet<uint> touched)
    {
        using LoadedPresentation source = loader.Open(op.Path, password: null);
        long count = 0;
        foreach (ISlide slide in source.Presentation.Slides)
        {
            ISlide clone = op.MasterPolicy == "keep-source"
                ? destination.Slides.AddClone(slide)
                : destination.Slides.AddClone(slide, destination.Masters[0], allowCloneMissingLayout: true);
            touched.Add(clone.SlideId);
            count++;
        }

        return count;
    }

    internal static long SetTitle(ISlide slide, string text, ISet<uint> touched)
    {
        IAutoShape? shape = SlidesPlaceholders.Title(slide);
        shape ??= slide.Shapes.OfType<IAutoShape>().FirstOrDefault(item =>
            item.Name.Contains("title", StringComparison.OrdinalIgnoreCase));
        shape ??= slide.Shapes.AddAutoShape(ShapeType.Rectangle, 54, 36, 612, 72);
        shape.TextFrame!.Text = text;
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long SetBody(
        ISlide slide,
        IReadOnlyList<SlidesParagraphInput> paragraphs,
        ISet<uint> touched)
    {
        IAutoShape? shape = SlidesPlaceholders.Content(slide).FirstOrDefault();
        shape ??=slide.Shapes.AddAutoShape(ShapeType.Rectangle, 72, 126, 576, 360);
        ITextFrame frame = shape.TextFrame!;
        frame.Paragraphs.Clear();
        foreach (SlidesParagraphInput input in paragraphs)
        {
            var paragraph = new Paragraph { Text = input.Text };
            paragraph.ParagraphFormat.Depth = (short)input.Level;
            if (input.Level >= 0)
            {
                paragraph.ParagraphFormat.Bullet.Type = BulletType.Symbol;
            }

            frame.Paragraphs.Add(paragraph);
        }

        touched.Add(slide.SlideId);
        return paragraphs.Count;
    }

}

