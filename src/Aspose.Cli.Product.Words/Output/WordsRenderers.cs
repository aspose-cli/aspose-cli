using System.Globalization;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Words.Output;

/// <summary>Human renderers for word-processing result families.</summary>
internal static class WordsRenderers
{
    public static void Render(DocumentInfoResult result, TableSurface surface)
    {
        DocumentSummary document = result.Document;
        surface.Out.WriteLine($"{result.Source.Path} ({result.Source.Format}, {TableText.Bytes(result.Source.SizeBytes)})");
        surface.Out.WriteLine(
            $"sections: {document.SectionCount}   blocks: {document.BlockCount} "
            + $"({document.ParagraphCount} paragraphs, {document.TableCount} tables)   "
            + $"pages: {document.PageCount}   words: {document.WordCount}");
        surface.Out.WriteLine($"revisions: {document.RevisionCount}   protection: {document.Protection}   signed: {TableText.YesNo(document.Signed)}   macros: {TableText.YesNo(document.HasMacros)}");
        RenderInfoDetails(result, surface);
    }

    public static void Render(DocumentReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path} (scope {result.Scope}, {result.BlockCount} blocks in the document)");
        var table = new TextTable("block", "type", "section", "style", "text");
        foreach (BlockData block in result.Blocks)
        {
            table.AddRow(TableText.Int(block.Block), block.Type, TableText.Int(block.Section), block.Style ?? "-", block.Text ?? $"[{block.RowCount}x{block.ColumnCount} table]");
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(WordsConvertResult result, TableSurface surface) =>
        ResultText.Produced(surface, result.Output, result.Pages is null ? null : $"pages {result.Pages}");

    public static void Render(WordsRenderResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"rendered {result.Outputs.Count} page(s)");
        foreach (PageOutput page in result.Outputs)
        {
            surface.Out.WriteLine($"  page {page.Page}: {page.Output.Path} ({TableText.Bytes(page.Output.SizeBytes)})");
        }
    }

    public static void Render(WordsCreateResult result, TableSurface surface) => ResultText.Produced(surface, result.Output);

    public static void Render(WordsEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        if (result.PagesTouched is { Count: > 0 } pages)
        {
            surface.Out.WriteLine($"pages touched: {string.Join(", ", pages)}");
        }

        if (result.Verification is { } verification)
        {
            surface.Out.WriteLine(
                $"verification: {(verification.Ok ? "ok" : "needs attention")}");
            foreach (VerificationIssue issue in verification.Issues)
            {
                surface.Out.WriteLine($"  {issue.Code}: {issue.Message}");
            }
        }
    }

    public static void Render(WordsCompareResult result, TableSurface surface)
    {
        surface.Out.WriteLine(result.Identical ? "documents are identical" : "documents differ");
        surface.Out.WriteLine($"insertions: {result.Revisions.InsertionCount}   deletions: {result.Revisions.DeletionCount}   formatting: {result.Revisions.FormatChangeCount}   moves: {result.Revisions.MoveCount}");
        if (result.Samples.Count > 0)
        {
            var table = new TextTable("type", "sample");
            foreach (RevisionSample sample in result.Samples)
            {
                table.AddRow(sample.Type, sample.Text ?? string.Empty);
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Output is { } output)
        {
            surface.Out.WriteLine($"redline: {output.Path}");
        }
    }

    public static void Render(WordsSearchResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Hits.Count} hit(s) for '{result.Pattern}'");
        var table = new TextTable("block", "section", "scope", "text");
        foreach (WordsSearchHit hit in result.Hits)
        {
            table.AddRow(
                Block(hit.Block),
                TableText.Int(hit.Section),
                hit.Location is { } location ? $"{hit.Scope} ({location}, {hit.Kind})" : hit.Scope,
                hit.Snippet);
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(WordsSplitResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"wrote {result.Outputs.Count} part(s)");
        foreach (SplitOutput output in result.Outputs)
        {
            surface.Out.WriteLine($"  {output.Index}: {output.Output.Path}");
        }
    }

    public static void Render(WordsExtractResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"extracted {result.Items.Count} {result.What} item(s)");
        foreach (ExtractedItem item in result.Items)
        {
            surface.Out.WriteLine($"  {item.Path} ({TableText.Bytes(item.SizeBytes)})");
        }
    }

    /// <summary>Each section --detail asked for, under its own heading.</summary>
    private static void RenderInfoDetails(
        DocumentInfoResult result,
        TableSurface surface)
    {
        RenderSections(result.Sections, surface);
        ResultText.Table(surface, "outline", result.Outline, ["block", "level", "heading"],
            static item => [TableText.Int(item.Block), TableText.Int(item.HeadingLevel), item.Text]);
        ResultText.List(surface, "styles", result.Styles);
        ResultText.List(surface, "bookmarks", result.Bookmarks);
        ResultText.List(surface, "fonts", result.Fonts);
        ResultText.Table(surface, "fields", result.Fields, ["block", "type", "code", "result"],
            static field => [Block(field.Block), field.Type, field.Code ?? "-", field.Result ?? "-"]);
        ResultText.Table(surface, "comments", result.Comments, ["block", "author", "text"],
            static comment => [Block(comment.Block), comment.Author, comment.Text]);
        ResultText.Table(surface, "revisions", result.Revisions, ["revision", "block", "type", "author", "date", "text"],
            static revision =>
            [
                TableText.Int(revision.Revision),
                Block(revision.Block),
                revision.Type,
                revision.Author,
                revision.Date ?? "-",
                revision.Text ?? "-",
            ]);
        ResultText.Table(surface, "images", result.Images, ["block", "name", "size"],
            static image => [Block(image.Block), image.Name ?? "-", $"{Points(image.Width)} x {Points(image.Height)} pt"]);
        ResultText.Table(surface, "tables", result.Tables, ["block", "rows", "columns", "style"],
            static item => [TableText.Int(item.Block), TableText.Int(item.RowCount), TableText.Int(item.ColumnCount), item.Style ?? "-"]);
        RenderProperties(result, surface);
    }

    // Page setup, then the header and footer paragraphs of every section that has any.
    private static void RenderSections(IReadOnlyList<SectionData>? sections, TableSurface surface)
    {
        if (sections is null || !ResultText.Section(surface, "sections", sections.Count == 0))
        {
            return;
        }

        var table = new TextTable("section", "orientation", "page size", "margins (t/r/b/l)");
        foreach (SectionData section in sections)
        {
            table.AddRow(
                TableText.Int(section.Section),
                section.Orientation,
                $"{Points(section.WidthPoints)} x {Points(section.HeightPoints)} pt",
                $"{Points(section.Margins.Top)}/{Points(section.Margins.Right)}/"
                + $"{Points(section.Margins.Bottom)}/{Points(section.Margins.Left)} pt");
        }

        table.WriteTo(surface.Out, surface.Format);
        var headersFooters = new TextTable("section", "location", "kind", "paragraphs");
        foreach (SectionData section in sections)
        {
            foreach (HeaderFooterData item in section.HeadersFooters)
            {
                headersFooters.AddRow(TableText.Int(section.Section), item.Location, item.Kind, string.Join(" | ", item.Paragraphs));
            }
        }

        if (sections.Any(static section => section.HeadersFooters.Count > 0))
        {
            headersFooters.WriteTo(surface.Out, surface.Format);
        }
    }

    private static void RenderProperties(
        DocumentInfoResult result,
        TableSurface surface)
    {
        if (result.Properties is { } properties && ResultText.Section(surface, "properties", properties.Count == 0))
        {
            var table = new TextTable("name", "value");
            foreach ((string name, string? value) in properties.OrderBy(
                         static property => property.Key,
                         StringComparer.Ordinal))
            {
                table.AddRow(name, value ?? "-");
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }

    // An item without a body block, such as one in a header, shows a dash.
    private static string Block(int? block) => block is { } value ? TableText.Int(value) : "-";

    private static string Points(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
