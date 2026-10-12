using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Commands;

internal static class InspectCommand
{
    private static readonly Dictionary<string, string> DetailNotes = new(StringComparer.Ordinal)
    {
        [WordsInspectDetails.Outline] = "headings",
        [WordsInspectDetails.Sections] = "page setup, headers and footers",
        [WordsInspectDetails.Properties] = "title, author, subject and keywords",
    };

    public static CommandDefinition<WordsInspectRequest, WordsInspectResult> Create()
    {
        var sample = new SampleOption("the heading outline, as with --detail outline");
        var detail = new DetailOption(WordsInspectDetails.All, DetailNotes);
        return new(
            "inspect",
            "Show document structure, safety state and metadata.",
            new CommandTraits { Input = WordsInputs.Document, UsesFonts = true },
            [.. sample.Options, .. detail.Options],
            (parse, standard) => new WordsInspectRequest
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
                "words inspect contract.docx --output-format json",
                "words inspect contract.docx --detail outline sections fields bookmarks --sample",
            ],
            Links =
            [
                CommandHelpLink.Docs(WordsModule.Manifest, "overview", "what each Words command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }

    internal static void Table(WordsInspectResult result, TableSurface surface)
    {
        WordsDocumentSummary document = result.Document;
        ResultText.Input(surface, result.Input);
        surface.Out.WriteLine(
            $"sections: {document.SectionCount}   blocks: {document.BlockCount} "
            + $"({document.ParagraphCount} paragraphs, {document.TableCount} tables)   "
            + $"pages: {document.PageCount}   words: {document.WordCount}");
        surface.Out.WriteLine($"revisions: {document.RevisionCount}   protection: {document.Protection}   signed: {TableText.YesNo(document.Signed)}   macros: {TableText.YesNo(document.HasMacros)}");
        RenderDetails(result, surface);
    }

    /// <summary>Each section --detail asked for, under its own heading.</summary>
    private static void RenderDetails(
        WordsInspectResult result,
        TableSurface surface)
    {
        RenderSections(result.Sections, surface);
        ResultText.Table(surface, "outline", result.Outline, ["block", "level", "heading"],
            static item => [TableText.Int(item.Block), TableText.Int(item.HeadingLevel), item.Text]);
        ResultText.List(surface, "styles", result.Styles);
        ResultText.List(surface, "bookmarks", result.Bookmarks);
        ResultText.List(surface, "fonts", result.Fonts);
        ResultText.Table(surface, "fields", result.Fields, ["block", "type", "code", "result"],
            static field => [WordsText.Block(field.Block), field.Type, field.Code, field.Result]);
        ResultText.Table(surface, "comments", result.Comments, ["block", "author", "text"],
            static comment => [WordsText.Block(comment.Block), comment.Author, comment.Text]);
        ResultText.Table(surface, "revisions", result.Revisions, ["revision", "block", "type", "author", "date", "text"],
            static revision =>
            [
                TableText.Int(revision.Revision),
                WordsText.Block(revision.Block),
                revision.Type,
                revision.Author,
                revision.Date,
                revision.Text,
            ]);
        ResultText.Table(surface, "images", result.Images, ["block", "name", "size"],
            static image => [WordsText.Block(image.Block), image.Name, $"{TableText.Points(image.Width)} x {TableText.Points(image.Height)} pt"]);
        ResultText.Table(surface, "tables", result.Tables, ["block", "rows", "columns", "style"],
            static item => [TableText.Int(item.Block), TableText.Int(item.RowCount), TableText.Int(item.ColumnCount), item.Style]);
        ResultText.Properties(surface, "properties", result.Properties);
    }

    // Page setup, then the header and footer paragraphs of every section that has any.
    private static void RenderSections(IReadOnlyList<WordsSectionData>? sections, TableSurface surface)
    {
        if (sections is null || !ResultText.Section(surface, "sections", sections.Count == 0))
        {
            return;
        }

        var table = new TextTable("section", "orientation", "page size", "margins (t/r/b/l)");
        foreach (WordsSectionData section in sections)
        {
            table.AddRow(
                TableText.Int(section.Section),
                section.Orientation,
                $"{TableText.Points(section.WidthPoints)} x {TableText.Points(section.HeightPoints)} pt",
                $"{TableText.Points(section.Margins.Top)}/{TableText.Points(section.Margins.Right)}/"
                + $"{TableText.Points(section.Margins.Bottom)}/{TableText.Points(section.Margins.Left)} pt");
        }

        table.WriteTo(surface.Out, surface.Format);
        var headersFooters = new TextTable("section", "location", "kind", "paragraphs");
        foreach (WordsSectionData section in sections)
        {
            foreach (WordsHeaderFooterData item in section.HeadersFooters)
            {
                headersFooters.AddRow(TableText.Int(section.Section), item.Location, item.Kind, string.Join(" | ", item.Paragraphs));
            }
        }

        if (sections.Any(static section => section.HeadersFooters.Count > 0))
        {
            headersFooters.WriteTo(surface.Out, surface.Format);
        }
    }
}
