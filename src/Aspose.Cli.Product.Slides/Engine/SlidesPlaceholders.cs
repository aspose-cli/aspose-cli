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

    /// <summary>
    /// The presentation's layout of the requested type. A template without it falls back
    /// to Title and Content, then to any layout with a title placeholder.
    /// </summary>
    internal static ILayoutSlide Layout(Presentation presentation, SlideLayoutType type) =>
        presentation.LayoutSlides.GetByType(type)
            ?? presentation.LayoutSlides.GetByType(SlideLayoutType.TitleAndObject)
            ?? presentation.LayoutSlides.FirstOrDefault(static layout =>
                layout.Shapes.Any(static shape => IsTitle(shape.Placeholder)))
            ?? presentation.LayoutSlides[0];

    /// <summary>
    /// The public role of a placeholder, shared by queries and shape targets. Content
    /// placeholders report "body" whether the layout typed them as body or object.
    /// </summary>
    internal static string? Role(PlaceholderType? type) => type switch
    {
        null => null,
        PlaceholderType.Title or PlaceholderType.CenteredTitle => "title",
        PlaceholderType.Body or PlaceholderType.Object => "body",
        PlaceholderType.Subtitle => "subtitle",
        PlaceholderType.Footer => "footer",
        PlaceholderType.DateAndTime => "date",
        PlaceholderType.SlideNumber => "slide-number",
        _ => type.Value.ToString().ToLowerInvariant(),
    };
}
