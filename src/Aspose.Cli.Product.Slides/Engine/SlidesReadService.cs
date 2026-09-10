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
using Aspose.Cli.Sdk.Preview;
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
        SlidesFontCatalog.EnsureInitialized();
    }

    /// <inheritdoc />
    internal PresentationInfoResult GetInfo(string filePath, PresentationInfoRequest request) =>
        SlidesErrorTranslator.Execute("info", () => GetInfoCore(filePath, request));

    /// <inheritdoc />
    internal PresentationReadResult Read(string filePath, PresentationReadRequest request) =>
        SlidesErrorTranslator.Execute("read", () => ReadCore(filePath, request));

    private PresentationInfoResult GetInfoCore(string filePath, PresentationInfoRequest request)
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

    private PresentationReadResult ReadCore(string filePath, PresentationReadRequest request)
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
        bool truncated = selectionTruncated || defaultWindowTruncated;
        string? nextSlides = selectionTruncated
            ? string.Join(",", requested.Skip(slides.Count))
            : defaultWindowTruncated ? $"{last + 1}-" : null;
        string? next = nextSlides is not null
            ? $"aspose-cli slides query slides \"{filePath}\" --slides {nextSlides} --scope {request.Scope}"
                + (request.IncludeNotes ? " --notes" : string.Empty)
                + $" --max-chars {request.MaxCharacters} --output json"
            : null;
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
            Next = next,
            License = EnvelopeParts.License(state),
            Warnings = EvaluationInputWarnings(state, presentation),
        };
    }

}

