using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesStateProjection;
using static Aspose.Cli.Product.Slides.Engine.SlidesResultParts;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Presentation structure, stable slide ids and the requested detail inventories.</summary>
internal static class SlidesInfo
{
    /// <summary>The most entries the <c>media</c> detail lists; <c>extract --what media</c> exports them all.</summary>
    internal const int MediaListLimit = 100;

    internal static SlidesInspectResult Run(SlidesSession session, SlidesInspectRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        session.Outputs.ReadInput(loaded.Presentation);
        Presentation presentation = loaded.Presentation;
        bool Details(string name) => request.Details?.Contains(name, StringComparer.Ordinal) == true;
        IComment[] comments = Comments(presentation);
        SlidesSlideInfo[] slides = presentation.Slides
            .Select((slide, index) => ProjectInfo(slide, index + 1, comments, request.IncludePreview))
            .ToArray();
        SlidesSlideSizeInfo size = SlideSize(presentation);
        IReadOnlyList<SlidesMediaInfo>? media = Details(SlidesInspectDetails.Media) ? Media(presentation) : null;

        return new SlidesInspectResult
        {
            Input = Source(request.Input, loaded),
            Presentation = new SlidesPresentationSummary
            {
                SlideCount = presentation.Slides.Count,
                SlideSize = size,
                Orientation = size.Width > size.Height ? "landscape" : size.Height > size.Width ? "portrait" : "square",
                MasterCount = presentation.Masters.Count,
                LayoutCount = presentation.LayoutSlides.Count,
                SectionCount = presentation.Sections.Count,
                CommentCount = comments.Length,
                MediaCount = presentation.Images.Count + presentation.Audios.Count + presentation.Videos.Count,
                HasMacros = presentation.VbaProject is not null,
            },
            Slides = slides,
            Sections = Details(SlidesInspectDetails.Sections)
                ? presentation.Sections.Select(section => new SlidesSectionInfo
                {
                    Name = section.Name,
                    SectionId = section.SectionId.ToString("D"),
                    StartSlide = FindSlideNumber(presentation, section.StartedFromSlide) is > 0 and var first ? first : null,
                }).ToArray()
                : null,
            Masters = Details(SlidesInspectDetails.Masters)
                ? presentation.Masters.Select(master => new SlidesMasterInfo
                {
                    Name = master.Name,
                    SlideCount = presentation.Slides.Count(slide =>
                        ReferenceEquals(slide.LayoutSlide?.MasterSlide, master)),
                }).OrderBy(static master => master.Name, StringComparer.Ordinal).ToArray()
                : null,
            Layouts = Details(SlidesInspectDetails.Layouts)
                ? presentation.LayoutSlides.Select(layout => new SlidesLayoutInfo
                {
                    Name = EmptyToNull(layout.Name),
                    Master = EmptyToNull(layout.MasterSlide?.Name),
                    SlideCount = presentation.Slides.Count(slide => ReferenceEquals(slide.LayoutSlide, layout)),
                }).OrderBy(static layout => layout.Name, StringComparer.Ordinal).ToArray()
                : null,
            Media = media?.Take(MediaListLimit).ToArray(),
            Notes = Details(SlidesInspectDetails.Notes)
                ? presentation.Slides.Select((slide, index) =>
                {
                    string? notes = Notes(slide);
                    return new SlidesNotesInfo
                    {
                        Slide = index + 1,
                        Present = notes is not null,
                        CharacterCount = notes?.Length ?? 0,
                    };
                }).ToArray()
                : null,
            Comments = Details(SlidesInspectDetails.Comments)
                ? comments.Select(comment => new SlidesCommentInfo
                {
                    Slide = FindSlideNumber(presentation, comment.Slide),
                    Author = comment.Author.Name,
                    Text = comment.Text,
                }).OrderBy(static comment => comment.Slide).ThenBy(static comment => comment.Author, StringComparer.Ordinal).ToArray()
                : null,
            Fonts = Details(SlidesInspectDetails.Fonts)
                ? presentation.FontsManager.GetFonts().Select(static font => font.FontName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : null,
            Properties = Details(SlidesInspectDetails.Properties) ? Properties(presentation.DocumentProperties) : null,
            License = EnvelopeParts.License(state),
            Warnings = EnvelopeParts.CombineWarnings(
                InputWarnings(loaded),
                media is { Count: > MediaListLimit }
                    ?
                    [
                        EnvelopeParts.ListTruncated(
                            "media",
                            MediaListLimit,
                            media.Count,
                            "Run 'aspose-cli slides extract <presentation> --what media --out-dir <directory>' to export every media item."),
                    ]
                    : null),
        };
    }

    private static SlidesSlideInfo ProjectInfo(ISlide slide, int number, IReadOnlyList<IComment> comments, bool preview)
    {
        string? title = preview ? Title(slide) : null;
        return new SlidesSlideInfo
        {
            Slide = number,
            SlideId = slide.SlideId,
            Name = EmptyToNull(slide.Name),
            Layout = EmptyToNull(slide.LayoutSlide?.Name),
            Title = title,
            PreviewText = preview ? PreviewText(slide) : null,
            ShapeCount = slide.Shapes.Count,
            Hidden = slide.Hidden,
            HasNotes = !string.IsNullOrWhiteSpace(Notes(slide)),
            CommentCount = comments.Count(comment => ReferenceEquals(comment.Slide, slide)),
        };
    }

    private static IReadOnlyList<SlidesMediaInfo> Media(Presentation presentation)
    {
        var media = new List<SlidesMediaInfo>();
        foreach (IPPImage image in presentation.Images)
        {
            media.Add(new SlidesMediaInfo
            {
                Index = media.Count + 1,
                Kind = "image",
                ContentType = EmptyToNull(image.ContentType),
                SizeBytes = image.BinaryData.LongLength,
            });
        }

        foreach (IAudio audio in presentation.Audios)
        {
            media.Add(new SlidesMediaInfo
            {
                Index = media.Count + 1,
                Kind = "audio",
                ContentType = EmptyToNull(audio.ContentType),
                SizeBytes = audio.BinaryData.LongLength,
            });
        }

        foreach (IVideo video in presentation.Videos)
        {
            media.Add(new SlidesMediaInfo
            {
                Index = media.Count + 1,
                Kind = "video",
                ContentType = EmptyToNull(video.ContentType),
                SizeBytes = video.BinaryData.LongLength,
            });
        }

        return media;
    }

    private static int FindSlideNumber(Presentation presentation, ISlide slide)
    {
        for (int index = 0; index < presentation.Slides.Count; index++)
        {
            if (ReferenceEquals(presentation.Slides[index], slide))
            {
                return index + 1;
            }
        }

        return 0;
    }
}
