using System.CommandLine;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>
/// <c>cells inspect</c> — the first step of the projection ladder:
/// structure and metadata, never bulk data.
/// </summary>
internal static class InspectCommand
{
    private const int MaxSampleRows = 100;

    public static CommandDefinition<CellsInspectRequest, CellsInspectResult> Create()
    {
        var sample = new SampleOption($"display values of the first --sample-rows rows and up to {CellsSheetInfo.PreviewColumns} columns of each sheet, from A1");
        var sampleRows = new Option<int>("--sample-rows")
        {
            Description = "Number of sample rows per sheet. Only with --sample.",
            DefaultValueFactory = _ => 5,
        }.InRange(1, MaxSampleRows);
        var detail = new DetailOption(CellsInspectDetails.All, new Dictionary<string, string>
        {
            [CellsInspectDetails.Names] = "defined names",
            [CellsInspectDetails.Errors] = "formula-error scan",
            [CellsInspectDetails.ConditionalFormats] = "conditional-formatting rules",
            [CellsInspectDetails.Layout] = "view, tab color, frozen panes, sizes, outline groups, filter, protection, print area and page setup of each sheet",
        });
        return new(
            "inspect",
            "Show structure and metadata of a workbook.",
            new CommandTraits { Input = CellsInputs.Workbook("Workbook to inspect (xlsx, xlsm, xlsb, xls, ods, csv, ...).") },
            [.. sample.Options, sampleRows, .. detail.Options],
            (parse, standard) =>
            {
                int rows = parse.GetInRange(sampleRows, "Pass a smaller sample size; samples are meant to be cheap to read.");
                if (!sample.Read(parse) && parse.GetResult(sampleRows) is { Implicit: false })
                {
                    throw CliErrors.OptionInvalid(
                        sampleRows.Name,
                        "--sample-rows sizes the sample, which only --sample includes",
                        "Add --sample to include the sample, or drop --sample-rows.");
                }
                return new CellsInspectRequest
                {
                    Input = standard.Input,
                    IncludePreview = sample.Read(parse),
                    SampleRows = rows,
                    Details = detail.Read(parse),
                    Password = standard.InputPassword,
                };
            },
            Table)
        {
            Examples =
            [
                "cells inspect book.xlsx --output-format json",
                "cells inspect book.xlsx --detail charts names --sample",
            ],
            Links =
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "what each Cells command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }

    internal static void Table(CellsInspectResult info, TableSurface surface)
    {
        CellsWorkbookSummary workbook = info.Workbook;
        ResultText.Input(surface, info.Input);
        surface.Out.WriteLine(
            $"sheets: {workbook.SheetCount}   macros: {TableText.YesNo(workbook.HasMacros)}   defined names: {workbook.DefinedNameCount}   encrypted: {TableText.YesNo(info.Input.Encrypted == true)}   structure protected: {TableText.YesNo(workbook.StructureProtected)}");
        surface.Out.WriteLine();

        var table = new TextTable("name", "position", "used range", "rows", "columns", "hidden", "protected", "charts", "pivots");
        foreach (CellsSheetInfo sheet in workbook.Sheets)
        {
            table.AddRow(
                sheet.Name,
                TableText.Int(sheet.Position),
                sheet.UsedRange,
                TableText.Int(sheet.RowCount),
                TableText.Int(sheet.ColumnCount),
                TableText.YesNo(sheet.Hidden),
                TableText.YesNo(sheet.Protected),
                TableText.Int(sheet.ChartCount),
                TableText.Int(sheet.PivotTableCount));
        }

        table.WriteTo(surface.Out, surface.Format);

        foreach (CellsSheetInfo sheet in workbook.Sheets)
        {
            if (sheet.Preview is not { Count: > 0 } preview)
            {
                continue;
            }

            surface.Out.WriteLine();
            surface.Out.WriteLine($"preview {sheet.Name}:");
            foreach (IReadOnlyList<string?> row in preview)
            {
                surface.Out.WriteLine("  " + string.Join(" | ", row.Select(static value => string.IsNullOrEmpty(value) ? TableText.Missing : value)));
            }
        }

        Details(workbook, surface);
    }

    /// <summary>Each list --detail requested, as a table under its own heading.</summary>
    private static void Details(CellsWorkbookSummary workbook, TableSurface surface)
    {
        ResultText.Table(surface, "names", workbook.DefinedNames, ["name", "refers to"],
            static name => [name.Name, name.RefersTo]);
        ResultText.Table(surface, "formula errors", workbook.FormulaErrors, ["sheet", "cell", "error"],
            static error => [error.Sheet, error.Cell, error.Error]);
        ResultText.Table(surface, "validations", workbook.Validations, ["sheet", "range", "kind"],
            static validation => [validation.Sheet, validation.Range, validation.Kind]);
        ResultText.Table(surface, "fonts", workbook.Fonts, ["font"], static font => [font]);
        ResultText.Table(surface, "tables", workbook.Tables, ["sheet", "name", "range"],
            static item => [item.Sheet, item.Name, item.Range]);
        ResultText.Table(surface, "charts", workbook.Charts, ["sheet", "index", "name", "kind"],
            static chart => [chart.Sheet, TableText.Int(chart.Index), chart.Name, chart.Kind]);
        ResultText.Table(surface, "pivots", workbook.Pivots, ["sheet", "name", "range", "source"],
            static pivot => [pivot.Sheet, pivot.Name, pivot.Range, pivot.SourceRange]);
        ResultText.Table(surface, "conditional formats", workbook.ConditionalFormats, ["sheet", "range", "kind"],
            static format => [format.Sheet, format.Range, format.Rule.Kind]);
        ResultText.Table(surface, "sparklines", workbook.Sparklines, ["sheet", "kind", "data", "location"],
            static group => [group.Sheet, group.Kind, group.DataRange, group.Location]);
        ResultText.Table(surface, "images", workbook.Images, ["sheet", "index", "at", "width", "height"],
            static image => [image.Sheet, TableText.Int(image.Index), image.At, TableText.Points(image.Width), TableText.Points(image.Height)]);
        ResultText.Table(surface, "comments", workbook.Comments, ["sheet", "cell", "author", "text"],
            static comment => [comment.Sheet, comment.Cell, comment.Author, comment.Text]);
        ResultText.Table(surface, "hyperlinks", workbook.Hyperlinks, ["sheet", "range", "link", "display"],
            static link => [link.Sheet, link.Range, link.Url ?? link.Target, link.Display]);
        ResultText.Table(surface, "layouts", workbook.Layouts,
            ["sheet", "freeze", "row groups", "column groups", "filter", "print area", "titles", "page", "header", "footer"],
            static layout =>
            [
                layout.Sheet,
                layout.FreezePanes?.Cell,
                Groups(layout.RowGroups, static group => (Span: $"{group.First}:{group.Last}", group.Level, group.Collapsed)),
                Groups(layout.ColumnGroups, static group => (Span: $"{group.First}:{group.Last}", group.Level, group.Collapsed)),
                layout.Autofilter?.Range,
                layout.PrintArea?.Range,
                string.Join(' ', new[] { layout.PrintArea?.TitleRows, layout.PrintArea?.TitleColumns }.OfType<string>()),
                $"{layout.PageSetup.PageSize.Preset ?? "custom"} {layout.PageSetup.Orientation}"
                    + (layout.PageSetup.ScalePercent is { } scale
                        ? $" {scale}%"
                        : $" fit {layout.PageSetup.FitToWidth}x{layout.PageSetup.FitToHeight}"),
                layout.PageSetup.Header,
                layout.PageSetup.Footer,
            ]);
    }

    // Outline groups as "2:20 L1, 3:4 L2 collapsed".
    private static string? Groups<T>(IReadOnlyList<T>? groups, Func<T, (string Span, int Level, bool Collapsed)> describe) =>
        groups is null
            ? null
            : string.Join(", ", groups.Select(describe).Select(static group =>
                $"{group.Span} L{group.Level}{(group.Collapsed ? " collapsed" : string.Empty)}"));
}
