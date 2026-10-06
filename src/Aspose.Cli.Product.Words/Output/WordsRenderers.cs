using System.Globalization;
using Aspose.Cli.Product.Words.Contracts;
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

    private static void RenderInfoDetails(
        DocumentInfoResult result,
        TableSurface surface)
    {
        RenderSectionsAndOutline(result, surface);
        WriteList("styles", result.Styles, surface);
        WriteList("bookmarks", result.Bookmarks, surface);
        WriteList("fonts", result.Fonts, surface);
        RenderFieldsAndComments(result, surface);
        RenderImagesAndTables(result, surface);
        RenderProperties(result, surface);
    }

    private static void RenderSectionsAndOutline(
        DocumentInfoResult result,
        TableSurface surface)
    {
        if (result.Sections is { } sections && ResultText.Section(surface, "sections", sections.Count == 0))
        {
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

        if (result.Outline is { } outline && ResultText.Section(surface, "outline", outline.Count == 0))
        {
            var table = new TextTable("block", "level", "heading");
            foreach (OutlineItem item in outline)
            {
                table.AddRow(
                    TableText.Int(item.Block),
                    TableText.Int(item.HeadingLevel),
                    item.Text);
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }

    private static void RenderFieldsAndComments(
        DocumentInfoResult result,
        TableSurface surface)
    {
        if (result.Fields is { } fields && ResultText.Section(surface, "fields", fields.Count == 0))
        {
            var table = new TextTable("block", "type", "code", "result");
            foreach (FieldData field in fields)
            {
                table.AddRow(
                    Block(field.Block),
                    field.Type,
                    field.Code ?? "-",
                    field.Result ?? "-");
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Comments is { } comments && ResultText.Section(surface, "comments", comments.Count == 0))
        {
            var table = new TextTable("block", "author", "text");
            foreach (CommentData comment in comments)
            {
                table.AddRow(
                    Block(comment.Block),
                    comment.Author,
                    comment.Text);
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Revisions is { } revisions && ResultText.Section(surface, "revisions", revisions.Count == 0))
        {
            var table = new TextTable("revision", "block", "type", "author", "date", "text");
            foreach (RevisionData revision in revisions)
            {
                table.AddRow(
                    TableText.Int(revision.Revision),
                    Block(revision.Block),
                    revision.Type,
                    revision.Author,
                    revision.Date ?? "-",
                    revision.Text ?? "-");
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }

    private static void RenderImagesAndTables(
        DocumentInfoResult result,
        TableSurface surface)
    {
        if (result.Images is { } images && ResultText.Section(surface, "images", images.Count == 0))
        {
            var table = new TextTable("block", "name", "size");
            foreach (ImageData image in images)
            {
                table.AddRow(
                    Block(image.Block),
                    image.Name ?? "-",
                    $"{Points(image.Width)} x {Points(image.Height)} pt");
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Tables is { } tables && ResultText.Section(surface, "tables", tables.Count == 0))
        {
            var table = new TextTable("block", "rows", "columns", "style");
            foreach (TableData item in tables)
            {
                table.AddRow(
                    TableText.Int(item.Block),
                    TableText.Int(item.RowCount),
                    TableText.Int(item.ColumnCount),
                    item.Style ?? "-");
            }

            table.WriteTo(surface.Out, surface.Format);
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

    private static void WriteList(
        string label,
        IReadOnlyList<string>? values,
        TableSurface surface)
    {
        if (values is not null && ResultText.Section(surface, label, values.Count == 0))
        {
            surface.Out.WriteLine(string.Join(", ", values));
        }
    }

    // An item without a body block, such as one in a header, shows a dash.
    private static string Block(int? block) => block is { } value ? TableText.Int(value) : "-";

    private static string Points(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
