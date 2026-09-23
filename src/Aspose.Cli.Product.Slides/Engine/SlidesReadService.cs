using Aspose.Cli.Sdk.Text;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.Export;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Owns structural and content projections.</summary>
internal sealed class SlidesReadService
{
    private readonly ILicenseGate _licenseGate;
    private readonly SlidesPresentationLoader _loader;

    internal SlidesReadService(
        ILicenseGate licenseGate,
        SlidesPresentationLoader loader)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = loader;
    }

    internal PresentationInfoResult GetInfo(string filePath, PresentationInfoRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        Presentation presentation = loaded.Presentation;
        bool Details(string name) => request.Details?.Contains(name, StringComparer.Ordinal) == true;
        IComment[] comments = Comments(presentation);
        SlideInfo[] slides = presentation.Slides
            .Select((slide, index) => ProjectInfo(slide, index + 1, comments, request.IncludePreview))
            .ToArray();
        double width = presentation.SlideSize.Size.Width;
        double height = presentation.SlideSize.Size.Height;

        return new PresentationInfoResult
        {
            Source = Source(filePath, loaded.FormatId),
            Presentation = new PresentationSummary
            {
                Slides = presentation.Slides.Count,
                WidthPoints = width,
                HeightPoints = height,
                Orientation = width > height ? "landscape" : height > width ? "portrait" : "square",
                Masters = presentation.Masters.Count,
                Layouts = presentation.LayoutSlides.Count,
                Sections = presentation.Sections.Count,
                Comments = comments.Length,
                Media = presentation.Images.Count + presentation.Audios.Count + presentation.Videos.Count,
                HasMacros = presentation.VbaProject is not null,
            },
            Slides = slides,
            Sections = Details("sections")
                ? presentation.Sections.Select(section => new PresentationSectionInfo
                {
                    Name = section.Name,
                    SectionId = section.SectionId.ToString("D"),
                    StartSlide = FindSlideNumber(presentation, section.StartedFromSlide),
                }).ToArray()
                : null,
            Masters = Details("masters")
                ? presentation.Masters.Select(master => new PresentationMasterInfo
                {
                    Name = master.Name,
                    Slides = presentation.Slides.Count(slide =>
                        ReferenceEquals(slide.LayoutSlide?.MasterSlide, master)),
                }).OrderBy(static master => master.Name, StringComparer.Ordinal).ToArray()
                : null,
            Layouts = Details("layouts")
                ? presentation.LayoutSlides.Select(layout => new PresentationLayoutInfo
                {
                    Name = layout.Name,
                    Master = EmptyToNull(layout.MasterSlide?.Name),
                    Slides = presentation.Slides.Count(slide => ReferenceEquals(slide.LayoutSlide, layout)),
                }).OrderBy(static layout => layout.Name, StringComparer.Ordinal).ToArray()
                : null,
            Media = Details("media") ? Media(presentation) : null,
            Notes = Details("notes")
                ? presentation.Slides.Select((slide, index) =>
                {
                    string? notes = Notes(slide);
                    return new PresentationNotesInfo
                    {
                        Slide = index + 1,
                        Present = notes is not null,
                        Characters = notes?.Length ?? 0,
                    };
                }).ToArray()
                : null,
            Comments = Details("comments")
                ? comments.Select(comment => new PresentationCommentInfo
                {
                    Slide = FindSlideNumber(presentation, comment.Slide),
                    Author = comment.Author.Name,
                    Text = comment.Text,
                }).OrderBy(static comment => comment.Slide).ThenBy(static comment => comment.Author, StringComparer.Ordinal).ToArray()
                : null,
            Fonts = Details("fonts")
                ? presentation.FontsManager.GetFonts().Select(static font => font.FontName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : null,
            Properties = Details("properties") ? Properties(presentation.DocumentProperties) : null,
            License = EnvelopeParts.License(state),
            Warnings = EvaluationInputWarnings(state, presentation),
        };
    }

    internal PresentationReadResult Read(string filePath, PresentationReadRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        Presentation presentation = loaded.Presentation;
        IReadOnlyList<int> requested = request.Slides is null
            ? Enumerable.Range(1, Math.Min(10, presentation.Slides.Count)).ToArray()
            : ResolveSlideRange(request.Slides, presentation.Slides.Count);
        IComment[] comments = Comments(presentation);
        int remaining = request.MaxCharacters;
        var slides = new List<SlideData>();
        foreach (int number in requested)
        {
            SlideData projected = ProjectSlide(
                presentation.Slides[number - 1],
                number,
                request.Scope,
                request.IncludeNotes,
                comments,
                ref remaining);
            slides.Add(projected);
            if (remaining <= 0)
            {
                break;
            }
        }

        int last = slides.Count == 0 ? 0 : slides[^1].Number;
        bool selectionTruncated = slides.Count < requested.Count;
        bool defaultWindowTruncated = request.Slides is null && last < presentation.Slides.Count;
        bool truncated = selectionTruncated || defaultWindowTruncated || slides.Any(static slide => slide.ContentTruncated);
        return new PresentationReadResult
        {
            Source = Source(filePath, loaded.FormatId),
            Scope = request.Scope,
            Window = new SlideWindow
            {
                Slides = slides.Count == 0 ? string.Empty : string.Join(",", slides.Select(static slide => slide.Number)),
                Of = presentation.Slides.Count,
                Truncated = truncated,
            },
            Slides = slides,
            License = EnvelopeParts.License(state),
            Warnings = EvaluationInputWarnings(state, presentation),
        };
    }

    public SlidesSearchResult Search(string filePath, PresentationSearchRequest request)
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
            Warnings = EvaluationInputWarnings(state, loaded.Presentation),
        };
    }

    private static string MatchPreview(string text, int start, int length) =>
        TextSearch.Preview(text, start, length, radius: 100);


}
