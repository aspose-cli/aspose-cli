using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Owns bounded shape and speaker-notes text search.</summary>
internal sealed class SlidesInspectionService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SlidesPresentationLoader _loader;

    internal SlidesInspectionService(
        ILicenseGate licenseGate,
        SlidesPresentationLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader;
    }

    internal SlidesSearchResult Search(string filePath, PresentationSearchRequest request)
    {
        TextSearch text = request.Query.Text;
        string scope = request.Query.Scope ?? PresentationSearchScopes.All;

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password?.Reveal());
        SearchHits<SlidesSearchHit> hits = request.Query.Collect<SlidesSearchHit>();
        foreach ((ISlide slide, int index) in loaded.Presentation.Slides.Select((slide, index) => (slide, index)))
        {
            if (scope is PresentationSearchScopes.Shapes or PresentationSearchScopes.All)
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
                && scope is PresentationSearchScopes.Notes or PresentationSearchScopes.All
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
                foreach ((int start, int length) in text.Find(source))
                {
                    if (!hits.Offer(() => new SlidesSearchHit
                    {
                        Slide = index + 1,
                        SlideId = slide.SlideId,
                        Scope = hitScope,
                        ShapeId = shapeId,
                        ShapeName = EmptyToNull(shapeName),
                        Text = MatchPreview(source, start, length),
                        Start = start,
                        Length = length,
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
            Source = Source(filePath, loaded.FormatId),
            Pattern = text.Pattern,
            Scope = scope,
            Hits = hits.Hits,
            Window = hits.Window(),
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(state, loaded, textRead: true),
        };
    }

    private static string MatchPreview(string text, int start, int length) =>
        TextSearch.Preview(text, start, length, radius: 100);
}
