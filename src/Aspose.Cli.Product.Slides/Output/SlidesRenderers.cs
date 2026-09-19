using System.Globalization;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Output;

/// <summary>Human renderers for presentation result families.</summary>
internal static class SlidesRenderers
{
    public static void Render(PresentationInfoResult result, TableSurface surface)
    {
        PresentationSummary presentation = result.Presentation;
        surface.Out.WriteLine($"{result.Source.Path} ({result.Source.Format}, {TableText.Bytes(result.Source.SizeBytes)})");
        surface.Out.WriteLine(
            $"slides: {presentation.Slides}   size: {Points(presentation.WidthPoints)} x {Points(presentation.HeightPoints)} pt   "
            + $"orientation: {presentation.Orientation}");
        surface.Out.WriteLine(
            $"masters: {presentation.Masters}   layouts: {presentation.Layouts}   sections: {presentation.Sections}   "
            + $"comments: {presentation.Comments}   media: {presentation.Media}   macros: {TableText.YesNo(presentation.HasMacros)}");

        var table = new TextTable("slide", "id", "title", "layout", "shapes", "flags");
        foreach (SlideInfo slide in result.Slides)
        {
            table.AddRow(
                TableText.Int(slide.Number),
                slide.SlideId.ToString(CultureInfo.InvariantCulture),
                slide.Title ?? slide.Name ?? string.Empty,
                slide.Layout ?? string.Empty,
                TableText.Int(slide.Shapes),
                string.Join(", ", new[]
                {
                    slide.Hidden ? "hidden" : null,
                    slide.HasNotes ? "notes" : null,
                    slide.Comments > 0 ? $"{slide.Comments} comment(s)" : null,
                }.Where(static value => value is not null)));
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(PresentationReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path}: slides {result.Window.Slides} of {result.Window.Of} ({result.Scope})");
        foreach (SlideData slide in result.Slides)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- slide {slide.Number} [{slide.SlideId}] {slide.Title ?? slide.Name ?? string.Empty} ---");
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

        if (result.Next is not null)
        {
            surface.Out.WriteLine($"next: {result.Next}");
        }
    }

    public static void Render(SlidesConvertResult result, TableSurface surface)
    {
        foreach (var output in result.Outputs)
        {
            surface.Out.WriteLine(
                $"wrote {output.Path} ({output.Format}, {TableText.Bytes(output.SizeBytes)})"
                + (result.Slides is null ? string.Empty : $" from slides {result.Slides}"));
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
            $"created {result.Output.Path} ({result.Slides} slide(s), {TableText.Bytes(result.Output.SizeBytes)})");

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
        surface.Out.WriteLine(result.DryRun
            ? $"dry run: {result.Applied.Count} operation(s)"
            : $"edited {result.Output?.Path} ({result.Applied.Count} operation(s))");
        foreach (BoundedOperationOutcome op in result.Applied)
        {
            surface.Out.WriteLine(
                $"[{op.Index}] {op.Op}: {op.Status}"
                + $" ({op.ItemsAffected} affected)"
                + (op.Error is null ? string.Empty : $" - {op.Error.Code}: {op.Error.Message}"));
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
        if (result.Truncated)
        {
            surface.Out.WriteLine("results truncated; lower the scope or raise --max-hits");
        }
    }

    private static string Points(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
