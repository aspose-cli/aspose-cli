using System.Globalization;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Names, before a save, the content of the sheets it writes that the output format does not
/// hold as the workbook does (<see cref="CellsEngineFormat.Leaves"/>), for every command that
/// saves (<see cref="CellsSavePipeline.ProduceReporting"/>): one
/// <c>LOSSY_CONVERSION</c> per kind of content the sheets have, with how much of it each sheet
/// has, naming its kind in <c>details.content</c> (<see cref="ContentLoss"/>). A workbook
/// without such content warns of nothing. A table of cells (csv, tsv, md) writes one sheet
/// (<see cref="WorkbookSavePlan.TextSheet"/>), so only that sheet's content counts; the sheets it
/// leaves out are <c>SHEETS_DROPPED</c>. The content the workbook already had stays in it and in
/// its other outputs, so its warning does not affect completeness, as speaker notes an export
/// leaves out do not; content an edit added or changed that its output leaves out is in no file,
/// so that warning does, and fails a verified edit (<see cref="WorkbookSavePlan.InputContent"/>).
/// The evaluation warning sheets are no content of the workbook and are skipped.
/// </summary>
internal static class UnwrittenContent
{
    private const string ReadComments = "Read the comments with 'aspose-cli cells inspect <file> --detail comments'; the workbook keeps them.";

    /// <summary>The warnings of a save of <paramref name="workbook"/> by <paramref name="plan"/>, in the order of <see cref="CellsContentLoss"/>.</summary>
    /// <param name="workbook">The workbook as the save is about to write it.</param>
    /// <param name="plan">How the output is saved: its format, the sheets <c>--sheets</c> selected and the sheet a table of cells writes.</param>
    internal static IEnumerable<Warning> Warnings(Workbook workbook, WorkbookSavePlan plan)
    {
        foreach ((ContentKind kind, List<(string Sheet, string[] Items)> counts, string message, string hint) in Losses(workbook, plan))
        {
            int added = plan.InputContent is { } before ? Added(counts.SelectMany(static sheet => sheet.Items), before) : 0;
            yield return added <= 0 ? ContentLoss.Warning([kind], message, hint, OneSheet(counts))
                : ContentLoss.Warning([kind], $"{message} This edit added or changed {added} of them, and a {plan.FormatId} file cannot hold them.",
                    "To keep what the edit added or changed, write it with --out to a format that holds it, such as xlsx.", OneSheet(counts), affectsCompleteness: true);
        }
    }

    /// <summary>
    /// The content of every sheet of <paramref name="workbook"/> that a save may leave out, each item
    /// as the warnings count it (<see cref="Key"/>), which an edit takes before its batch.
    /// </summary>
    internal static string[] Inventory(Workbook workbook) =>
        [.. workbook.Worksheets.Cast<Worksheet>().Where(static sheet => !CellsEvaluation.IsWarningSheet(sheet))
            .SelectMany(static sheet => Comments(sheet, static _ => true).Concat(Charts(sheet)).Concat(Headers(sheet)))];

    // How many of the items the input did not have: of each item, the copies beyond those it had.
    private static int Added(IEnumerable<string> items, IReadOnlyList<string> before)
    {
        Dictionary<string, int> had = before.CountBy(static item => item).ToDictionary();
        return items.CountBy(static item => item).Sum(item => Math.Max(0, item.Value - had.GetValueOrDefault(item.Key)));
    }

    private static IEnumerable<(ContentKind Kind, List<(string Sheet, string[] Items)> Counts, string Message, string Hint)> Losses(Workbook workbook, WorkbookSavePlan plan)
    {
        string formatId = plan.FormatId;
        IReadOnlyList<int>? selectedSheets = plan.SelectedSheets;
        CellsContentLoss leaves = CellsEngineFormats.Leaves(formatId);
        Worksheet[] written = plan.WritesActiveSheetOnly
            ? [plan.TextSheet(workbook)]
            : [.. workbook.Worksheets.Cast<Worksheet>()
                .Where(sheet => !CellsEvaluation.IsWarningSheet(sheet) && (selectedSheets is null || selectedSheets.Contains(sheet.Index)))];
        if (leaves.HasFlag(CellsContentLoss.UnprintedComments)
            && Counted(written.Where(sheet => selectedSheets is not null || sheet.IsVisible), static sheet => Comments(sheet, comment => !Printed(sheet, comment))) is { Count: > 0 } unprinted)
        {
            yield return (ContentKind.Comments, unprinted,
                $"The {formatId} output leaves out {Total(unprinted)} comment(s), which the page setup of their worksheet does not print: {PerSheet(unprinted)}.",
                "Read the comments with 'aspose-cli cells inspect <file> --detail comments'. To keep them in the output, "
                    + "set printComments to sheet-end with set_page_setup on the worksheet and convert again: they print after its last page."
                    + (CellsEngineFormats.LeavesDetected(workbook.FileFormat).HasFlag(CellsContentLoss.PrintCommentsSetting)
                        ? " This workbook's format does not keep that setting, so save the workbook as xlsx first and set it there." : ""));
        }

        if (leaves.HasFlag(CellsContentLoss.Comments) && Counted(written, static sheet => Comments(sheet, static _ => true)) is { Count: > 0 } comments)
        {
            yield return (ContentKind.Comments, comments, $"The {formatId} output leaves out {Total(comments)} comment(s): {PerSheet(comments)}.", ReadComments);
        }

        if (leaves.HasFlag(CellsContentLoss.FootnoteComments) && Counted(written, static sheet => Comments(sheet, static _ => true)) is { Count: > 0 } footnotes)
        {
            yield return (ContentKind.Comments, footnotes,
                $"The {formatId} output writes {Total(footnotes)} comment(s) only as footnote definitions no cell refers to, which a Markdown reader does not get: {PerSheet(footnotes)}.",
                ReadComments);
        }

        if (leaves.HasFlag(CellsContentLoss.ChartPictures) && Counted(written, Charts) is { Count: > 0 } charts)
        {
            yield return (ContentKind.Charts, charts,
                $"The {formatId} output shows {Total(charts)} chart(s) as pictures, whose titles, labels and data are not text: {PerSheet(charts)}.",
                "Read the chart titles and data ranges with 'aspose-cli cells inspect <file> --detail charts'; the workbook keeps the charts editable.");
        }

        bool printData = leaves.HasFlag(CellsContentLoss.PrintHeadersFooters);
        if ((printData || leaves.HasFlag(CellsContentLoss.HeadersFooters))
            && Counted(written, Headers) is { Count: > 0 } headed)
        {
            string names = $"{(headed.Count == 1 ? "worksheet" : "worksheets")} {string.Join(", ", headed.Select(static item => $"'{item.Sheet}'"))}";
            yield return (ContentKind.HeadersFooters, headed,
                printData
                    ? $"The {formatId} output keeps the headers and footers of {names} only as Excel print data in its styles, which a browser neither shows nor prints."
                    : $"The {formatId} output leaves out the headers and footers of {names}: a table of cells has no pages to print them on.",
                "Read the headers and footers with 'aspose-cli cells inspect <file> --detail layout'; a PDF of the workbook prints them on every page.");
        }
    }

    /// <summary>
    /// Whether the page setup of a sheet prints a comment of it: every one at the end of the sheet,
    /// the ones shown in place, and none otherwise.
    /// </summary>
    private static bool Printed(Worksheet sheet, Comment comment) => sheet.PageSetup.PrintComments switch
    {
        PrintCommentsType.PrintSheetEnd => true,
        PrintCommentsType.PrintInPlace => comment.IsVisible,
        _ => false,
    };

    // A sheet's items of each kind, keyed by Key.
    private static IEnumerable<string> Comments(Worksheet sheet, Func<Comment, bool> which) =>
        sheet.Comments.Cast<Comment>().Where(which).Select(comment => Key("comment", sheet, comment.Note));

    private static IEnumerable<string> Charts(Worksheet sheet) =>
        sheet.Charts.Cast<Aspose.Cells.Charts.Chart>().Select(chart => Key("chart", sheet, chart.Name + '\u001F' + chart.Title?.Text));

    private static IEnumerable<string> Headers(Worksheet sheet) => HasHeaderOrFooter(sheet.PageSetup)
        ? [Key("header", sheet, string.Join('\u001F', HeaderFooterText.InEffect(sheet.PageSetup).Select(static section => section.Text)))] : [];

    /// <summary>
    /// An item of content as its kind, the id of its sheet and its text: renaming the sheet or
    /// moving the item in it, as inserting rows does, keeps it, and changing its text does not.
    /// </summary>
    private static string Key(string kind, Worksheet sheet, string? text) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind}\u001F{sheet.TabId}\u001F{text}");

    /// <summary>
    /// Whether a section of the header or footer, as a read reports them, prints text of its own
    /// (<see cref="HeaderFooterText.PrintsText"/>): more than the page number and page count, which a
    /// page with no pages has nothing to show for, and the formatting codes.
    /// </summary>
    private static bool HasHeaderOrFooter(PageSetup page) =>
        HeaderFooterText.InEffect(page).Any(static section => section.Pages == HeaderFooterPages.Default && HeaderFooterText.PrintsText(section.Text));

    /// <summary>The sheets that have some of a content, with its items, in sheet order.</summary>
    private static List<(string Sheet, string[] Items)> Counted(IEnumerable<Worksheet> sheets, Func<Worksheet, IEnumerable<string>> items) =>
        [.. sheets.Select(sheet => (sheet.Name, items(sheet).ToArray())).Where(static item => item.Item2.Length > 0)];

    private static int Total(List<(string Sheet, string[] Items)> counts) => counts.Sum(static item => item.Items.Length);

    /// <summary>How much each sheet has, such as <c>2 on worksheet 'Sales', 1 on worksheet 'Notes'</c>.</summary>
    private static string PerSheet(List<(string Sheet, string[] Items)> counts) =>
        string.Join(", ", counts.Select(static item => string.Create(CultureInfo.InvariantCulture, $"{item.Items.Length} on worksheet '{item.Sheet}'")));

    /// <summary>The address of the sheet that holds all of the content, or null when several do.</summary>
    private static string? OneSheet(List<(string Sheet, string[] Items)> counts) =>
        counts.Count == 1 ? CellsAddresses.OfSheet("sheet", counts[0].Sheet) : null;
}
