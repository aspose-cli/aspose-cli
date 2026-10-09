using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>One bounded slide-content window.</summary>
internal static class SlidesRead
{
    internal static PresentationReadResult Run(SlidesSession session, PresentationReadRequest request)
    {
        LicenseState state = session.Outputs.License;
        using LoadedPresentation loaded = session.Loader.Open(request.Input, request.Password);
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

        // Without --slides the selection is every slide, of which a read returns at most ten.
        int selected = request.Slides is null ? presentation.Slides.Count : requested.Count;
        int last = slides.Count == 0 ? 0 : slides[^1].Slide;
        bool selectionTruncated = slides.Count < requested.Count;
        bool defaultWindowTruncated = request.Slides is null && last < presentation.Slides.Count;
        bool truncated = selectionTruncated || defaultWindowTruncated || slides.Any(static slide => slide.ContentTruncated);
        return new PresentationReadResult
        {
            Source = Source(request.Input, loaded.FormatId),
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
