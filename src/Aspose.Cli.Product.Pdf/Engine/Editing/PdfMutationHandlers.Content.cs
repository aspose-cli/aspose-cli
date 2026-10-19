using System.Globalization;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.Editing.PdfMutationSupport;
using static Aspose.Cli.Product.Pdf.Engine.PdfGeometry;
using static Aspose.Cli.Product.Pdf.Engine.PdfPages;
using static Aspose.Cli.Product.Pdf.Engine.PdfTextMatch;
using PdfColor = Aspose.Pdf.Color;

namespace Aspose.Cli.Product.Pdf.Engine.Editing;

// Visible content, annotations and redaction.
internal sealed partial class PdfMutationHandlers
{
    public ValueTuple Apply(AddWatermarkTextOp operation)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        foreach (int number in pages)
        {
            var stamp = new TextStamp(operation.Text)
            {
                Background = operation.Layer == PdfLayers.Under,
                Opacity = operation.Opacity,
                // The engine turns a stamp counterclockwise; the operation's rotation is clockwise.
                RotateAngle = -operation.Rotation,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            stamp.TextState.FontSize = (float)operation.FontSize;
            stamp.TextState.ForegroundColor = ParseColor(operation.Color);
            if (operation.Font is not null)
            {
                stamp.TextState.Font = FontRepository.FindFont(operation.Font);
            }

            _document.Pages[number].AddStamp(stamp);
        }

        return default;
    }

    public ValueTuple Apply(AddWatermarkImageOp operation)
    {
        EnsureFile(operation.Path);
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        // Read (and charge) the image once; every page stamps its own view of the bytes. An SVG
        // stamp fetches its external images with no resource hook (known issue PDF-HTML-EGRESS), so the
        // read refuses an SVG that names a network address. The engine loads a stamp's image
        // only when the document is saved, so a damaged image is refused here.
        byte[] image = OperationImages.Read(_inputs, operation.Path, refuseNetworkReferences: true);

        foreach (int number in pages)
        {
            var stamp = new ImageStamp(new MemoryStream(image, writable: false))
            {
                Background = operation.Layer == PdfLayers.Under,
                Opacity = operation.Opacity,
                Zoom = operation.ScalePercent / 100.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _document.Pages[number].AddStamp(stamp);
        }

        return default;
    }

    public ValueTuple Apply(AddPageNumbersOp operation)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        int ordinal = operation.Start;
        foreach (int number in pages)
        {
            string text = operation.Text
                .Replace("{n}", ordinal.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{N}", _document.Pages.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            AddTextStamp(_document.Pages[number], text, operation.Placement, operation.Font);
            ordinal++;
        }

        return default;
    }

    public ValueTuple Apply(AddHeaderTextOp operation) =>
        MarginText(operation.Text, operation.Pages, operation.Placement, operation.Font);

    public ValueTuple Apply(AddFooterTextOp operation) =>
        MarginText(operation.Text, operation.Pages, operation.Placement, operation.Font);

    public ValueTuple Apply(AddStampImageOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        EnsureFile(operation.Path);
        Rectangle rectangle = ToPdfRect(page, operation.Rect);
        byte[] image = OperationImages.Read(_inputs, operation.Path, refuseNetworkReferences: true);
        var stamp = new ImageStamp(new MemoryStream(image, writable: false))
        {
            XIndent = rectangle.LLX,
            YIndent = rectangle.LLY,
            Width = rectangle.Width,
            Height = rectangle.Height,
        };
        page.AddStamp(stamp);
        return default;
    }

    public ValueTuple Apply(AddHyperlinkOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        var annotation = new LinkAnnotation(page, ToPdfRect(page, operation.Rect))
        {
            Action = new GoToURIAction(operation.Url),
        };
        page.Annotations.Add(annotation);
        return default;
    }

    public ValueTuple Apply(RedactTextOp operation)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, operation.Pages);
        PdfColor fill = ParseColor(operation.FillColor);
        foreach (int number in pages)
        {
            Page page = _document.Pages[number];
            Unrotated(page, () =>
            {
                TextFragmentCollection fragments = MatchText(page, operation.Find, operation.Regex, operation.CaseSensitive,
                    static reason => new OperationInvalidException(reason));
                if (fragments.Count == 0)
                {
                    return;
                }

                ILookup<string, Point> before = TextRuns(page);
                // PDF-REDACT-TEXT-SHIFT moves the text that follows a removed match in the content
                // stream, wherever it is drawn. The search returns the matches in that order, so
                // they are removed last first: each one still lies where the search found it.
                foreach (TextFragment fragment in fragments.Reverse())
                {
                    Redact(page, fragment.Rectangle, fill);
                }
                NoteMovedText(number, page, before);
            });
        }

        return default;
    }

    public ValueTuple Apply(RedactAreaOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        Rectangle area = ToPdfRect(page, operation.Rect);
        Unrotated(page, () =>
        {
            ILookup<string, Point> before = TextRuns(page);
            Redact(page, area, ParseColor(operation.FillColor));
            NoteMovedText(operation.Page, page, before);
        });
        return default;
    }

    /// <summary>
    /// Removes the characters whose middle lies in the area and covers it. The engine removes
    /// every character its redaction touches, so on lines set closer than their characters are
    /// high it would also take the neighbouring lines' text the area only grazes. The cover is
    /// drawn short of each such character, and a selected character the shortened cover no
    /// longer touches is removed under a cover of its own at its middle.
    /// </summary>
    private static void Redact(Page page, Rectangle area, PdfColor fill)
    {
        Rectangle[] characters = Characters(page);
        Rectangle[] selected = [.. characters.Where(character => Contains(area, Middle(character)))];
        double left = area.LLX, bottom = area.LLY, right = area.URX, top = area.URY;
        foreach (Rectangle other in characters.Where(character => !Contains(area, Middle(character))))
        {
            if (!Touches(other, left, bottom, right, top))
            {
                continue;
            }

            (double x, double y) = Middle(other);
            double above = y > top ? (top - (other.LLY - Gap)) * (right - left) : double.MaxValue;
            double below = y < bottom ? ((other.URY + Gap) - bottom) * (right - left) : double.MaxValue;
            double before = x < left ? ((other.URX + Gap) - left) * (top - bottom) : double.MaxValue;
            double after = x > right ? (right - (other.LLX - Gap)) * (top - bottom) : double.MaxValue;
            double least = Math.Min(Math.Min(above, below), Math.Min(before, after));
            if (least == above) { top = other.LLY - Gap; }
            else if (least == below) { bottom = other.URY + Gap; }
            else if (least == before) { left = other.URX + Gap; }
            else { right = other.LLX - Gap; }
        }

        bool cover = left < right && bottom < top;
        foreach (Rectangle character in selected.Where(character => !cover || !Touches(character, left, bottom, right, top)))
        {
            (double x, double y) = Middle(character);
            double half = Math.Min(MiddleCover, Math.Min(character.Width, character.Height) / 4);
            Cover(page, new Rectangle(x - half, y - half, x + half, y + half), fill).Redact();
        }

        if (cover)
        {
            Cover(page, new Rectangle(left, bottom, right, top), fill).Redact();
        }
    }

    /// <summary>How far a shortened cover stays from a character it must not touch, in points.</summary>
    private const double Gap = 0.01;

    /// <summary>Half the side of the cover over the middle of one character, in points.</summary>
    private const double MiddleCover = 0.25;

    private static Rectangle[] Characters(Page page)
    {
        var absorber = new TextFragmentAbsorber();
        page.Accept(absorber);
        return [.. absorber.TextFragments
            .SelectMany(static fragment => fragment.Segments)
            .SelectMany(static segment => segment.Characters)
            .Select(static character => character.Rectangle)
            .Where(static rectangle => rectangle.Width > 0 && rectangle.Height > 0)];
    }

    private static (double X, double Y) Middle(Rectangle rectangle) =>
        ((rectangle.LLX + rectangle.URX) / 2, (rectangle.LLY + rectangle.URY) / 2);

    private static bool Contains(Rectangle area, (double X, double Y) point) =>
        point.X > area.LLX && point.X < area.URX && point.Y > area.LLY && point.Y < area.URY;

    private static bool Touches(Rectangle character, double left, double bottom, double right, double top) =>
        character.LLX <= right && character.URX >= left && character.LLY <= top && character.URY >= bottom;

    /// <summary>
    /// Redacts with the page's rotation cleared: on a rotated page the engine would draw the
    /// cover in the unrotated page's space and remove the text under it in the rotated one
    /// (PDF-REDACT-ROTATED). Upright the two are one, the space annotations are placed in.
    /// </summary>
    private static void Unrotated(Page page, Action redact)
    {
        Rotation rotation = page.Rotate;
        page.Rotate = Rotation.None;
        try
        {
            redact();
        }
        finally
        {
            page.Rotate = rotation;
        }
    }

    /// <summary>
    /// PDF-REDACT-TEXT-SHIFT: redaction moves the runs that followed the removed text left by
    /// its width. The page is noted when a run now lies left of every place a run of its text
    /// had on its line.
    /// </summary>
    private void NoteMovedText(int number, Page page, ILookup<string, Point> before)
    {
        if (TextRuns(page).Any(runs => runs.Any(run => MovedLeft(run, before[runs.Key]))))
        {
            _textMoved.Add(number);
        }
    }

    /// <summary>Whether a run lies left of every place a run of its text had on its line.</summary>
    private static bool MovedLeft(Point run, IEnumerable<Point> before)
    {
        Point[] line = [.. before.Where(earlier => Math.Abs(earlier.Y - run.Y) < 1)];
        return line.Length > 0 && line.All(earlier => earlier.X > run.X + 0.5);
    }

    /// <summary>Where each run of visible text on the page starts, by its text.</summary>
    private static ILookup<string, Point> TextRuns(Page page)
    {
        var absorber = new TextFragmentAbsorber();
        page.Accept(absorber);
        return absorber.TextFragments
            .Where(static fragment => !string.IsNullOrWhiteSpace(fragment.Text))
            .ToLookup(static fragment => fragment.Text, static fragment => new Point(fragment.Rectangle.LLX, fragment.Rectangle.LLY));
    }

    private ValueTuple MarginText(string text, string? pageRange, string position, string? font)
    {
        IReadOnlyList<int> pages = ResolveOptional(_document, pageRange);
        foreach (int number in pages)
        {
            AddTextStamp(_document.Pages[number], text, position, font);
        }

        return default;
    }

    private static void AddTextStamp(Page page, string text, string position, string? font)
    {
        var stamp = new TextStamp(text) { Background = false };
        if (font is not null)
        {
            stamp.TextState.Font = FontRepository.FindFont(font);
        }

        ApplyPosition(stamp, position);
        page.AddStamp(stamp);
    }

    /// <summary>
    /// Adds the redaction annotation that covers one rectangle in the requested
    /// colour. Aspose only builds the cover from FillColor once Color is also set;
    /// with FillColor alone it draws its default black box whatever was asked for.
    /// Color itself never reaches the page, so both carry the requested colour.
    /// </summary>
    private static RedactionAnnotation Cover(Page page, Rectangle rectangle, PdfColor fill)
    {
        var annotation = new RedactionAnnotation(page, rectangle)
        {
            FillColor = fill,
            Color = fill,
        };
        page.Annotations.Add(annotation);
        return annotation;
    }
}
