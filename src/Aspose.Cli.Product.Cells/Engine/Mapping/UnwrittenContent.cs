using System.Globalization;
using System.Text.RegularExpressions;
using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Names, before a convert saves, the content of the sheets it writes that the output format
/// does not hold as the workbook does (<see cref="CellsEngineFormat.Leaves"/>): one
/// <c>LOSSY_CONVERSION</c> per kind of content the sheets have, with how much of it each sheet
/// has, naming its kind in <c>details.content</c> (<see cref="ContentLoss"/>). A workbook
/// without such content warns of nothing. The content stays in the workbook and in its other
/// outputs, so no warning affects completeness, as speaker notes an export leaves out do not.
/// The evaluation warning sheets are no content of the workbook and are skipped.
/// </summary>
internal static partial class UnwrittenContent
{
    /// <summary>The warnings of a convert of <paramref name="workbook"/> to <paramref name="formatId"/>, in the order of <see cref="CellsContentLoss"/>.</summary>
    /// <param name="workbook">The workbook as loaded, before the save.</param>
    /// <param name="formatId">The id of the output format.</param>
    /// <param name="selectedSheets">The zero-based sheets <c>--sheets</c> selected, or null for every sheet.</param>
    internal static IEnumerable<Warning> Warnings(Workbook workbook, string formatId, IReadOnlyList<int>? selectedSheets)
    {
        CellsContentLoss leaves = CellsEngineFormats.Leaves(formatId);
        Worksheet[] written = [.. workbook.Worksheets.Cast<Worksheet>()
            .Where(sheet => !CellsEvaluation.IsWarningSheet(sheet) && (selectedSheets is null || selectedSheets.Contains(sheet.Index)))];
        if (leaves.HasFlag(CellsContentLoss.UnprintedComments)
            && Counted(written.Where(sheet => selectedSheets is not null || sheet.IsVisible), UnprintedComments) is { Count: > 0 } unprinted)
        {
            yield return ContentLoss.Warning([ContentKind.Comments],
                $"The {formatId} output leaves out {Total(unprinted)} comment(s), which the page setup of their worksheet does not print: {PerSheet(unprinted)}.",
                "Read the comments with 'aspose-cli cells inspect <file> --detail comments'. A worksheet whose page setup prints comments "
                    + "at the end of the sheet, which Excel can set and no operation does, keeps them in the output.",
                OneSheet(unprinted));
        }

        if (leaves.HasFlag(CellsContentLoss.Comments) && Counted(written, static sheet => sheet.Comments.Count) is { Count: > 0 } comments)
        {
            yield return ContentLoss.Warning([ContentKind.Comments],
                $"The {formatId} output leaves out {Total(comments)} comment(s): {PerSheet(comments)}.",
                "Read the comments with 'aspose-cli cells inspect <file> --detail comments'; the workbook keeps them.",
                OneSheet(comments));
        }

        if (leaves.HasFlag(CellsContentLoss.ChartPictures) && Counted(written, static sheet => sheet.Charts.Count) is { Count: > 0 } charts)
        {
            yield return ContentLoss.Warning([ContentKind.Charts],
                $"The {formatId} output shows {Total(charts)} chart(s) as pictures, whose titles, labels and data are not text: {PerSheet(charts)}.",
                "Read the chart titles and data ranges with 'aspose-cli cells inspect <file> --detail charts'; the workbook keeps the charts editable.",
                OneSheet(charts));
        }

        if (leaves.HasFlag(CellsContentLoss.PrintHeadersFooters)
            && Counted(written, static sheet => HasHeaderOrFooter(sheet.PageSetup) ? 1 : 0) is { Count: > 0 } headed)
        {
            string names = string.Join(", ", headed.Select(static item => $"'{item.Sheet}'"));
            yield return ContentLoss.Warning([ContentKind.HeadersFooters],
                $"The {formatId} output keeps the headers and footers of {(headed.Count == 1 ? "worksheet" : "worksheets")} {names} only as Excel print data in its styles, which a browser neither shows nor prints.",
                "Read the headers and footers with 'aspose-cli cells inspect <file> --detail layout'; a PDF of the workbook prints them on every page.",
                OneSheet(headed));
        }
    }

    /// <summary>
    /// The comments of a sheet its page setup does not print: every one, unless it prints them at
    /// the end of the sheet, or, in place, those not shown.
    /// </summary>
    private static int UnprintedComments(Worksheet sheet) => sheet.PageSetup.PrintComments switch
    {
        PrintCommentsType.PrintSheetEnd => 0,
        PrintCommentsType.PrintInPlace => sheet.Comments.Cast<Comment>().Count(static comment => !comment.IsVisible),
        _ => sheet.Comments.Count,
    };

    /// <summary>Whether a section of the header or footer, as a read reports them, prints text of its own (<see cref="PrintsText"/>).</summary>
    private static bool HasHeaderOrFooter(PageSetup page) =>
        Enumerable.Range(0, 3).Any(section => PrintsText(page.GetHeader(section)) || PrintsText(page.GetFooter(section)));

    /// <summary>
    /// Whether a header or footer section prints more than the page number and page count (the
    /// codes P and N), which a page with no pages has nothing to show for, and the codes that
    /// only format what follows them: the font, the size, the color and the styles such as bold.
    /// A doubled ampersand prints one, so it is text.
    /// </summary>
    private static bool PrintsText(string? section) =>
        !string.IsNullOrWhiteSpace(section)
        && !string.IsNullOrWhiteSpace(HeaderCodes().Replace(section, static code => code.Value == "&&" ? "&" : string.Empty));

    // A doubled ampersand, or a page number, page count, font, size, color or style code.
    [GeneratedRegex(@"&&|&(?:""[^""]*""|\d+|K(?:[0-9A-Fa-f]{6}|\d{2}[+-]\d{3})|[PNBIUESXYOH])", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderCodes();

    /// <summary>The sheets that have some of a content, with how much, in sheet order.</summary>
    private static List<(string Sheet, int Count)> Counted(IEnumerable<Worksheet> sheets, Func<Worksheet, int> count) =>
        [.. sheets.Select(sheet => (sheet.Name, count(sheet))).Where(static item => item.Item2 > 0)];

    private static int Total(List<(string Sheet, int Count)> counts) => counts.Sum(static item => item.Count);

    /// <summary>How much each sheet has, such as <c>2 on worksheet 'Sales', 1 on worksheet 'Notes'</c>.</summary>
    private static string PerSheet(List<(string Sheet, int Count)> counts) =>
        string.Join(", ", counts.Select(static item => string.Create(CultureInfo.InvariantCulture, $"{item.Count} on worksheet '{item.Sheet}'")));

    /// <summary>The address of the sheet that holds all of the content, or null when several do.</summary>
    private static string? OneSheet(List<(string Sheet, int Count)> counts) =>
        counts.Count == 1 ? CellsAddresses.OfSheet("sheet", counts[0].Sheet) : null;
}
