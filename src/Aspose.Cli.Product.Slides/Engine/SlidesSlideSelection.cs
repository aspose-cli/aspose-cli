using Aspose.Cli.Sdk.Errors;
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

    /// <summary>The slide numbers, from 1, a range selects; a presentation without slides has none to select.</summary>
    internal static IReadOnlyList<int> ResolveSlideRange(PageRange range, int slideCount) =>
        range.Resolve(slideCount, ErrorCodes.AddressNotFound, "slide");
}
