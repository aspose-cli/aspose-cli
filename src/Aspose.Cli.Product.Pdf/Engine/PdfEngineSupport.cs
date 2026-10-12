using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility.Output;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Shared PDF document selection, text extraction, and output projection.</summary>
internal static class PdfEngineSupport
{
    internal static Document Select(Document source, IReadOnlyList<int> pages)
    {
        var selected = new Document();
        try
        {
            foreach (int pageNumber in pages)
            {
                selected.Pages.Add(source.Pages[pageNumber]);
            }

            return selected;
        }
        catch
        {
            selected.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Copies the document-level attachments of a source into a document assembled from copied
    /// pages, which carries none of them, and returns the names it left out because an earlier
    /// source left an attachment of that name with other contents there. An attachment whose
    /// name and contents the document already holds is not copied again, and the attachments of
    /// one source are all copied, even two that show the same name.
    /// </summary>
    internal static IReadOnlyList<string> CopyAttachments(Document source, Document target)
    {
        // A source without attachments leaves the document as it is.
        if (source.EmbeddedFiles.Count == 0)
        {
            return [];
        }

        ILookup<string, FileSpecification> earlier = target.EmbeddedFiles
            .ToLookup(static file => AttachmentName(file) ?? string.Empty, StringComparer.Ordinal);
        // The engine throws for the keys of a document without attachments.
        var keys = new HashSet<string>(
            target.EmbeddedFiles.Count == 0 ? [] : target.EmbeddedFiles.Keys, StringComparer.Ordinal);
        var skipped = new List<string>();
        foreach (string key in source.EmbeddedFiles.Keys)
        {
            FileSpecification file = source.EmbeddedFiles[key];
            string name = AttachmentName(file) ?? string.Empty;
            if (earlier[name].Any(held => SameContents(held, file)))
            {
                continue;
            }

            if (name.Length > 0 && earlier.Contains(name))
            {
                skipped.Add(name);
                continue;
            }

            // The name tree key is not shown to readers; a taken one gets a numbered suffix.
            string free = key;
            for (int suffix = 2; !keys.Add(free); suffix++)
            {
                free = string.Create(CultureInfo.InvariantCulture, $"{key}-{suffix}");
            }

            target.EmbeddedFiles.Add(free, file);
        }

        return skipped;
    }

    private static bool SameContents(FileSpecification first, FileSpecification second)
    {
        Stream? left = first.Contents;
        Stream? right = second.Contents;
        if (ReferenceEquals(left, right))
        {
            return left is not null;
        }

        if (left is not { CanSeek: true } || right is not { CanSeek: true } || left.Length != right.Length)
        {
            return false;
        }

        byte[] leftBuffer = new byte[81920];
        byte[] rightBuffer = new byte[81920];
        left.Position = 0;
        right.Position = 0;
        try
        {
            while (true)
            {
                int read = left.ReadAtLeast(leftBuffer, leftBuffer.Length, throwOnEndOfStream: false);
                if (right.ReadAtLeast(rightBuffer.AsSpan(0, read), read, throwOnEndOfStream: false) != read
                    || !leftBuffer.AsSpan(0, read).SequenceEqual(rightBuffer.AsSpan(0, read)))
                {
                    return false;
                }

                if (read < leftBuffer.Length)
                {
                    return true;
                }
            }
        }
        finally
        {
            left.Position = 0;
            right.Position = 0;
        }
    }

    /// <summary>
    /// The warning that a document assembled from copied pages keeps the attachments of a
    /// portfolio but not the portfolio itself, or null when the source is not one.
    /// <paramref name="input"/> names the source among several inputs.
    /// </summary>
    internal static Warning? PortfolioNotKept(Document source, string? input) =>
        source.Collection is null
            ? null
            : new Warning(
                WarningCodes.LossyConversion,
                $"{(input is null ? "The input" : $"Input '{input}'")} is a PDF portfolio: its attachments were copied as plain attachments, but the portfolio presentation, such as its file list layout, columns and sort order, was not kept.")
            {
                Location = input is null ? "portfolio" : $"{input} portfolio",
                Hint = "List the attachments with 'pdf inspect --detail attachments'; rebuild the portfolio in a PDF editor if readers must open it as a portfolio.",
                AffectsCompleteness = true,
            };

    /// <summary>The name a reader shows for an attachment, or null when it has none.</summary>
    internal static string? AttachmentName(FileSpecification file) =>
        (file.UnicodeName ?? file.Name) is { Length: > 0 } name ? name : null;

    internal static string[] AttachmentNames(Document document) =>
        [.. document.EmbeddedFiles.Select(AttachmentName).OfType<string>()];

    /// <summary>
    /// Copies an outline into a document assembled from copied pages, pointing each bookmark at
    /// its page there with a Fit destination, and returns how many working bookmarks lost
    /// fidelity: a location or zoom other than Fit, or a named destination the document does not
    /// carry. <paramref name="pageOf"/> maps a source page to its page in the document, or to 0
    /// when the document does not hold it; such a bookmark is left out and its children take
    /// its place.
    /// </summary>
    internal static int CopyOutline(
        Document source,
        IEnumerable<OutlineItemCollection> items,
        ICollection<OutlineItemCollection> target,
        Document document,
        Func<int, int> pageOf)
    {
        int degraded = 0;
        foreach (OutlineItemCollection item in items)
        {
            int page = PdfNavigationCensus.DestinationPage(item);
            int copiedPage = page > 0 ? pageOf(page) : 0;
            if (page > 0 && copiedPage == 0)
            {
                degraded += CopyOutline(source, item, target, document, pageOf);
                continue;
            }

            var copied = new OutlineItemCollection(document.Outlines)
            {
                Title = item.Title,
                Bold = item.Bold,
                Italic = item.Italic,
                Color = item.Color,
            };
            if (copiedPage > 0)
            {
                copied.Destination = new FitExplicitDestination(document.Pages[copiedPage]);
            }

            IAppointment? destination = PdfNavigationCensus.Target(item.Destination, item.Action);
            if (PdfNavigationCensus.Resolves(source, destination)
                && (page == 0 || destination is not FitExplicitDestination))
            {
                degraded++;
            }

            degraded += CopyOutline(source, item, copied, document, pageOf);
            target.Add(copied);
        }

        return degraded;
    }

    internal static Page PageAt(Document document, int page) =>
        page > 0 && page <= document.Pages.Count
            ? document.Pages[page]
            : throw CliErrors.NotFoundAt(
                ErrorCodes.PageNotFound,
                "page",
                page.ToString(CultureInfo.InvariantCulture),
                document.Pages.Count);

    internal static void EnsureAcroForm(Document document)
    {
        if (document.Form.HasXfa)
        {
            throw new CliException(
                PdfDiagnostics.FormXfaUnsupported,
                "XFA forms are read-only in the current PDF command surface.",
                hint: "Convert the XFA form to AcroForm before filling, flattening or exporting it.");
        }
    }

    internal static string ExtractText(Page page, string content)
    {
        TextExtractionOptions.TextFormattingMode formatting = content == PdfReadContents.Layout
            ? TextExtractionOptions.TextFormattingMode.Pure
            : TextExtractionOptions.TextFormattingMode.Flatten;
        var absorber = new TextAbsorber
        {
            ExtractionOptions = new TextExtractionOptions(formatting),
        };
        page.Accept(absorber);
        return absorber.Text ?? string.Empty;
    }

    /// <summary>
    /// The plain text of the pages, each page separated from the next by a form feed and a
    /// line break, so page boundaries survive even when a page has no text.
    /// </summary>
    internal static string DocumentText(Document document, IReadOnlyList<int> pages) =>
        string.Join(
            "\f" + Environment.NewLine,
            pages.Select(pageNumber => ExtractText(document.Pages[pageNumber], PdfReadContents.Text)));

    /// <summary>
    /// Whether one image is scaled to the page, as a scan is: it spans at least three quarters of
    /// the page's width or height and covers at least a quarter of its area. A scan placed inside
    /// margins with its proportions kept, such as a square scan on an A4 page, covers far less
    /// than the whole page but still spans it in one dimension.
    /// </summary>
    internal static bool IsImageDominated(Page page)
    {
        var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
        page.Accept(absorber);
        double pageWidth = Math.Max(1d, page.Rect.Width);
        double pageHeight = Math.Max(1d, page.Rect.Height);
        return absorber.ImagePlacements.Any(placement =>
        {
            Rectangle placed = placement.Rectangle;
            return (placed.Width >= pageWidth * 0.75d || placed.Height >= pageHeight * 0.75d)
                && placed.Width * placed.Height >= pageWidth * pageHeight * 0.25d;
        });
    }

    internal static Rectangle ToPdfRect(Page page, PdfRectInput rect)
    {
        Rectangle box = page.GetPageRect(considerRotation: true);
        if (rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > box.Width || rect.Y + rect.Height > box.Height)
        {
            throw new InvalidOperationException("Rectangle lies outside the page bounds.");
        }
        return page.RotationMatrix.Reverse().Transform(new Rectangle(
            box.LLX + rect.X, box.URY - rect.Y - rect.Height,
            box.LLX + rect.X + rect.Width, box.URY - rect.Y));
    }

    internal static PdfRect ToContractRect(Page page, Rectangle rect)
    {
        Rectangle box = page.GetPageRect(considerRotation: true);
        Rectangle visible = page.RotationMatrix.Transform(rect);
        return new PdfRect
        {
            X = visible.LLX - box.LLX,
            Y = box.URY - visible.URY,
            Width = visible.Width,
            Height = visible.Height,
        };
    }

    /// <summary>
    /// The expression that search, redact_text and its verification match. Literal text also
    /// matches with up to two spaces, never a line break, wherever an East Asian character meets
    /// a character that is not East Asian, such as a digit or Latin letter: the engine reads the gap that automatic spacing leaves there, such as
    /// Word's between Chinese and digits, as a space (known issue PDF-TEXT-GAP-SPACE in
    /// KNOWN-ISSUES.md).
    /// </summary>
    internal static Regex TextPattern(string pattern, bool regex, bool caseSensitive)
    {
        if (regex)
        {
            return SafeRegex.Create(pattern, caseSensitive);
        }

        var expression = new StringBuilder();
        Rune? previous = null;
        foreach (Rune rune in pattern.EnumerateRunes())
        {
            if (previous is { } before && !Rune.IsWhiteSpace(before) && !Rune.IsWhiteSpace(rune)
                && IsEastAsian(before) != IsEastAsian(rune))
            {
                expression.Append(@"[^\S\r\n]{0,2}");
            }

            expression.Append(Regex.Escape(rune.ToString()));
            previous = rune;
        }

        return SafeRegex.Create(expression.ToString(), caseSensitive);

        // East Asian Wide and Fullwidth characters: ideographs, kana, Hangul and fullwidth forms.
        static bool IsEastAsian(Rune rune) => TextWidth.Of(rune) == 2;
    }

    internal static TextFragmentCollection MatchText(
        Page page, string pattern, bool regex, bool caseSensitive, Func<string, Exception> invalidPattern)
    {
        Regex expression = TextPattern(pattern, regex, caseSensitive);
        try
        {
            // The SDK overflows on contextual zero-width matches. Reject them before
            // handing it the original expression, whose timeout and context it preserves.
            if (regex && expression.Matches(ExtractText(page, PdfReadContents.Text))
                .Any(static match => match.Length == 0))
            {
                throw invalidPattern("the regular expression must not match an empty string");
            }
            var absorber = new TextFragmentAbsorber(expression, new TextSearchOptions(true));
            page.Accept(absorber);
            return absorber.TextFragments;
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw CliErrors.RegexTimeout(exception);
        }
    }
}
