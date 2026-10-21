using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Forms;
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
        // Read (and charge) the image once; every page stamps its own view of the bytes. The
        // engine loads a stamp's image only when the document is saved, so a damaged image is
        // refused here.
        byte[] image = OperationImages.Read(_inputs, operation.Path);

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
        MarginText(operation.Text, operation.Pages, $"top-{operation.Alignment}", operation.Font);

    public ValueTuple Apply(AddFooterTextOp operation) =>
        MarginText(operation.Text, operation.Pages, $"bottom-{operation.Alignment}", operation.Font);

    public ValueTuple Apply(AddStampImageOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        EnsureFile(operation.Path);
        Rectangle rectangle = ToPdfRect(page, operation.Rect);
        byte[] image = OperationImages.Read(_inputs, operation.Path);
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
        Regex pattern = TextPattern(operation.Find, operation.Regex, operation.CaseSensitive);
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

                HashSet<(double, double, double, double)> matched = [.. fragments
                    .SelectMany(static fragment => fragment.Segments)
                    .SelectMany(static segment => segment.Characters)
                    .Select(static character => Key(character.Rectangle))];
                ILookup<string, Point> before = TextRuns(page);
                // PDF-REDACT-TEXT-SHIFT moves the text that follows a removed match in the content
                // stream, wherever it is drawn. The search returns the matches in that order, so
                // they are removed last first: each one still lies where the search found it.
                Rectangle[] covers = [.. fragments.Reverse().Select(static fragment => fragment.Rectangle)];
                NoteBeside(number, Redact(page, covers, character => matched.Contains(Key(character)), fill));
                NoteMovedText(number, page, before);
            });
            RemoveAnnotations(page, pattern);
        }

        ClearFields(operation.Pages is null ? null : pages, pattern);
        return default;
    }

    public ValueTuple Apply(RedactAreaOp operation)
    {
        Page page = PageAt(_document, operation.Page);
        Rectangle area = ToPdfRect(page, operation.Rect);
        Unrotated(page, () =>
        {
            ILookup<string, Point> before = TextRuns(page);
            NoteBeside(operation.Page, Redact(page, [area], character => Contains(area, Middle(character)), ParseColor(operation.FillColor)));
            NoteMovedText(operation.Page, page, before);
        });
        return default;
    }

    /// <summary>
    /// Covers each rectangle and removes what lies under it, in the order given. The cover is
    /// never drawn smaller than asked, which would leave part of a scanned image of the text
    /// showing, and the engine also removes characters of a neighbouring line that a cover
    /// overlaps. Returns how many lines lost characters beside the <paramref name="selected"/>
    /// ones and how many such characters, not counting white space; never the text, which may be
    /// what a later redaction removes. Each line's text after the redaction is matched against its
    /// text before, since the engine moves what follows a removal left but keeps every line where
    /// it was.
    /// </summary>
    private static (int Lines, int Characters) Redact(Page page, IReadOnlyList<Rectangle> covers, Func<Rectangle, bool> selected, PdfColor fill)
    {
        List<Glyph> before = Glyphs(page, selected);
        foreach (Rectangle cover in covers)
        {
            Cover(page, cover, fill).Redact();
        }

        ILookup<double, char> after = Glyphs(page, static _ => false).ToLookup(static glyph => glyph.Line, static glyph => glyph.Text);
        int lines = 0, characters = 0;
        foreach (IGrouping<double, Glyph> line in before.GroupBy(static glyph => glyph.Line))
        {
            Glyph[] glyphs = [.. line];
            int next = 0, lost = 0;
            foreach (char kept in after[line.Key])
            {
                for (; next < glyphs.Length && glyphs[next].Text != kept; next++)
                {
                    lost += Beside(glyphs[next]);
                }

                next++;
            }

            for (; next < glyphs.Length; next++)
            {
                lost += Beside(glyphs[next]);
            }

            lines += lost > 0 ? 1 : 0;
            characters += lost;
        }

        return (lines, characters);

        static int Beside(Glyph glyph) => glyph.Selected || char.IsWhiteSpace(glyph.Text) ? 0 : 1;
    }

    /// <summary>One character of a page's text: the line it lies on, by the bottom of its box, its text, and whether it is selected.</summary>
    private readonly record struct Glyph(double Line, char Text, bool Selected);

    /// <summary>The characters of the page's text in content order.</summary>
    private static List<Glyph> Glyphs(Page page, Func<Rectangle, bool> selected)
    {
        var glyphs = new List<Glyph>();
        var absorber = new TextFragmentAbsorber();
        page.Accept(absorber);
        foreach (TextSegment segment in absorber.TextFragments.SelectMany(static fragment => fragment.Segments))
        {
            int index = 0;
            foreach (CharInfo character in segment.Characters)
            {
                if (index < segment.Text.Length)
                {
                    glyphs.Add(new Glyph(Math.Round(character.Rectangle.LLY, 1), segment.Text[index], selected(character.Rectangle)));
                }

                index++;
            }
        }

        return glyphs;
    }

    /// <summary>
    /// Removes each annotation whose text matches, and the pop-up that shows a removed one's
    /// text. A form field's widget is left to <see cref="ClearFields"/>.
    /// </summary>
    private static void RemoveAnnotations(Page page, Regex pattern)
    {
        Annotation[] matching = [.. page.Annotations.Where(annotation => annotation switch
        {
            WidgetAnnotation => false,
            PopupAnnotation popup => popup.Parent is { } parent && PdfRedactionAudit.Matches(pattern, PdfRedactionAudit.Texts(parent)),
            _ => PdfRedactionAudit.Matches(pattern, PdfRedactionAudit.Texts(annotation)),
        })];
        foreach (Annotation annotation in matching)
        {
            page.Annotations.Delete(annotation);
        }
    }

    /// <summary>
    /// Clears each form field whose value matches, as set_form_field clears it: the whole value,
    /// so no part of it shows in the field. With <paramref name="pages"/>, only the fields on them.
    /// </summary>
    private void ClearFields(IReadOnlyList<int>? pages, Regex pattern)
    {
        foreach (Field field in _document.Form.Fields)
        {
            if (field is ButtonField or SignatureField
                || (pages is not null && !(PdfForms.PageOf(field) is int page && pages.Contains(page)))
                || !PdfRedactionAudit.Matches(pattern, [PdfForms.State(field).Value]))
            {
                continue;
            }

            (PdfForms.RadioGroup(field) ?? field).Value = ClearedFieldValue(field);
        }
    }

    private void NoteBeside(int number, (int Lines, int Characters) taken)
    {
        if (taken.Characters > 0)
        {
            _beside.Add((number, taken.Lines, taken.Characters));
        }
    }

    private static (double, double, double, double) Key(Rectangle rectangle) =>
        (Math.Round(rectangle.LLX, 3), Math.Round(rectangle.LLY, 3), Math.Round(rectangle.URX, 3), Math.Round(rectangle.URY, 3));

    private static (double X, double Y) Middle(Rectangle rectangle) =>
        ((rectangle.LLX + rectangle.URX) / 2, (rectangle.LLY + rectangle.URY) / 2);

    private static bool Contains(Rectangle area, (double X, double Y) point) =>
        point.X > area.LLX && point.X < area.URX && point.Y > area.LLY && point.Y < area.URY;

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
