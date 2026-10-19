using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>The slides a range selects, and a presentation reduced to them.</summary>
internal static class SlidesSlideSelection
{
    internal static void RemoveUnselectedSlides(Presentation presentation, IReadOnlyList<int> slides)
    {
        var keep = new HashSet<int>(slides);
        ISlide[] unselected = presentation.Slides.Where((_, index) => !keep.Contains(index + 1)).ToArray();
        foreach (ISlide slide in unselected)
        {
            presentation.Slides.Remove(slide);
        }
    }

    /// <summary>The slide numbers a range selects; a presentation without slides has none to select.</summary>
    internal static IReadOnlyList<int> ResolveSlideRange(PageRange range, int slideCount) =>
        range.Resolve(slideCount, SlidesDiagnostics.SlideNotFound, "slide");

    /// <summary>Every slide number, as the range <c>1-</c> selects them.</summary>
    internal static IReadOnlyList<int> AllSlides(int slideCount) =>
        ResolveSlideRange(PageRange.Parse("1-"), slideCount);
}
