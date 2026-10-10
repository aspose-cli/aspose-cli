using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells convert</c>: a workbook saved in another document format.</summary>
internal static class CellsConvert
{
    public static ConvertResult Run(CellsSession session, ConvertRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Input);

        var saver = new CellsSavePipeline(session.Outputs, session.Loader);
        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Input, request.Password, request.TextImport);
        Workbook workbook = loaded.Workbook;

        // Capture before saving: Workbook.FileFormat mutates to the target
        // format once the workbook is saved.
        SourceInfo input = BuildSource(request.Input, workbook);
        // The imported rows come over as they are; say which of them are not table data.
        IReadOnlyList<Warning> textLayout = TextTableLayout.Warnings(loaded, session.Budgets);

        SaveFormat saveFormat = CellsEngineFormats.Save(request.Output.Format.Id);
        string? resolvedSheetName = null;
        int? selectedSheet = null;

        // A modern sheet can overflow the legacy xls grid (65,536 rows × 256
        // columns); the engine's save then silently discards everything past it.
        // Detect it off the in-memory used range BEFORE saving so the warning
        // names the exact rows/columns that would be lost — never silent, as the
        // contract demands and as Excel itself warns when saving down to xls.
        Warning? dataTruncated = saver.DetectGridTruncation(workbook, saveFormat);

        if (request.SheetName is not null)
        {
            Worksheet sheet = Sheets.Resolve(workbook, request.SheetName);
            resolvedSheetName = sheet.Name;

            if (WorkbookSavePlan.WritesActiveSheetOnlyFor(request.Output.Format.Id))
            {
                // Text formats export the active sheet.
                workbook.Worksheets.ActiveSheetIndex = sheet.Index;
            }
            else if (request.Output.Format.Id is "pdf")
            {
                selectedSheet = sheet.Index;
            }
            else
            {
                throw new InvalidOperationException(
                    $"--sheet reached the engine for '{request.Output.Format.Id}', which ConvertCommand refuses.");
            }
        }

        if (WorkbookSavePlan.WritesActiveSheetOnlyFor(request.Output.Format.Id)
            && licenseState == LicenseState.Evaluation && request.SheetName is not null)
        {
            // The evaluation SDK writes the first sheet regardless of ActiveSheetIndex.
            Worksheet active = workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex];
            Worksheet first = workbook.Worksheets[0];
            if (active.Index != 0)
            { throw CellsErrors.TextExportEvaluationLimit(request.Output.Format.Id, active.Name, first.Name); }
        }

        Warning? chartsSplit = request.Output.Format.Id is "pdf" ? PrintedPages.SplitChartsWarning(workbook, selectedSheet) : null;
        int refsBefore = saver.CountRefFormulas(workbook);
        WorkbookSavePlan savePlan = WorkbookSavePlan.Create(request.Output.Format, licenseState,
            request.EncryptPassword, loaded.IsEncrypted ? request.Password : null, selectedSheet, request.ByteOrderMark);
        loaded.RestoreActiveSheet(savePlan);
        Warning? sheetsDropped = savePlan.DetectSheetLoss(workbook);
        long sizeBytes = saver.Write(request.Output.Path, request.Output.Overwrite, workbook, singleSheet: selectedSheet is not null,
            path => saver.Produce(workbook, savePlan, path));
        Warning? formulasBroken = saver.BuildBrokenFormulaWarning(
            refsBefore,
            saver.CountRefFormulas(workbook),
            request.Output.Format.Id);

        return new ConvertResult
        {
            Input = input,
            Output = new OutputInfo
            {
                Path = request.Output.Path,
                Format = request.Output.Format.Id,
                SizeBytes = sizeBytes,
                Encrypted = savePlan.Encrypts,
            },
            Sheet = resolvedSheetName,
            License = EnvelopeParts.License(licenseState),
            // Only a text export without --sheet writes one sheet chosen by default; the other
            // formats write every sheet.
            Warnings = EnvelopeParts.CombineWarnings([loaded.Resources.CoverageWarning, loaded.CalculatedOnOpen,
                loaded.SkippedSheetWarning(request.SheetName is null && savePlan.WritesActiveSheetOnly), sheetsDropped, dataTruncated, formulasBroken, savePlan.EncryptionWarning, chartsSplit,
                .. textLayout]),
        };
    }
}
