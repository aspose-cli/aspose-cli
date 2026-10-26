using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells convert</c>: a workbook saved in another document format.</summary>
internal static class CellsConvert
{
    public static CellsConvertResult Run(CellsSession session, CellsConvertRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);

        var saver = new CellsSavePipeline(session.Outputs, session.Loader);
        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password, request.TextImport);
        Workbook workbook = loaded.Workbook;
        if (request.Sheets is null && request.SheetsText is { } byName)
        {
            throw Sheets.SelectedByName(workbook, byName, range: false);
        }

        // Capture before saving: Workbook.FileFormat mutates to the target
        // format once the workbook is saved.
        InputInfo input = BuildSource(request.Input, loaded);

        SaveFormat saveFormat = CellsEngineFormats.Save(request.Output.Format.Id);
        IReadOnlyList<Worksheet>? selected = request.Sheets is { } selection
            ? [.. Sheets.Selected(workbook, request.SheetsText, range: false,
                () => selection.Resolve(workbook.Worksheets.Count, CellsDiagnostics.SheetNotFound, "sheet")).Distinct().Order().Select(position => workbook.Worksheets[position - 1])]
            : null;
        IReadOnlyList<int>? selectedSheets = null;

        // A modern sheet can overflow the legacy xls grid (65,536 rows × 256
        // columns); the engine's save then silently discards everything past it.
        // Detect it off the in-memory used range BEFORE saving so the warning
        // names the exact rows/columns that would be lost — never silent, as the
        // contract demands and as Excel itself warns when saving down to xls.
        Warning? dataTruncated = saver.DetectGridTruncation(workbook, saveFormat);

        if (selected is not null)
        {
            if (WorkbookSavePlan.WritesActiveSheetOnlyFor(request.Output.Format.Id))
            {
                if (selected.Count != 1)
                {
                    throw CliErrors.OptionInvalid(
                        CellsOptionNames.Sheets,
                        $"a '{request.Output.Format.Id}' output holds one sheet, but the selection holds {selected.Count}",
                        "Select one sheet, such as --sheets 2, or convert to a multi-sheet format.");
                }

                // Text formats export the active sheet.
                workbook.Worksheets.ActiveSheetIndex = selected[0].Index;
            }
            else if (CellsFormats.SheetScopeOf(request.Output.Format.Id) == CellsSheetScope.SheetSet)
            {
                selectedSheets = [.. selected.Select(static sheet => sheet.Index)];
            }
            else
            {
                throw new InvalidOperationException(
                    $"--sheets reached the engine for '{request.Output.Format.Id}', which ConvertCommand refuses.");
            }
        }

        if (WorkbookSavePlan.WritesActiveSheetOnlyFor(request.Output.Format.Id)
            && licenseState == LicenseState.Evaluation && selected is not null)
        {
            // The evaluation SDK writes the first sheet regardless of ActiveSheetIndex.
            Worksheet active = workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex];
            Worksheet first = workbook.Worksheets[0];
            if (active.Index != 0)
            { throw CellsErrors.TextExportEvaluationLimit(request.Output.Format.Id, active.Name, first.Name); }
        }

        Warning? headerOnlySkipped = null;
        IReadOnlyList<Worksheet> jsonSheets = [];
        if (CellsEngineFormats.WritesRecords(request.Output.Format.Id))
        {
            (jsonSheets, headerOnlySkipped) = JsonSheets(workbook);
        }

        // Say which rows are not table data: the rows a text input brought over as they are, or,
        // in a JSON output, the rows each sheet writes as records.
        IReadOnlyList<Warning> textLayout = CellsEngineFormats.WritesRecords(request.Output.Format.Id)
            ? TextTableLayout.JsonWarnings(jsonSheets, session.Budgets)
            : TextTableLayout.Warnings(loaded, session.Budgets);

        Warning? chartsSplit = CellsEngineFormats.IsPaginated(request.Output.Format.Id)
            ? PrintedPages.SplitChartsWarning(workbook, request.Output.Format.Id, selectedSheets)
            : null;
        int refsBefore = saver.CountRefFormulas(workbook);
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(request.Output.Format, licenseState,
            OutputProtection.For(request.Output.Format, request.EncryptPassword, loaded.Protection, request.Password),
            selectedSheets, request.ByteOrderMark);
        if (CellsEngineFormats.WritesRecords(request.Output.Format.Id))
        {
            savePlan = savePlan with { Options = JsonOptions(workbook, jsonSheets) };
        }

        loaded.RestoreActiveSheet(savePlan);
        Warning? sheetsDropped = savePlan.DetectSheetLoss(workbook);
        Warning? drawingsDropped = savePlan.DropDrawings(workbook);
        PublishedFile written = saver.Write(request.Output, workbook, singleSheet: selectedSheets is not null,
            savePlan.Encrypts, path => saver.Produce(workbook, savePlan, path));
        Warning? formulasBroken = saver.BuildBrokenFormulaWarning(
            refsBefore,
            saver.CountRefFormulas(workbook),
            request.Output.Format.Id);

        return new CellsConvertResult
        {
            Input = input,
            Outputs = [new(FileObjects.Output(written, request.Output.Format, savePlan.Encrypts))],
            Sheets = selected is null ? null : [.. selected.Select(static sheet => sheet.Name)],
            License = EnvelopeParts.License(licenseState),
            // Only a text export without --sheets writes one sheet chosen by default; the other
            // formats write every sheet.
            Warnings = EnvelopeParts.CombineWarnings([.. loaded.Resources.Warnings, loaded.CalculatedOnOpen,
                loaded.SkippedSheetWarning(selected is null && savePlan.WritesActiveSheetOnly, licenseState == LicenseState.Evaluation
                    // The evaluation SDK writes only the first sheet to a text format.
                    ? $"Evaluation mode exports only the first sheet to {request.Output.Format.Id}; converting another sheet needs an Aspose.Cells license, then --sheets with the sheet's position from inspect."
                    : "Pass --sheets with the sheet's position from inspect to convert another sheet."), sheetsDropped, drawingsDropped, headerOnlySkipped, dataTruncated, formulasBroken, chartsSplit,
                .. textLayout]),
        };
    }

    /// <summary>
    /// The JSON export of the sheets <see cref="JsonSheets"/> chose. It keeps one member per sheet
    /// whenever the workbook has more than one sheet of its own, even when only one is written.
    /// </summary>
    private static JsonSaveOptions JsonOptions(Workbook workbook, IReadOnlyList<Worksheet> written)
    {
        int own = workbook.Worksheets.Cast<Worksheet>().Count(static sheet => !CellsEvaluation.IsWarningSheet(sheet));
        var options = new JsonSaveOptions { AlwaysExportAsJsonObject = own > 1 };
        if (written.Count > 0 && written.Count < workbook.Worksheets.Count)
        {
            options.SheetIndexes = [.. written.Select(static sheet => sheet.Index)];
        }

        return options;
    }

    /// <summary>
    /// The sheets a JSON export writes: the workbook's own sheets, without the evaluation warning
    /// sheets, which are no data of the workbook, and without a sheet whose data is one row, which
    /// the engine takes for a header and writes as invalid JSON (CELLS-JSON-HEADER-ONLY). Such a
    /// sheet has no records to write and is named in the warning; a workbook with no other sheet
    /// is refused before anything is written.
    /// </summary>
    private static (IReadOnlyList<Worksheet> Written, Warning? Skipped) JsonSheets(Workbook workbook)
    {
        Worksheet[] own = [.. workbook.Worksheets.Cast<Worksheet>().Where(static sheet => !CellsEvaluation.IsWarningSheet(sheet))];
        Worksheet[] headerOnly = [.. own.Where(static sheet => sheet.Cells.MaxDataRow >= 0 && sheet.Cells.MinDataRow == sheet.Cells.MaxDataRow)];
        string names = string.Join(", ", headerOnly.Select(static sheet => $"'{sheet.Name}'"));
        if (headerOnly.Length > 0 && headerOnly.Length == own.Length)
        {
            throw CliErrors.EngineDefect(
                $"Aspose.Cells writes invalid JSON for a sheet whose data is a single header row, as in {names}, and the workbook has no other sheet to write; nothing was written.",
                "Add at least one data row below the header, or convert to csv, which writes the header alone.");
        }

        bool one = headerOnly.Length == 1;
        Warning? skipped = headerOnly.Length == 0 ? null : new Warning(CellsDiagnostics.SheetsDropped,
            $"{(one ? "Worksheet" : "Worksheets")} {names} {(one ? "was" : "were")} not exported: {(one ? "it holds" : "each holds")} "
            + "only a header row and no records, which Aspose.Cells cannot write as JSON.")
        {
            Hint = "Convert the sheet to csv with --sheets and its position from inspect to keep its column names, or add data rows below its header.",
            AffectsCompleteness = true,
        };
        return ([.. own.Except(headerOnly)], skipped);
    }
}
