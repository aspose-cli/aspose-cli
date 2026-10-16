namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>One slide image render wrote, and the slide it shows.</summary>
public sealed record SlidesSlideOutput : OutputItem
{
    /// <summary>Describes the image written for one slide.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    internal SlidesSlideOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>The 1-based slide the image shows.</summary>
    [Minimum(1)]
    public int? Slide { get; init; }
}
