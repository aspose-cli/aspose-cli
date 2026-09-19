using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>
/// The single definition of which layout placeholders hold a slide's title and
/// content. Standard "Title and Content" layouts use an untyped (object)
/// placeholder for content, so body lookup must accept both body and object.
/// </summary>
internal static class SlidesPlaceholders
{
    internal static bool IsTitle(IPlaceholder? placeholder) =>
        placeholder?.Type is PlaceholderType.Title or PlaceholderType.CenteredTitle;

    internal static IAutoShape? Title(ISlide slide) =>
        slide.Shapes.OfType<IAutoShape>().FirstOrDefault(static shape => IsTitle(shape.Placeholder));

    /// <summary>Content placeholders in layout order; a title slide's subtitle counts when requested.</summary>
    internal static IAutoShape[] Content(ISlide slide, bool includeSubtitle = false) =>
        slide.Shapes
            .OfType<IAutoShape>()
            .Where(shape => shape.Placeholder?.Type is PlaceholderType.Body or PlaceholderType.Object
                || (includeSubtitle && shape.Placeholder?.Type == PlaceholderType.Subtitle))
            .OrderBy(static shape => shape.Placeholder!.Index)
            .ToArray();
}
