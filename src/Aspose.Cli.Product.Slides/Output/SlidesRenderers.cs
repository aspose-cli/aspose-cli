using System.Globalization;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Output;

/// <summary>Human renderers for presentation result families.</summary>
internal static class SlidesRenderers
{
    public static void Render(PresentationInfoResult result, TableSurface surface)
    {
        PresentationSummary presentation = result.Presentation;
        ResultText.Source(surface, result.Source);
        surface.Out.WriteLine(
            $"slides: {presentation.SlideCount}   size: {TableText.Points(presentation.WidthPoints)} x {TableText.Points(presentation.HeightPoints)} pt   "
            + $"orientation: {presentation.Orientation}");
        surface.Out.WriteLine(
            $"masters: {presentation.MasterCount}   layouts: {presentation.LayoutCount}   sections: {presentation.SectionCount}   "
            + $"comments: {presentation.CommentCount}   media: {presentation.MediaCount}   macros: {TableText.YesNo(presentation.HasMacros)}");

        var table = new TextTable("slide", "id", "title", "layout", "shapes", "flags");
        foreach (SlideInfo slide in result.Slides)
        {
            table.AddRow(
                TableText.Int(slide.Slide),
                slide.SlideId.ToString(CultureInfo.InvariantCulture),
                slide.Title ?? slide.Name ?? string.Empty,
                slide.Layout ?? string.Empty,
                TableText.Int(slide.ShapeCount),
                string.Join(", ", new[]
                {
                    slide.Hidden ? "hidden" : null,
                    slide.HasNotes ? "notes" : null,
                    slide.CommentCount > 0 ? $"{slide.CommentCount} comment(s)" : null,
                }.Where(static value => value is not null)));
        }

        table.WriteTo(surface.Out, surface.Format);
        RenderDetails(result, surface);
    }

    /// <summary>The sections --detail asked for, in the order the JSON result lists them.</summary>
    private static void RenderDetails(PresentationInfoResult result, TableSurface surface)
    {
        ResultText.Table(surface, "sections", result.Sections, ["name", "id", "start slide"],
            static section => [section.Name, section.SectionId, TableText.Int(section.StartSlide)]);
        ResultText.Table(surface, "masters", result.Masters, ["name", "slides"],
            static master => [master.Name, TableText.Int(master.SlideCount)]);
        ResultText.Table(surface, "layouts", result.Layouts, ["name", "master", "slides"],
            static layout => [layout.Name, layout.Master ?? string.Empty, TableText.Int(layout.SlideCount)]);
        ResultText.Table(surface, "media", result.Media, ["index", "type", "content type", "size"],
            static item => [TableText.Int(item.Index), item.Type, item.ContentType ?? string.Empty, TableText.Bytes(item.SizeBytes)]);
        ResultText.Table(surface, "notes", result.Notes, ["slide", "present", "characters"],
            static note => [TableText.Int(note.Slide), TableText.YesNo(note.Present), TableText.Int(note.CharacterCount)]);
        ResultText.Table(surface, "comments", result.Comments, ["slide", "author", "text"],
            static comment => [TableText.Int(comment.Slide), comment.Author, comment.Text]);
        ResultText.List(surface, "fonts", result.Fonts);

        ResultText.Properties(surface, "properties", result.Properties);
    }

    public static void Render(PresentationReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path}: {result.SlideCount} slide(s) ({result.Scope})");
        foreach (SlideData slide in result.Slides)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- slide {slide.Slide} [{slide.SlideId}] {slide.Title ?? slide.Name ?? string.Empty} ---");
            foreach (string text in slide.Text ?? [])
            {
                surface.Out.WriteLine(text);
            }

            foreach (SlideShapeData shape in slide.Shapes)
            {
                if (shape.Text is not null)
                {
                    surface.Out.WriteLine(shape.Text);
                }
            }

            if (slide.Notes is not null)
            {
                surface.Out.WriteLine($"notes: {slide.Notes}");
            }
        }
    }

    public static void Render(SlidesConvertResult result, TableSurface surface)
    {
        foreach (var output in result.Outputs)
        {
            ResultText.Produced(surface, output, result.Slides is null ? null : $"slides {result.Slides}");
        }
    }

    public static void Render(SlidesRenderResult result, TableSurface surface)
    {
        foreach (SlideRenderOutput item in result.Outputs)
        {
            surface.Out.WriteLine(
                $"rendered slide {item.Slide} [{item.SlideId}] to {item.Output.Path} "
                + $"({item.Output.Format}, {TableText.Bytes(item.Output.SizeBytes)})");
        }
    }

    public static void Render(SlidesCreateResult result, TableSurface surface) =>
        surface.Out.WriteLine(
            $"created {result.Output.Path} ({result.SlideCount} slide(s), {TableText.Bytes(result.Output.SizeBytes)})");

    public static void Render(SlidesExtractResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"extracted {result.Items.Count} {result.What} item(s)");
        foreach (SlidesExtractedItem item in result.Items)
        {
            surface.Out.WriteLine($"{item.Path} ({item.Kind}, {TableText.Bytes(item.SizeBytes)})");
        }
    }

    public static void Render(SlidesEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        if (result.SlidesTouched is { Count: > 0 } slides)
        {
            surface.Out.WriteLine($"slide ids touched: {string.Join(", ", slides)}");
        }
    }

    public static void Render(SlidesSearchResult result, TableSurface surface)
    {
        var table = new TextTable("slide", "id", "scope", "shape", "start", "text");
        foreach (SlidesSearchHit hit in result.Hits)
        {
            table.AddRow(
                TableText.Int(hit.Slide),
                hit.SlideId.ToString(CultureInfo.InvariantCulture),
                hit.Scope,
                hit.ShapeName ?? hit.ShapeId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                TableText.Int(hit.Start),
                hit.Text);
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
