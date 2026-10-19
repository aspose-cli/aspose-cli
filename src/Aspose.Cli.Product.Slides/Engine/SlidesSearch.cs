using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
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
        foreach ((ISlide slide, int index) in loaded.Presentation.Slides.Select((slide, index) => (slide, index)))
        {
            if (scope is SlidesSearchScopes.Shapes or SlidesSearchScopes.All)
            {
                foreach (IShape shape in slide.Shapes)
                {
                    if (ShapeText(shape) is { } shapeText
                        && !Offer(shapeText, "shapes", shape.OfficeInteropShapeId, shape.Name))
                    {
                        break;
                    }
                }
            }

            if (!hits.Truncated
                && scope is SlidesSearchScopes.Notes or SlidesSearchScopes.All
                && Notes(slide) is { } notes)
            {
                Offer(notes, "notes", null, null);
            }

            if (hits.Truncated)
            {
                break;
            }

            // Offers every match in one text; false once the hit window is full.
            bool Offer(string source, string hitScope, long? shapeId, string? shapeName)
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
                        Slide = index + 1,
                        SlideId = slide.SlideId,
                        Scope = hitScope,
                        ShapeId = shapeId,
                        ShapeName = EmptyToNull(shapeName),
                        Text = source.Substring(start, length),
                        Context = TextSearch.Context(source, start, length),
                    }))
                    {
                        return false;
                    }
                }

                return true;
            }
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
