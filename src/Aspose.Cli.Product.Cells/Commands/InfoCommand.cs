using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells inspect</c> — the first step of the projection ladder:
/// structure and metadata, never bulk data.
/// </summary>
internal static class InfoCommand
{
    private const int MaxPreviewRows = 100;

    public static CommandDefinition<InfoRequest, WorkbookInfoResult> Create()
    {
        var preview = new PreviewOption($"display values of the first --preview-rows rows and up to {SheetInfo.PreviewColumns} columns of each sheet, from A1");
        var previewRows = new Option<int>("--preview-rows")
        {
            Description = $"Number of preview rows per sheet (1-{MaxPreviewRows}).",
            DefaultValueFactory = _ => 5,
        };
        var detail = new DetailOption(InfoDetails.All, new Dictionary<string, string>
        {
            [InfoDetails.Names] = "defined names",
            [InfoDetails.Errors] = "formula-error scan",
            [InfoDetails.Layout] = "frozen panes, outline groups, filter, print area and page setup of each sheet",
        });
        return new(
            "inspect",
            "Show structure and metadata of a workbook.",
            new CommandTraits { Input = CellsTraits.Workbook("Workbook to inspect (xlsx, xlsm, xlsb, xls, ods, csv, ...).") },
            [.. preview.Options, previewRows, .. detail.Options],
            (parse, standard) =>
            {
                int rows = parse.GetValue(previewRows);
                OptionGuards.EnsureInRange("--preview-rows", rows, 1, MaxPreviewRows,
                    "Pass a smaller sample size; previews are meant to be cheap to read.");
                return new InfoRequest
                {
                    Input = standard.Input,
                    IncludePreview = preview.Read(parse),
                    PreviewRows = rows,
                    Details = detail.Read(parse),
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "cells inspect book.xlsx --output json",
                "cells inspect book.xlsx --detail charts names --preview",
            ],
        };
    }

    internal static void Table(WorkbookInfoResult info, TableSurface surface)
    {
        WorkbookSummary workbook = info.Workbook;
        surface.Out.WriteLine($"{workbook.Name} ({info.Source.Format}, {TableText.Bytes(info.Source.SizeBytes)})");
        surface.Out.WriteLine(
            $"sheets: {workbook.SheetCount}   vba: {TableText.YesNo(workbook.HasVba)}   defined names: {workbook.DefinedNameCount}   encrypted: {TableText.YesNo(info.Source.Encrypted == true)}   structure protected: {TableText.YesNo(workbook.StructureProtected)}");
        surface.Out.WriteLine();

        var table = new TextTable("name", "position", "used range", "rows", "cols", "hidden", "protected", "charts", "pivots");
        foreach (SheetInfo sheet in workbook.Sheets)
        {
            table.AddRow(
                sheet.Name,
                TableText.Int(sheet.Position),
                sheet.UsedRange ?? "-",
                TableText.Int(sheet.RowCount),
                TableText.Int(sheet.ColumnCount),
                TableText.YesNo(sheet.Hidden),
                TableText.YesNo(sheet.Protected),
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

        Details(workbook, surface);
    }

    /// <summary>Each list --detail requested, as a table under its own heading.</summary>
    private static void Details(WorkbookSummary workbook, TableSurface surface)
    {
        ResultText.Table(surface, "names", workbook.DefinedNames, ["name", "refers to"],
            static name => [name.Name, name.RefersTo]);
        ResultText.Table(surface, "formula errors", workbook.FormulaErrors, ["sheet", "cell", "error"],
            static error => [error.Sheet, error.Cell, error.Error]);
        ResultText.Table(surface, "validations", workbook.Validations, ["sheet", "range", "type"],
            static validation => [validation.Sheet, validation.Range, validation.Type]);
        ResultText.Table(surface, "fonts", workbook.Fonts, ["font"], static font => [font]);
        ResultText.Table(surface, "tables", workbook.Tables, ["sheet", "name", "range"],
            static item => [item.Sheet, item.Name, item.Range]);
        ResultText.Table(surface, "charts", workbook.Charts, ["sheet", "index", "name", "type"],
            static chart => [chart.Sheet, TableText.Int(chart.Index), chart.Name, chart.Type]);
        ResultText.Table(surface, "pivots", workbook.Pivots, ["sheet", "name", "range"],
            static pivot => [pivot.Sheet, pivot.Name, pivot.Range]);
        ResultText.Table(surface, "layouts", workbook.Layouts,
            ["sheet", "freeze", "row groups", "column groups", "filter", "print area", "titles", "page", "header", "footer"],
            static layout =>
            [
                layout.Sheet,
                layout.FreezePanes ?? "-",
                Groups(layout.RowGroups, static group => (Span: $"{group.From}:{group.To}", group.Level, group.Collapsed)),
                Groups(layout.ColumnGroups, static group => (Span: $"{group.From}:{group.To}", group.Level, group.Collapsed)),
                layout.AutoFilter ?? "-",
                layout.PrintArea ?? "-",
                string.Join(' ', new[] { layout.TitleRows, layout.TitleColumns }.OfType<string>().DefaultIfEmpty("-")),
                layout.Orientation + (layout.Scale is { } scale
                    ? $" {scale}%"
                    : $" fit {layout.FitToWidth}x{layout.FitToHeight}"),
                layout.Header ?? "-",
                layout.Footer ?? "-",
            ]);
    }

    // Outline groups as "2:20 L1, 3:4 L2 collapsed".
    private static string Groups<T>(IReadOnlyList<T>? groups, Func<T, (string Span, int Level, bool Collapsed)> describe) =>
        groups is null
            ? "-"
            : string.Join(", ", groups.Select(describe).Select(static group =>
                $"{group.Span} L{group.Level}{(group.Collapsed ? " collapsed" : string.Empty)}"));
}
