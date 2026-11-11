using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using static Aspose.Cli.Product.Slides.Engine.SlidesEvaluationMarks;
using static Aspose.Cli.Product.Slides.Engine.SlidesResultParts;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Bounded shape and speaker-notes text search.</summary>
internal static class SlidesSearch
{
    /// <summary>The notice evaluation mode puts after the first characters of a text it cuts short.</summary>
    private static readonly Regex EvaluationNotice = new(
        @"\.\.\.\s*text has been " + Regex.Escape(EvaluationTruncationMarker) + @"\.?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static SlidesSearchResult Run(SlidesSession session, SlidesSearchRequest request)
    {
        TextSearch text = request.Query.Text;
        string scope = request.Query.Scope ?? SlidesSearchScopes.All;

        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        session.Outputs.ReadInput(loaded.Presentation);
        SearchHits<SlidesSearchHit> hits = request.Query.Collect<SlidesSearchHit>();
        foreach (SlidesTextUnit unit in SlidesTextUnits.Of(loaded.Presentation, scope))
        {
            if (unit.Text is { } source && !Offer(unit, source))
            {
                break;
            }
        }

        // Offers every match in one unit's text; false once the hit window is full.
        bool Offer(SlidesTextUnit unit, string source)
        {
            IReadOnlyList<(int Start, int Length)> notices = EvaluationNotices(source);
            foreach ((int start, int length) in text.Find(source))
            {
                if (notices.Any(notice => start < notice.Start + notice.Length && notice.Start < start + length))
                {
                    continue;
                }

                if (!hits.Offer(() => new SlidesSearchHit
                {
                    Slide = unit.Slide,
                    SlideId = unit.Owner.SlideId,
                    Scope = unit.Scope,
                    ShapeId = unit.Shape?.OfficeInteropShapeId,
                    ShapeName = EmptyToNull(unit.Shape?.Name),
                    Text = source.Substring(start, length),
                    Context = TextSearch.Context(source, start, length),
                }))
                {
                    return false;
                }
            }

            return true;
        }

        return new SlidesSearchResult
        {
            Input = Source(request.Input, loaded),
            Pattern = text.Pattern,
            Scope = scope,
            Hits = hits.Hits,
            Window = hits.Window(),
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(loaded),
        };
    }

    /// <summary>
    /// Where evaluation mode put its truncation notice in a text it cut short, such as
    /// "First... text has been truncated due to evaluation version limitation.": the notice is
    /// the engine's, not the presentation's, so a match in it is no hit.
    /// </summary>
    private static IReadOnlyList<(int Start, int Length)> EvaluationNotices(string text) =>
        CutByEvaluation(text)
            ? [.. EvaluationNotice.Matches(text).Select(static notice => (notice.Index, notice.Length))]
            : [];
}
