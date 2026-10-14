namespace Aspose.Cli.Product.Slides.Contracts;

/// <summary>One file a command wrote, and the slide it holds when it holds one.</summary>
public sealed record SlidesSlideOutput : OutputItem
{
    /// <summary>Describes a file written for the presentation or one of its slides.</summary>
    /// <param name="file">The published file, as <c>FileObjects.Output</c> describes it.</param>
    internal SlidesSlideOutput(OutputInfo file)
        : base(file)
    {
    }

    /// <summary>
    /// The 1-based slide the file holds; absent when it holds no single slide, as a whole-deck
    /// file or a companion file the format writes beside it does.
    /// </summary>
    [Minimum(1)]
    public int? Slide { get; init; }
}
