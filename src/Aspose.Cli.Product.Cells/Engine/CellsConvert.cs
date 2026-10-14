using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
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
        // The imported rows come over as they are; say which of them are not table data.
        IReadOnlyList<Warning> textLayout = TextTableLayout.Warnings(loaded, session.Budgets);

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
                        "--sheets",
                        $"a '{request.Output.Format.Id}' output holds one sheet, but the selection holds {selected.Count}",
                        "Select one sheet, such as --sheets 2, or convert to a multi-sheet format.");
                }

                // Text formats export the active sheet.
                workbook.Worksheets.ActiveSheetIndex = selected[0].Index;
            }
            else if (request.Output.Format.Id is "pdf")
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

        Warning? chartsSplit = request.Output.Format.Id is "pdf" ? PrintedPages.SplitChartsWarning(workbook, selectedSheets) : null;
        int refsBefore = saver.CountRefFormulas(workbook);
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(request.Output.Format, licenseState,
            request.EncryptPassword, loaded.IsEncrypted ? request.Password : null, selectedSheets, request.ByteOrderMark);
        loaded.RestoreActiveSheet(savePlan);
        Warning? sheetsDropped = savePlan.DetectSheetLoss(workbook);
        PublishedFile written = saver.Write(request.Output.Path, request.Output.Overwrite, workbook, singleSheet: selectedSheets is not null,
            savePlan.Encrypts, path => saver.Produce(workbook, savePlan, path));
        Warning? formulasBroken = saver.BuildBrokenFormulaWarning(
            refsBefore,
            saver.CountRefFormulas(workbook),
            request.Output.Format.Id);

        return new CellsConvertResult
        {
            Input = input,
            Output = FileObjects.Output(written, request.Output.Format, savePlan.Encrypts),
            Sheets = selected is null ? null : [.. selected.Select(static sheet => sheet.Name)],
            License = EnvelopeParts.License(licenseState),
            // Only a text export without --sheets writes one sheet chosen by default; the other
            // formats write every sheet.
            Warnings = EnvelopeParts.CombineWarnings([loaded.Resources.CoverageWarning, loaded.CalculatedOnOpen,
                loaded.SkippedSheetWarning(selected is null && savePlan.WritesActiveSheetOnly, "Pass --sheets with the sheet's position from inspect to convert another sheet."), sheetsDropped, dataTruncated, formulasBroken, chartsSplit,
                .. textLayout]),
        };
    }
}
