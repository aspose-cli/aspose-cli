using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Owns presentation info and bounded content-window reads.</summary>
internal sealed class SlidesReadService
{
    /// <summary>The most entries the <c>media</c> detail lists; <c>extract --what media</c> exports them all.</summary>
    internal const int MediaListLimit = 100;

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
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password?.Reveal());
        Presentation presentation = loaded.Presentation;
        bool Details(string name) => request.Details?.Contains(name, StringComparer.Ordinal) == true;
        IComment[] comments = Comments(presentation);
        SlideInfo[] slides = presentation.Slides
            .Select((slide, index) => ProjectInfo(slide, index + 1, comments, request.IncludePreview))
            .ToArray();
        double width = presentation.SlideSize.Size.Width;
        double height = presentation.SlideSize.Size.Height;
        IReadOnlyList<PresentationMediaInfo>? media = Details("media") ? Media(presentation) : null;

        return new PresentationInfoResult
        {
            Source = Source(filePath, loaded.FormatId),
            Presentation = new PresentationSummary
            {
                SlideCount = presentation.Slides.Count,
                WidthPoints = width,
                HeightPoints = height,
                Orientation = width > height ? "landscape" : height > width ? "portrait" : "square",
                MasterCount = presentation.Masters.Count,
                LayoutCount = presentation.LayoutSlides.Count,
                SectionCount = presentation.Sections.Count,
                CommentCount = comments.Length,
                MediaCount = presentation.Images.Count + presentation.Audios.Count + presentation.Videos.Count,
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
                    SlideCount = presentation.Slides.Count(slide =>
                        ReferenceEquals(slide.LayoutSlide?.MasterSlide, master)),
                }).OrderBy(static master => master.Name, StringComparer.Ordinal).ToArray()
                : null,
            Layouts = Details("layouts")
                ? presentation.LayoutSlides.Select(layout => new PresentationLayoutInfo
                {
                    Name = layout.Name,
                    Master = EmptyToNull(layout.MasterSlide?.Name),
                    SlideCount = presentation.Slides.Count(slide => ReferenceEquals(slide.LayoutSlide, layout)),
                }).OrderBy(static layout => layout.Name, StringComparer.Ordinal).ToArray()
                : null,
            Media = media?.Take(MediaListLimit).ToArray(),
            Notes = Details("notes")
                ? presentation.Slides.Select((slide, index) =>
                {
                    string? notes = Notes(slide);
                    return new PresentationNotesInfo
                    {
                        Slide = index + 1,
                        Present = notes is not null,
                        CharacterCount = notes?.Length ?? 0,
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
            Warnings = EnvelopeParts.CombineWarnings(
                InputWarnings(state, loaded, textRead: true),
                media is { Count: > MediaListLimit }
                    ?
                    [
                        EnvelopeParts.ListTruncated(
                            "media",
                            MediaListLimit,
                            media.Count,
                            "Run 'aspose-cli slides extract <presentation> --what media --out <directory>' to export every media item."),
                    ]
                    : null),
        };
    }

    internal PresentationReadResult Read(string filePath, PresentationReadRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password?.Reveal());
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
                state == LicenseState.Evaluation,
                ref remaining);
            slides.Add(projected);
            if (remaining <= 0)
            {
                break;
            }
        }

        // Without --slides the selection is every slide, of which a read returns at most ten.
        int selected = request.Slides is null ? presentation.Slides.Count : requested.Count;
        int last = slides.Count == 0 ? 0 : slides[^1].Slide;
        bool selectionTruncated = slides.Count < requested.Count;
        bool defaultWindowTruncated = request.Slides is null && last < presentation.Slides.Count;
        bool truncated = selectionTruncated || defaultWindowTruncated || slides.Any(static slide => slide.ContentTruncated);
        return new PresentationReadResult
        {
            Source = Source(filePath, loaded.FormatId),
            Scope = request.Scope,
            SlideCount = presentation.Slides.Count,
            Slides = slides,
            Window = new ResultWindow
            {
                Unit = "slide",
                Returned = slides.Count,
                Total = selected,
                Truncated = truncated,
            },
            License = EnvelopeParts.License(state),
            Warnings = InputWarnings(state, loaded, textRead: true),
        };
    }
}
