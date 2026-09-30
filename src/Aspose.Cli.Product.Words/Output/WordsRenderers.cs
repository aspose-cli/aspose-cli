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
        surface.Out.WriteLine($"revisions: {document.RevisionCount}   protection: {document.Protection}   signed: {TableText.YesNo(document.Signed)}");
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
            foreach (string issue in verification.Issues)
            {
                surface.Out.WriteLine($"  issue: {issue}");
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
                table.AddRow(sample.Type, sample.Text);
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
            table.AddRow(TableText.Int(hit.Block), TableText.Int(hit.Section), hit.Scope, hit.Snippet);
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
        if (result.Sections is { Count: > 0 } sections)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("sections:");
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
        }

        if (result.Outline is { Count: > 0 } outline)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("outline:");
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
        if (result.Fields is { Count: > 0 } fields)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("fields:");
            var table = new TextTable("block", "type", "code", "result");
            foreach (FieldData field in fields)
            {
                table.AddRow(
                    TableText.Int(field.Block),
                    field.Type,
                    field.Code ?? "-",
                    field.Result ?? "-");
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Comments is { Count: > 0 } comments)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("comments:");
            var table = new TextTable("block", "author", "text");
            foreach (CommentData comment in comments)
            {
                table.AddRow(
                    comment.Block is { } block ? TableText.Int(block) : "-",
                    comment.Author,
                    comment.Text);
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Revisions is { Count: > 0 } revisions)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("revisions:");
            var table = new TextTable("block", "type", "author", "date", "text");
            foreach (RevisionData revision in revisions)
            {
                table.AddRow(
                    revision.Block is { } block ? TableText.Int(block) : "-",
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
        if (result.Images is { Count: > 0 } images)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("images:");
            var table = new TextTable("block", "name", "size");
            foreach (ImageData image in images)
            {
                table.AddRow(
                    TableText.Int(image.Block),
                    image.Name ?? "-",
                    $"{Points(image.Width)} x {Points(image.Height)} pt");
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (result.Tables is { Count: > 0 } tables)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("tables:");
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
        if (result.Properties is { Count: > 0 } properties)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine("properties:");
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
        if (values is not { Count: > 0 })
        {
            return;
        }

        surface.Out.WriteLine();
        surface.Out.WriteLine($"{label}: {string.Join(", ", values)}");
    }

    private static string Points(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
