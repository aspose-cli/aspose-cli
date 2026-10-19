using System.Globalization;
using Aspose.Pdf;
using Aspose.Pdf.Annotations;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Assembles a document from pages copied out of others, as merge and split do: the pages
/// themselves and what the copy does not carry along, the page labels, document information,
/// attachments and outline.
/// </summary>
internal static class PdfAssembly
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
    /// Labels the pages of a document assembled from copied pages, from its page
    /// <paramref name="offset"/> + 1 on, as <paramref name="pages"/> were labelled in the source.
    /// A label range starts at the first of them and wherever a page does not follow the previous
    /// one in the same source range; a source page before the first range is numbered as the
    /// reader shows it. Returns false, and labels nothing, for a source without labels.
    /// </summary>
    internal static bool CopyPageLabels(Document source, Document target, IReadOnlyList<int> pages, int offset)
    {
        int[] starts = [.. source.PageLabels.GetPages().Order()];
        if (starts.Length == 0)
        {
            return false;
        }

        int previous = -2;
        int previousStart = -1;
        for (int index = 0; index < pages.Count; index++)
        {
            int page = pages[index] - 1;
            int start = starts.LastOrDefault(candidate => candidate <= page, -1);
            if (start != previousStart || page != previous + 1)
            {
                PageLabel label = start < 0
                    ? new PageLabel { NumberingStyle = NumberingStyle.NumeralsArabic }
                    : source.PageLabels.GetLabel(start);
                target.PageLabels.UpdateLabel(offset + index, new PageLabel
                {
                    Prefix = label.Prefix,
                    NumberingStyle = label.NumberingStyle,
                    StartingValue = label.StartingValue + page - Math.Max(start, 0),
                });
            }

            previous = page;
            previousStart = start;
        }

        return true;
    }

    /// <summary>
    /// Copies the document information entries of a source into a document assembled from
    /// copied pages, which carries none of them, except those the engine writes on every save.
    /// </summary>
    internal static void CopyInformation(Document source, Document target)
    {
        foreach (string key in source.Info.Keys)
        {
            if (!Editing.PdfSnapshots.SaveStamped.Contains(PdfPropertyNames.Standard(key)?.Name ?? key))
            {
                target.Info[key] = source.Info[key];
            }
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

            // PDF-ATTACHMENT-COPY-TYPE: adding a specification of another document loses its media
            // type, so it is read before the copy and written back to the copy.
            string? mediaType = file.MIMEType;
            target.EmbeddedFiles.Add(free, file);
            if (!string.IsNullOrEmpty(mediaType))
            {
                target.EmbeddedFiles[free].MIMEType = mediaType;
            }
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
}
