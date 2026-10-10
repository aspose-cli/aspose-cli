using System.Globalization;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class InspectCommand
{
    public static CommandDefinition<PresentationInfoRequest, PresentationInfoResult> Create()
    {
        var preview = new PreviewOption($"each slide's title and up to {SlideInfo.PreviewTextLength} characters of its text");
        var detail = new DetailOption(
            InfoDetails.All,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [InfoDetails.Notes] = "presence and character count, with the text from query slides --notes",
            });
        return new(
            "inspect",
            "Show presentation structure, stable slide ids and metadata.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            [.. preview.Options, .. detail.Options],
            (parse, standard) => new PresentationInfoRequest
            {
                Input = standard.Input,
                IncludePreview = preview.Read(parse),
                Details = detail.Read(parse),
                Password = standard.InputPassword,
            },
            Table);
    }

    internal static void Table(PresentationInfoResult result, TableSurface surface)
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
        DetailTables(result, surface);
    }

    /// <summary>The sections --detail asked for, in the order the JSON result lists them.</summary>
    private static void DetailTables(PresentationInfoResult result, TableSurface surface)
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
}
