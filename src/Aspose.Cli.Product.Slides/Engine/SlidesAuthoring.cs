using System.Drawing;
using System.Globalization;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The one way authored titles, body text and pictures are placed on a slide, shared by
/// Markdown authoring and edit operations. Text goes into the layout's placeholders; a
/// layout without one gets an unstyled box placed proportionally on the canvas,
/// named so later operations find the same box again.
/// </summary>
internal static class SlidesAuthoring
{
    internal const string TitleName = "Title";
    internal const string BodyName = "Body";

    // A code span or block is the one semantic that needs a typeface the theme does not name.
    private const string CodeFont = "Consolas";

    internal static IAutoShape Title(ISlide slide) =>
        SlidesPlaceholders.Title(slide)
            ?? Named(slide, TitleName)
            ?? AddBox(slide, TitleName, new RectangleF(0.07f, 0.06f, 0.86f, 0.16f), TextAnchorType.Center);

    internal static IAutoShape Body(ISlide slide, bool includeSubtitle = false) =>
        SlidesPlaceholders.Content(slide, includeSubtitle).FirstOrDefault()
            ?? Named(slide, BodyName)
            ?? AddBox(slide, BodyName, new RectangleF(0.07f, 0.25f, 0.86f, 0.65f), TextAnchorType.Top);

    /// <summary>
    /// Replaces a frame's paragraphs. Formatting the paragraphs do not state, including
    /// bullets marked <see cref="ParagraphList.Inherit"/>, comes from the placeholder.
    /// </summary>
    internal static void WriteParagraphs(ITextFrame frame, IEnumerable<AuthoredParagraph> paragraphs)
    {
        frame.Paragraphs.Clear();
        foreach (AuthoredParagraph item in paragraphs)
        {
            var paragraph = new Paragraph();
            foreach (AuthoredRun run in item.Runs)
            {
                var portion = new Portion(run.Text);
                if (run.Bold)
                {
                    portion.PortionFormat.FontBold = NullableBool.True;
                }

                if (run.Italic)
                {
                    portion.PortionFormat.FontItalic = NullableBool.True;
                }

                if (run.Code)
                {
                    portion.PortionFormat.LatinFont = new FontData(CodeFont);
                }

                paragraph.Portions.Add(portion);
            }

            paragraph.ParagraphFormat.Depth = (short)item.Level;
            switch (item.List)
            {
                case ParagraphList.None:
                    paragraph.ParagraphFormat.Bullet.Type = BulletType.None;
                    break;
                case ParagraphList.Numbered:
                    paragraph.ParagraphFormat.Bullet.Type = BulletType.Numbered;
                    paragraph.ParagraphFormat.Bullet.NumberedBulletStyle = NumberedBulletStyle.BulletArabicPeriod;
                    break;
            }

            frame.Paragraphs.Add(paragraph);
            if (item.List == ParagraphList.None)
            {
                AlignWrappedLines(paragraph);
            }
        }
    }

    /// <summary>
    /// Moves the hanging indent that makes room for a level's bullet into the margin of a
    /// paragraph without one, as PowerPoint does when bullets are turned off, so its wrapped
    /// lines start where its first line does.
    /// </summary>
    private static void AlignWrappedLines(IParagraph paragraph)
    {
        IParagraphFormatEffectiveData effective = paragraph.ParagraphFormat.GetEffective();
        if (effective.Indent < 0)
        {
            paragraph.ParagraphFormat.MarginLeft = effective.MarginLeft + effective.Indent;
            paragraph.ParagraphFormat.Indent = 0;
        }
    }

    /// <summary>
    /// Adds a table of equal columns and equal rows filling the box. The table takes the
    /// presentation's default table style, so it follows the template, and its first row is
    /// the header row; a row grows past its share when its text needs more height.
    /// </summary>
    internal static ITable AddTable(ISlide slide, double x, double y, double width, double height, int rows, int columns)
    {
        double[] widths = Enumerable.Repeat(width / columns, columns).ToArray();
        double[] heights = Enumerable.Repeat(height / rows, rows).ToArray();
        ITable table = slide.Shapes.AddTable((float)x, (float)y, widths, heights);
        // Stated rather than left to the SDK's current default, which also marks it.
        table.FirstRow = true;
        return table;
    }

    /// <summary>
    /// Reports a table that ends below its area. Rows grow with their text at the table style's
    /// font size, so the laid-out height shows whether the table stays readable inside it.
    /// </summary>
    internal static Warning? TableOverflow(ITable table, int slideNumber, double areaHeight) =>
        // A table may end half a point below its area before it is reported.
        table.Height <= areaHeight + 0.5
            ? null
            : new Warning(SlidesDiagnostics.TableOverflow, string.Create(
                    CultureInfo.InvariantCulture,
                    $"The table on slide {slideNumber} is {table.Height:0} pt high but its area is {areaHeight:0} pt, so it runs past the area."))
            {
                Hint = "Split the table across slides under the same heading, shorten its cells or give it a smaller text size with set_shape_style, then review the slide.",
                Location = string.Create(CultureInfo.InvariantCulture, $"slide {slideNumber}"),
            };

    /// <summary>The largest rectangle with the image's aspect ratio, centered in the box.</summary>
    internal static RectangleF Fit(IPPImage image, RectangleF box)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return box;
        }

        float scale = Math.Min(box.Width / image.Width, box.Height / image.Height);
        float width = image.Width * scale;
        float height = image.Height * scale;
        return new RectangleF(box.X + ((box.Width - width) / 2), box.Y + ((box.Height - height) / 2), width, height);
    }

    /// <summary>A box given as fractions of the slide canvas, in points.</summary>
    internal static RectangleF Canvas(ISlide slide, RectangleF fraction)
    {
        SizeF size = slide.Presentation.SlideSize.Size;
        return new RectangleF(
            size.Width * fraction.X,
            size.Height * fraction.Y,
            size.Width * fraction.Width,
            size.Height * fraction.Height);
    }

    private static IAutoShape? Named(ISlide slide, string name) =>
        slide.Shapes.OfType<IAutoShape>().FirstOrDefault(shape =>
            shape.Placeholder is null
            && shape.TextFrame is not null
            && string.Equals(shape.Name, name, StringComparison.Ordinal));

    private static IAutoShape AddBox(ISlide slide, string name, RectangleF fraction, TextAnchorType anchor)
    {
        RectangleF box = Canvas(slide, fraction);
        // Without the default shape style the box has no fill, no outline and theme text color.
        IAutoShape shape = slide.Shapes.AddAutoShape(
            ShapeType.Rectangle,
            box.X,
            box.Y,
            box.Width,
            box.Height,
            createFromTemplate: false);
        shape.Name = name;
        shape.AddTextFrame(string.Empty).TextFrameFormat.AnchoringType = anchor;
        return shape;
    }
}

/// <summary>How an authored paragraph is listed.</summary>
internal enum ParagraphList
{
    /// <summary>Bullets as the placeholder's level defines them.</summary>
    Inherit,

    /// <summary>A plain paragraph without a bullet.</summary>
    None,

    /// <summary>An Arabic-numbered item.</summary>
    Numbered,
}

/// <summary>One run of authored text with its semantic emphasis.</summary>
internal sealed record AuthoredRun(string Text, bool Bold = false, bool Italic = false, bool Code = false);

/// <summary>One authored paragraph at a 0-based outline level.</summary>
internal sealed record AuthoredParagraph(IReadOnlyList<AuthoredRun> Runs, int Level, ParagraphList List);
