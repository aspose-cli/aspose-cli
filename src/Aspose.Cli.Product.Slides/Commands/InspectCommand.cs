using System.Globalization;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Slides.Commands;

internal static class InspectCommand
{
    public static CommandDefinition<SlidesInspectRequest, SlidesInspectResult> Create()
    {
        var sample = new SampleOption($"each slide's title and up to {SlidesSlideInfo.PreviewTextLength} characters of its text");
        var detail = new DetailOption(
            SlidesInspectDetails.All,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SlidesInspectDetails.Notes] = "presence and character count, with the text from 'slides read --notes'",
            });
        return new(
            "inspect",
            "Show presentation structure, stable slide ids and metadata.",
            new CommandTraits { Input = SlidesInputs.Presentation },
            [.. sample.Options, .. detail.Options],
            (parse, standard) => new SlidesInspectRequest
            {
                Input = standard.Input,
                IncludePreview = sample.Read(parse),
                Details = detail.Read(parse),
                Password = standard.InputPassword,
            },
            Table)
        {
            Examples =
            [
                "slides inspect deck.pptx --output-format json",
                "slides inspect deck.pptx --sample --detail layouts notes",
            ],
            Links =
            [
                CommandHelpLink.Docs(SlidesModule.Manifest, "overview", "what each Slides command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }

    internal static void Table(SlidesInspectResult result, TableSurface surface)
    {
        SlidesPresentationSummary presentation = result.Presentation;
        ResultText.Input(surface, result.Input);
        surface.Out.WriteLine(
            $"slides: {presentation.SlideCount}   size: {TableText.Points(presentation.SlideSize.Width)} x {TableText.Points(presentation.SlideSize.Height)} pt   "
            + $"orientation: {presentation.Orientation}");
        surface.Out.WriteLine(
            $"masters: {presentation.MasterCount}   layouts: {presentation.LayoutCount}   sections: {presentation.SectionCount}   "
            + $"comments: {presentation.CommentCount}   media: {presentation.MediaCount}   macros: {TableText.YesNo(presentation.HasMacros)}");

        var table = new TextTable("slide", "id", "title", "layout", "shapes", "flags");
        foreach (SlidesSlideInfo slide in result.Slides)
        {
            table.AddRow(
                TableText.Int(slide.Slide),
                slide.SlideId.ToString(CultureInfo.InvariantCulture),
                slide.Title ?? slide.Name,
                slide.Layout,
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
    private static void DetailTables(SlidesInspectResult result, TableSurface surface)
    {
        ResultText.Table(surface, "sections", result.Sections, ["name", "id", "start slide"],
            static section => [section.Name, section.SectionId, TableText.Int(section.StartSlide)]);
        ResultText.Table(surface, "masters", result.Masters, ["name", "slides"],
            static master => [master.Name, TableText.Int(master.SlideCount)]);
        ResultText.Table(surface, "layouts", result.Layouts, ["name", "master", "slides"],
            static layout => [layout.Name, layout.Master, TableText.Int(layout.SlideCount)]);
        ResultText.Table(surface, "media", result.Media, ["index", "type", "content type", "size"],
            static item => [TableText.Int(item.Index), item.Kind, item.ContentType, TableText.Bytes(item.SizeBytes)]);
        ResultText.Table(surface, "notes", result.Notes, ["slide", "present", "characters"],
            static note => [TableText.Int(note.Slide), TableText.YesNo(note.Present), TableText.Int(note.CharacterCount)]);
        ResultText.Table(surface, "comments", result.Comments, ["slide", "author", "text"],
            static comment => [TableText.Int(comment.Slide), comment.Author, comment.Text]);
        ResultText.List(surface, "fonts", result.Fonts);

        ResultText.Properties(surface, "properties", result.Properties);
    }
}
