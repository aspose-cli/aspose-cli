using Aspose.Cli.Product.Slides.Engine.Mapping;
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
        TextSearch query = TextSearch.Create(request.Pattern, request.Regex, request.CaseSensitive);

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        var hits = new List<SlidesSearchHit>();
        bool truncated = false;
        foreach ((ISlide slide, int index) in loaded.Presentation.Slides.Select((slide, index) => (slide, index)))
        {
            if (request.Scope is PresentationSearchScopes.Shapes or PresentationSearchScopes.All)
            {
                foreach (IShape shape in slide.Shapes)
                {
                    string? text = ShapeText(shape);
                    if (text is null)
                    {
                        continue;
                    }

                    AddHits(text, "shapes", shape.OfficeInteropShapeId, shape.Name);
                    if (truncated)
                    {
                        break;
                    }
                }
            }

            if (!truncated && request.Scope is PresentationSearchScopes.Notes or PresentationSearchScopes.All)
            {
                string? notes = Notes(slide);
                if (notes is not null)
                {
                    AddHits(notes, "notes", null, null);
                }
            }

            if (truncated)
            {
                break;
            }

            void AddHits(string text, string scope, long? shapeId, string? shapeName)
            {
                foreach ((int start, int length) in query.Find(text))
                {
                    if (hits.Count == request.MaxHits)
                    {
                        truncated = true;
                        break;
                    }

                    hits.Add(new SlidesSearchHit
                    {
                        Slide = index + 1,
                        SlideId = slide.SlideId,
                        Scope = scope,
                        ShapeId = shapeId,
                        ShapeName = EmptyToNull(shapeName),
                        Text = MatchPreview(text, start, length),
                        Start = start,
                        Length = length,
                    });
                }
            }
        }

        return new SlidesSearchResult
        {
            Input = Source(filePath, loaded.FormatId),
            Pattern = request.Pattern,
            Scope = request.Scope,
            Hits = hits,
            Truncated = truncated,
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(state, loaded),
        };
    }

    private static string MatchPreview(string text, int start, int length) =>
        TextSearch.Preview(text, start, length, radius: 100);
}
