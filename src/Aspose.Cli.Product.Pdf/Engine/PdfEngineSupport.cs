using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Text;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Product.Pdf.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
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

    internal static string ExtractText(Page page, string mode)
    {
        TextExtractionOptions.TextFormattingMode formatting = mode == PdfReadModes.Layout
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
            pages.Select(pageNumber => ExtractText(document.Pages[pageNumber], PdfReadModes.Plain)));

    internal static bool IsImageDominated(Page page)
    {
        var absorber = new ImagePlacementAbsorber { IsReadOnlyMode = true };
        page.Accept(absorber);
        double pageArea = Math.Max(1d, page.Rect.Width * page.Rect.Height);
        return absorber.ImagePlacements.Any(placement =>
            placement.Rectangle.Width * placement.Rectangle.Height >= pageArea * 0.8d);
    }

    internal static string PageRangeText(IEnumerable<int> values) => string.Join(",", values);

    internal static OutputInfo BuildOutput(string path, string format, long size) => new()
    {
        Path = Path.GetFullPath(path),
        Format = format,
        SizeBytes = size,
    };

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

    internal static TextFragmentCollection MatchText(
        Page page, string pattern, bool regex, bool caseSensitive, Func<string, Exception> invalidPattern)
    {
        Regex expression = SafeRegex.Create(regex ? pattern : Regex.Escape(pattern), caseSensitive);
        try
        {
            // The SDK overflows on contextual zero-width matches. Reject them before
            // handing it the original expression, whose timeout and context it preserves.
            if (regex && expression.Matches(ExtractText(page, PdfReadModes.Plain))
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
            throw new CliException(ErrorCodes.OperationTimeout,
                "The PDF regular expression exceeded its one-second execution budget.",
                hint: "Simplify the expression or search a narrower page range.", innerException: exception);
        }
    }

    internal static (double Width, double Height) PageDimensions(string id) => id.ToUpperInvariant() switch
    {
        "A3" => (841.89, 1190.55),
        "A4" => (595.28, 841.89),
        "LETTER" => (612, 792),
        "LEGAL" => (612, 1008),
        _ => throw CliErrors.OptionInvalid("--page-size", $"unknown size '{id}'", "Use A3, A4, Letter or Legal."),
    };

    internal static void EnsurePdfOutput(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw CliErrors.OptionInvalid(
                "--out",
                $"PDF output must use the .pdf extension: '{path}'",
                "Choose a path ending in .pdf.");
        }
    }
}
