using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>
/// Recognizes what Aspose.Cells evaluation mode writes into a workbook and its exports: the
/// warning sheet each evaluation save adds and activates (<see cref="CellsEvaluation.IsWarningSheet"/>),
/// licensed or not, and the notice an unlicensed text or JSON export writes after the data, where
/// a reader takes it for data. A workbook format keeps every sheet; a page format and an image
/// print the visible ones; a text format exports one sheet.
/// </summary>
/// <remarks>
/// The profile sees the workbook and the output format, not which sheets an output holds, so
/// the Cells handlers decide what to hand the write pipeline:
/// <list type="bullet">
/// <item>A PDF of one sheet (<c>convert --sheet</c>) is published without the workbook, since the
/// content sheet it prints carries none of the workbook's marks.</item>
/// <item>A render of one sheet is published with the workbook only when that sheet is a warning
/// sheet; the marks then name every visible warning sheet of the workbook.</item>
/// <item>A render of every sheet (<c>--sheets all</c>) names every visible warning sheet, as the
/// images do.</item>
/// <item>A CSV, TSV or Markdown export names the evaluation notice only; the active warning sheet
/// it would export is skipped by the command before (<c>ACTIVE_SHEET_SKIPPED</c>).</item>
/// </list>
/// </remarks>
internal sealed class CellsEvaluationProfile : IEvaluationProfile<Workbook>
{
    // How many files Aspose.Cells evaluation mode opens in one process before it refuses more.
    private const int OpenLimit = 100;

    public string Engine => "Aspose.Cells";

    /// <summary>
    /// Evaluation mode refuses to open a file once its process has opened <see cref="OpenLimit"/>,
    /// which only a long-lived process, such as a preview's render worker, reaches.
    /// </summary>
    public EvaluationLimitFact? RecognizeLimit(Exception exception) => IsOpenLimit(exception)
        ? new EvaluationLimitFact($"opens at most {OpenLimit} files in one process", OpenLimit, "files", "run the command again in a new process")
        : null;

    /// <summary>Whether <paramref name="exception"/> is evaluation mode refusing to open another file.</summary>
    internal static bool IsOpenLimit(Exception exception) => exception is CellsException { Code: ExceptionType.License };

    public EvaluationMarks Inspect(Workbook workbook) =>
        new([.. WarningSheets(workbook, visibleOnly: false).Select(sheet => sheet.Index == workbook.Worksheets.ActiveSheetIndex
            ? $"the evaluation warning sheet '{sheet.Name}', which is the active sheet"
            : $"the evaluation warning sheet '{sheet.Name}'")]);

    public EvaluationMarks Inspect(Workbook workbook, string format) => CellsEngineFormats.EvaluationMarksOf(format) switch
    {
        CellsEvaluationMarkKind.DataRow => Notice(workbook, $"as the last row of the {format} output, after the data"),
        CellsEvaluationMarkKind.MarkdownHeading => Notice(workbook, $"as a heading after the table in the {format} output"),
        // The json export leaves the evaluation warning sheets out (CellsConvert.JsonOptions).
        CellsEvaluationMarkKind.JsonRecord => Notice(workbook, $"in the {format} output, as a " + "{\"watermark\": ...} record after the records of each sheet that has data rows"),
        CellsEvaluationMarkKind.Pages =>
            new([.. WarningSheets(workbook, visibleOnly: true).Select(static sheet =>
                $"the pages of the evaluation warning sheet '{sheet.Name}', which hold only the notice '{CellsEvaluation.Notice}...'")]),
        // A workbook format, and an id the product does not declare.
        _ => Inspect(workbook),
    };

    private static IEnumerable<Worksheet> WarningSheets(Workbook workbook, bool visibleOnly) =>
        workbook.Worksheets.Cast<Worksheet>().Where(sheet => (!visibleOnly || sheet.IsVisible) && CellsEvaluation.IsWarningSheet(sheet));

    /// <summary>The notice an unlicensed export writes into a data output as content.</summary>
    private static EvaluationMarks Notice(Workbook workbook, string where) =>
        workbook.IsLicensed ? EvaluationMarks.None : new([$"the notice '{CellsEvaluation.Notice}...' {where}, which is not workbook data"]);
}
