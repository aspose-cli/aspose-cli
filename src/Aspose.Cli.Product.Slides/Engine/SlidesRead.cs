using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.Mapping.SlidesStateProjection;
using static Aspose.Cli.Product.Slides.Engine.SlidesResultParts;
using static Aspose.Cli.Product.Slides.Engine.SlidesShapeText;
using static Aspose.Cli.Product.Slides.Engine.SlidesSlideSelection;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>One bounded slide-content window.</summary>
internal static class SlidesRead
{
    internal static SlidesReadResult Run(SlidesSession session, SlidesReadRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
        session.Outputs.ReadInput(loaded.Presentation);
        Presentation presentation = loaded.Presentation;
        IReadOnlyList<int> requested = request.Slides is null
            ? Enumerable.Range(1, Math.Min(10, presentation.Slides.Count)).ToArray()
            : ResolveSlideRange(request.Slides, presentation.Slides.Count);
        IComment[] comments = Comments(presentation);
        int remaining = request.MaxCharacters;
        var slides = new List<SlidesSlideData>();
        foreach (int number in requested)
        {
            SlidesSlideData projected = ProjectSlide(
                presentation.Slides[number - 1],
                number,
                request.Content,
                request.IncludeNotes,
                comments,
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
        return new SlidesReadResult
        {
            Input = Source(request.Input, loaded),
            Content = request.Content,
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
            Warnings = InputWarnings(loaded),
        };
    }

    private static SlidesSlideData ProjectSlide(
        ISlide slide,
        int number,
        string content,
        bool includeNotes,
        IReadOnlyList<IComment> comments,
        ref int remaining)
    {
        bool includeShapes = content is SlidesReadContents.Shapes or SlidesReadContents.Full;
        bool contentTruncated = false;
        string? title = Take(Title(slide), ref remaining, ref contentTruncated);
        var textBlocks = content == SlidesReadContents.Text ? new List<string>() : null;
        var shapes = new List<SlidesShapeData>();
        bool full = content == SlidesReadContents.Full;
        foreach (IShape shape in slide.Shapes)
        {
            string? sourceText = ShapeText(shape);
            if (remaining == 0 && (includeShapes || !string.IsNullOrWhiteSpace(sourceText)))
            {
                contentTruncated = true;
                break;
            }
            string? text = Take(sourceText, ref remaining, ref contentTruncated);
            if (textBlocks is not null && !string.IsNullOrEmpty(text))
            {
                textBlocks.Add(text);
            }
            if (includeShapes)
            {
                shapes.Add(Shape(shape, text, full, ref remaining, ref contentTruncated));
            }
        }
        string? notes = full || includeNotes ? Take(Notes(slide), ref remaining, ref contentTruncated) : null;
        var read = new SlidesSlideData
        {
            Slide = number,
            SlideId = slide.SlideId,
            Name = EmptyToNull(slide.Name),
            Layout = EmptyToNull(slide.LayoutSlide?.Name),
            Title = title,
            Text = textBlocks,
            Shapes = shapes,
            Notes = notes is null ? null : new SlidesNotesData { Text = notes },
            ContentTruncated = contentTruncated,
        };
        if (!full)
        {
            return read;
        }

        // --content full also reports the comments, footer, background and transition.
        IReadOnlyList<SlidesCommentData> slideComments = SlideComments(slide, comments, ref remaining, ref contentTruncated);
        SlidesFooterData footer = Footer(slide, ref remaining, ref contentTruncated);
        return read with
        {
            Comments = slideComments,
            Footer = footer,
            Background = Background(slide),
            Transition = Transition(slide),
            ContentTruncated = contentTruncated,
        };
    }

    private static List<SlidesCommentData> SlideComments(
        ISlide slide,
        IReadOnlyList<IComment> comments,
        ref int remaining,
        ref bool truncated)
    {
        var projected = new List<SlidesCommentData>();
        foreach (IComment comment in comments.Where(comment => ReferenceEquals(comment.Slide, slide)))
        {
            if (remaining == 0 && !string.IsNullOrWhiteSpace(comment.Text))
            {
                truncated = true;
                break;
            }

            projected.Add(new SlidesCommentData
            {
                Author = comment.Author.Name,
                Text = Take(comment.Text, ref remaining, ref truncated) ?? string.Empty,
            });
        }

        return projected;
    }
}
