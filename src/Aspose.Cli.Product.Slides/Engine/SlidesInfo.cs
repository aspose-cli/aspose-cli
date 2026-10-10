using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Presentation structure, stable slide ids and the requested detail inventories.</summary>
internal static class SlidesInfo
{
    /// <summary>The most entries the <c>media</c> detail lists; <c>extract --what media</c> exports them all.</summary>
    internal const int MediaListLimit = 100;

    internal static PresentationInfoResult Run(SlidesSession session, PresentationInfoRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        Presentation presentation = loaded.Presentation;
        bool Details(string name) => request.Details?.Contains(name, StringComparer.Ordinal) == true;
        IComment[] comments = Comments(presentation);
        SlideInfo[] slides = presentation.Slides
            .Select((slide, index) => ProjectInfo(slide, index + 1, comments, request.IncludePreview))
            .ToArray();
        double width = presentation.SlideSize.Size.Width;
        double height = presentation.SlideSize.Size.Height;
        IReadOnlyList<PresentationMediaInfo>? media = Details(InfoDetails.Media) ? Media(presentation) : null;

        return new PresentationInfoResult
        {
            Source = Source(request.Input, loaded.FormatId),
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
            Sections = Details(InfoDetails.Sections)
                ? presentation.Sections.Select(section => new PresentationSectionInfo
                {
                    Name = section.Name,
                    SectionId = section.SectionId.ToString("D"),
                    StartSlide = FindSlideNumber(presentation, section.StartedFromSlide),
                }).ToArray()
                : null,
            Masters = Details(InfoDetails.Masters)
                ? presentation.Masters.Select(master => new PresentationMasterInfo
                {
                    Name = master.Name,
                    SlideCount = presentation.Slides.Count(slide =>
                        ReferenceEquals(slide.LayoutSlide?.MasterSlide, master)),
                }).OrderBy(static master => master.Name, StringComparer.Ordinal).ToArray()
                : null,
            Layouts = Details(InfoDetails.Layouts)
                ? presentation.LayoutSlides.Select(layout => new PresentationLayoutInfo
                {
                    Name = layout.Name,
                    Master = EmptyToNull(layout.MasterSlide?.Name),
                    SlideCount = presentation.Slides.Count(slide => ReferenceEquals(slide.LayoutSlide, layout)),
                }).OrderBy(static layout => layout.Name, StringComparer.Ordinal).ToArray()
                : null,
            Media = media?.Take(MediaListLimit).ToArray(),
            Notes = Details(InfoDetails.Notes)
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
            Comments = Details(InfoDetails.Comments)
                ? comments.Select(comment => new PresentationCommentInfo
                {
                    Slide = FindSlideNumber(presentation, comment.Slide),
                    Author = comment.Author.Name,
                    Text = comment.Text,
                }).OrderBy(static comment => comment.Slide).ThenBy(static comment => comment.Author, StringComparer.Ordinal).ToArray()
                : null,
            Fonts = Details(InfoDetails.Fonts)
                ? presentation.FontsManager.GetFonts().Select(static font => font.FontName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : null,
            Properties = Details(InfoDetails.Properties) ? Properties(presentation.DocumentProperties) : null,
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
}
