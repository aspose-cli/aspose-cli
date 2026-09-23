using System.Globalization;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Cells.Output;

/// <summary>Human renderers for the spreadsheet result families.</summary>
internal static class CellsRenderers
{
    public static void Render(WorkbookInfoResult info, TableSurface surface)
    {
        WorkbookSummary workbook = info.Workbook;
        surface.Out.WriteLine($"{workbook.Name} ({info.Source.Format}, {TableText.Bytes(info.Source.SizeBytes)})");
        surface.Out.WriteLine(
            $"sheets: {workbook.SheetCount}   vba: {TableText.YesNo(workbook.HasVba)}   defined names: {workbook.DefinedNameCount}");
        surface.Out.WriteLine();

        var table = new TextTable("name", "index", "used range", "rows", "cols", "hidden", "charts", "pivots");
        foreach (SheetInfo sheet in workbook.Sheets)
        {
            table.AddRow(
                sheet.Name,
                TableText.Int(sheet.Index),
                sheet.UsedRange ?? "-",
                TableText.Int(sheet.RowCount),
                TableText.Int(sheet.ColumnCount),
                TableText.YesNo(sheet.Hidden),
                TableText.Int(sheet.ChartCount),
                TableText.Int(sheet.PivotTableCount));
        }

        table.WriteTo(surface.Out, surface.Format);

        foreach (SheetInfo sheet in workbook.Sheets)
        {
            if (sheet.Preview is not { Count: > 0 } preview)
            {
                continue;
            }

            surface.Out.WriteLine();
            surface.Out.WriteLine($"preview {sheet.Name}:");
            foreach (IReadOnlyList<string?> row in preview)
            {
                surface.Out.WriteLine("  " + string.Join(" | ", row.Select(static value => value ?? string.Empty)));
            }
        }
    }

    public static void Render(WorkbookReadResult read, TableSurface surface)
    {
        SheetProjection sheet = read.Sheet;
        var headline = new List<string> { sheet.Name };
        if (sheet.Window is { } window)
        {
            headline.Add(window);
        }

        if (sheet.UsedRange is { } used)
        {
            headline.Add($"(used {used})");
        }

        surface.Out.WriteLine(string.Join(' ', headline));

        if (sheet.Cells is { Count: > 0 } cells && sheet.Window is not null)
        {
            RangeRef range = A1.ParseRange(sheet.Window).Range;

            string[] headers = new string[range.ColumnCount + 1];
            headers[0] = string.Empty;
            for (int column = 0; column < range.ColumnCount; column++)
            {
                headers[column + 1] = A1.ColumnName(range.Start.Column + column);
            }

            var table = new TextTable(headers);
            for (int row = 0; row < cells.Count; row++)
            {
                string[] line = new string[range.ColumnCount + 1];
                line[0] = TableText.Int(range.Start.Row + row + 1);
                for (int column = 0; column < cells[row].Count; column++)
                {
                    line[column + 1] = FormatCellValue(cells[row][column]);
                }

                table.AddRow(line);
            }

            table.WriteTo(surface.Out, surface.Format);
        }
        else if (sheet.Truncated)
        {
            surface.Out.WriteLine("cell data omitted: the sheet exceeds the cell budget");
        }

        if (read.Next is { } next)
        {
            surface.Out.WriteLine($"next: {next}");
        }
    }

    public static void Render(ConvertResult convert, TableSurface surface) =>
        ResultText.Produced(surface, convert.Output, convert.Sheet is null ? null : $"sheet {convert.Sheet}");

    public static void Render(RenderResult render, TableSurface surface)
    {
        if (render.Outputs is { } outputs)
        {
            // The --all-sheets shape: the per-sheet list IS the result, so a
            // human sees every produced file, not just the first-sheet summary.
            string dpi = render.Dpi is { } value ? $", {TableText.Int(value)} dpi" : string.Empty;
            surface.Out.WriteLine($"rendered {TableText.Int(outputs.Count)} sheet(s) ({render.Output.Format}{dpi})");
            foreach (SheetRenderOutput output in outputs)
            {
                surface.Out.WriteLine($"  {output.Sheet}: {output.Path} ({TableText.Bytes(output.SizeBytes)})");
            }

            return;
        }

        ResultText.Produced(surface, render.Output, DescribeRender(render));
    }

    public static void Render(EditResult edit, TableSurface surface)
    {
        ResultText.Edit(surface, edit.DryRun, edit.Output, edit.Applied, edit.Backup);
        if (edit.Verification is { } verification)
        {
            surface.Out.WriteLine(
                $"verification: {(verification.Ok ? "ok" : "needs attention")}; " +
                $"{verification.DirectChanges.Count} direct, " +
                $"{verification.FormulaResultChanges.Count} formula-result, " +
                $"{verification.FormulaErrors.Count} formula error(s)");
            foreach (VerificationIssue issue in verification.Issues ?? [])
            {
                surface.Out.WriteLine($"  {issue.Code}: {issue.Message}");
            }
        }
    }

    public static void Render(CreateResult create, TableSurface surface)
    {
        ResultText.Produced(surface, create.Output, $"sheets: {string.Join(", ", create.Sheets)}");
    }

    public static void Render(DiffResult diff, TableSurface surface)
    {
        if (diff.Identical)
        {
            surface.Out.WriteLine($"identical: {diff.Left.Path} == {diff.Right.Path} (within scope)");
            return;
        }

        DiffSummary summary = diff.Summary;
        surface.Out.WriteLine(
            $"{diff.Left.Path} vs {diff.Right.Path}: {summary.CellsDiffering} cell(s) differ across " +
            $"{summary.SheetsModified} sheet(s); +{summary.SheetsAdded} -{summary.SheetsRemoved} sheet(s)");

        if (diff.Sheets is { Count: > 0 } sheets)
        {
            surface.Out.WriteLine();
            var table = new TextTable("sheet", "status", "cells");
            foreach (SheetDiff sheet in sheets)
            {
                table.AddRow(sheet.Name, sheet.Status, sheet.Cells is { } changed ? TableText.Int(changed.Count) : "-");
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (diff.Truncated)
        {
            surface.Out.WriteLine("(cell list truncated by the diff budget)");
        }
    }

    public static void Render(SearchResult search, TableSurface surface)
    {
        surface.Out.WriteLine($"{search.Hits.Count} hit(s) for '{search.Pattern}' in {search.Source.Path}");

        if (search.Hits.Count > 0)
        {
            surface.Out.WriteLine();
            var table = new TextTable("sheet", "cell", "value");
            foreach (SearchHit hit in search.Hits)
            {
                table.AddRow(hit.Sheet, hit.Cell, hit.Value);
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        if (search.Truncated && search.Hint is { } hint)
        {
            surface.Out.WriteLine($"({hint})");
        }
    }

    private static string FormatCellValue(CellData cell) => cell.V switch
    {
        null => string.Empty,
        bool value => value ? "TRUE" : "FALSE",
        double value => value.ToString(CultureInfo.InvariantCulture),
        string value => value,
        var value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static string DescribeRender(RenderResult render)
    {
        var parts = new List<string>(3) { $"sheet {render.Sheet}" };
        if (render.Range is { } range)
        {
            parts.Add($"range {range}");
        }

        if (render.Dpi is { } dpi)
        {
            parts.Add(TableText.Int(dpi) + " dpi");
        }

        return string.Join(", ", parts);
    }
}
