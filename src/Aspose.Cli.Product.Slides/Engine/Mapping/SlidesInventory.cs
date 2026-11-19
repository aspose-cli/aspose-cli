using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SmartArt;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// The inventory of a presentation: how many objects of each kind of content it holds, counted
/// with the engine's own collections, and whether <c>read</c> lists them. An edit reports the
/// counts that changed; a read reports all of them.
/// </summary>
internal static class SlidesInventory
{
    /// <summary>The shape interfaces the inventory counts shapes by.</summary>
    private static readonly string[] ShapeTypes =
    [
        nameof(IAutoShape), nameof(IPictureFrame), nameof(ITable), nameof(IChart), nameof(IGroupShape), nameof(ISmartArt),
        nameof(IAudioFrame), nameof(IVideoFrame), nameof(IOleObjectFrame), nameof(IConnector),
    ];

    /// <summary>The kinds the inventory counts, in the order a result lists them; read once the table's kinds exist.</summary>
    internal static IReadOnlyList<InventoryKind> Kinds => Declared.Value;

    private static class Declared
    {
        internal static readonly IReadOnlyList<InventoryKind> Value =
        [
        new("slides", "The slides: Presentation.Slides.Count.", SlidesTable.SlideKind),
        new("hiddenSlides", "The slides whose ISlide.Hidden is true.", SlidesTable.SlideKind),
        new("masters", "The masters: Presentation.Masters.Count.", SlidesTable.MasterKind),
        new("layouts", "The layouts of every master: Presentation.LayoutSlides.Count.", SlidesTable.LayoutKind),
        new("sections", "The sections: Presentation.Sections.Count.", SlidesTable.SectionKind),
        new("notesSlides", "The slides that have a notes slide.", SlidesTable.NotesKind),
        .. ShapeTypes.Select(static type => new InventoryKind($"shapes.{type}", $"The shapes of the slides that are {type}, in groups or not.", SlidesTable.ShapeKind)),
        new("shapes.other", "The shapes of the slides of every other type, in groups or not.", SlidesTable.ShapeKind),
        new("groupedShapes", "The shapes of the slides that sit in a group.", SlidesTable.ShapeKind),
        new("images", "The pictures the presentation holds: Presentation.Images.Count.", SlidesTable.ImageKind),
        new("audios", "The audio the presentation holds: Presentation.Audios.Count.", Kind: null),
        new("videos", "The videos the presentation holds: Presentation.Videos.Count.", Kind: null),
        new("comments", "The comments of every comment author.", Kind: null),
        new("layoutShapes", "The shapes of the layouts.", Kind: null),
        new("masterShapes", "The shapes of the masters.", Kind: null),
        new("smartArtNodes", "The nodes of the slides' SmartArt shapes, at every level: ISmartArt.AllNodes.Count.", Kind: null),
        new("axisTitles", "The axes of the slides' charts whose IAxis.HasTitle is true.", Kind: null),
        new("dataLabels", "The data labels of the slides' chart series: IChartSeries.Labels.Count.", Kind: null),
        new("mainSequenceEffects", "The animation effects of the slides' main sequences: ISlide.Timeline.MainSequence.Count.", Kind: null),
        new("interactiveSequenceEffects", "The animation effects of the slides' interactive sequences.", Kind: null),
        new("hyperlinks", "The click and mouse-over hyperlinks of the slides' shapes and text portions.", Kind: null),
        new("vbaProjects", "1 when the presentation holds a VBA project (Presentation.VbaProject), else 0.", Kind: null),
        new("embeddedFonts", "The embedded fonts: IFontsManager.GetEmbeddedFonts().Length.", Kind: null),
        new("customProperties", "The custom document properties: IDocumentProperties.CountOfCustomProperties.", Kind: null),
        ];
    }

    /// <summary>Counts every kind.</summary>
    internal static IReadOnlyDictionary<string, long> Count(Presentation presentation)
    {
        IShape[] shapes = [.. presentation.Slides.SelectMany(static slide => SlidesShapeTypes.All(slide.Shapes))];
        var counts = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["slides"] = presentation.Slides.Count,
            ["hiddenSlides"] = presentation.Slides.Count(static slide => slide.Hidden),
            ["masters"] = presentation.Masters.Count,
            ["layouts"] = presentation.LayoutSlides.Count,
            ["sections"] = presentation.Sections.Count,
            ["notesSlides"] = presentation.Slides.Count(static slide => slide.NotesSlideManager.NotesSlide is not null),
            ["shapes.other"] = shapes.Count(static shape => !ShapeTypes.Contains(SlidesShapeTypes.Of(shape))),
            ["groupedShapes"] = presentation.Slides.Sum(static slide => (long)SlidesShapeTypes.Grouped(slide.Shapes).Count()),
            ["images"] = presentation.Images.Count,
            ["audios"] = presentation.Audios.Count,
            ["videos"] = presentation.Videos.Count,
            ["comments"] = presentation.CommentAuthors.Sum(static author => (long)author.Comments.Count),
            ["layoutShapes"] = presentation.LayoutSlides.Sum(static layout => (long)layout.Shapes.Count),
            ["masterShapes"] = presentation.Masters.Sum(static master => (long)master.Shapes.Count),
            ["smartArtNodes"] = shapes.OfType<ISmartArt>().Sum(static smartArt => (long)smartArt.AllNodes.Count),
            ["axisTitles"] = shapes.OfType<IChart>().Sum(static chart => (chart.Axes.HorizontalAxis?.HasTitle == true ? 1L : 0) + (chart.Axes.VerticalAxis?.HasTitle == true ? 1 : 0)),
            ["dataLabels"] = shapes.OfType<IChart>().Sum(static chart => chart.ChartData.Series.Sum(static series => (long)series.Labels.Count)),
            ["mainSequenceEffects"] = presentation.Slides.Sum(static slide => (long)slide.Timeline.MainSequence.Count),
            ["interactiveSequenceEffects"] = presentation.Slides.Sum(static slide => slide.Timeline.InteractiveSequences.Sum(static sequence => (long)sequence.Count)),
            ["hyperlinks"] = shapes.Sum(Hyperlinks),
            ["vbaProjects"] = presentation.VbaProject is null ? 0 : 1,
            ["embeddedFonts"] = presentation.FontsManager.GetEmbeddedFonts().Length,
            ["customProperties"] = presentation.DocumentProperties.CountOfCustomProperties,
        };
        foreach (string type in ShapeTypes)
        {
            counts[$"shapes.{type}"] = shapes.Count(shape => SlidesShapeTypes.Of(shape) == type);
        }

        return counts;
    }

    private static long Hyperlinks(IShape shape)
    {
        long count = (shape.HyperlinkClick is null ? 0 : 1) + (shape.HyperlinkMouseOver is null ? 0 : 1);
        if (shape is IAutoShape { TextFrame: { } frame })
        {
            count += frame.Paragraphs.SelectMany(static paragraph => paragraph.Portions)
                .Sum(static portion => (portion.PortionFormat.HyperlinkClick is null ? 0L : 1) + (portion.PortionFormat.HyperlinkMouseOver is null ? 0 : 1));
        }

        return count;
    }
}
