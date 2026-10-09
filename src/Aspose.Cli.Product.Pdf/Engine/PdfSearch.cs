using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Searches page text and returns page rectangles: <c>pdf query search</c>.</summary>
internal static class PdfSearch
{
    /// <summary>The most characters a search hit's context shows on each side of the match.</summary>
    private const int ContextRadius = 40;

    internal static PdfSearchResult Run(PdfSession session, PdfSearchRequest request)
    {
        string filePath = request.Input;
        TextSearch text = request.Query.Text;
        LicenseState state = session.Outputs.License;
        using LoadedPdf loaded = session.Loader.Open(filePath, request.Password);
        IReadOnlyList<int> pages = request.Pages?.Resolve(loaded.Document.Pages.Count)
            ?? Enumerable.Range(1, loaded.Document.Pages.Count).ToArray();
        SearchHits<PdfSearchHit> hits = request.Query.Collect<PdfSearchHit>();
        // The page text is searched with the expression the engine matched.
        Regex matching = TextPattern(text.Pattern, text.Expression is not null, text.CaseSensitive);
        foreach (int number in pages)
        {
            Page page = loaded.Document.Pages[number];
            int occurrence = 0;
            TextFragmentCollection fragments = MatchText(page, text.Pattern, text.Expression is not null, text.CaseSensitive,
                static reason => CliErrors.OptionInvalid("--pattern", reason, "Use a pattern that matches at least one character."));
            // The engine's hits carry no surrounding text; the page's plain text gives it when
            // it holds the same number of matches, which then pair up in reading order. A pair
            // whose texts differ gets none. The text is extracted only for a hit that is kept.
            string? pageText = null;
            IReadOnlyList<Match> found = [];
            string? Context(int index, string snippet)
            {
                if (pageText is null)
                {
                    pageText = ExtractText(page, PdfReadModes.Plain);
                    found = matching.Matches(pageText);
                }

                if (found.Count != fragments.Count)
                {
                    return null;
                }

                (int start, int length) = (found[index].Index, found[index].Length);
                return string.Equals(pageText.Substring(start, length), snippet, StringComparison.OrdinalIgnoreCase)
                    ? TextSearch.Preview(pageText, start, length, ContextRadius).ReplaceLineEndings(" ")
                    : null;
            }

            foreach (TextFragment fragment in fragments)
            {
                int current = ++occurrence;
                if (!hits.Offer(() => new PdfSearchHit
                {
                    Page = number,
                    Snippet = fragment.Text,
                    Context = Context(current - 1, fragment.Text),
                    Rect = ToContractRect(page, fragment.Rectangle),
                    Occurrence = current,
                }))
                {
                    break;
                }
            }

            if (hits.Truncated)
            {
                break;
            }
        }

        return new PdfSearchResult
        {
            Source = PdfInfoProjection.Source(filePath),
            Pattern = text.Pattern,
            Hits = hits.Hits,
            Window = hits.Window(),
            License = EnvelopeParts.License(state),
        };
    }
}
