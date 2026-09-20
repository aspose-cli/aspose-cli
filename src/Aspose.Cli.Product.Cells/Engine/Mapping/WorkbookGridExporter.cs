using System.Text;
using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cells.Rendering;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Renders the workbook view: one self-contained HTML document holding every
/// sheet's grid, with each data cell stamped with its A1 address so the
/// presenter can patch and highlight cells in place. The HTML option
/// combination below was probed against Aspose.Cells 26.9.0:
/// <list type="bullet">
/// <item><description>
/// Default <see cref="HtmlSaveOptions"/> emit a frameset entry plus an
/// <c>index_files/</c> satellite directory (per-sheet <c>.htm</c>, stylesheet,
/// tabstrip) — workable for directory serving but needlessly chatty.
/// </description></item>
/// <item><description>
/// <see cref="HtmlSaveOptions.SaveAsSingleFile"/> collapses everything into
/// one HTML file: every sheet is inlined as a
/// <c>&lt;div id='table_N' sheetName='…'&gt;</c> block, the tab-switching
/// script is embedded, the charset meta is UTF-8, and chart/shape images are
/// already inlined as <c>data:image</c> URIs even without the base64 flag.
/// </description></item>
/// <item><description>
/// <see cref="HtmlSaveOptions.ExportImagesAsBase64"/> made no observable
/// difference on 26.9.0 (single-file mode inlines images regardless); it is
/// still set explicitly so an engine upgrade that decouples the two keeps
/// picture-bearing workbooks single-file rather than silently spilling an
/// image directory next to the entry.
/// </description></item>
/// <item><description>
/// <see cref="HtmlSaveOptions.ExportActiveWorksheetOnly"/> defaults to
/// <c>false</c>; it is pinned explicitly because the view must always show
/// every sheet, whatever future defaults do.
/// </description></item>
/// <item><description>
/// <see cref="HtmlSaveOptions.CellNameAttribute"/> stamps every data cell
/// with its sheet-relative A1 address, single-quoted and uppercase:
/// <c>&lt;td data-cell='B2'&gt;</c> — including cells that are empty but
/// inside the used range (given the gridlines flag below). A merged range
/// emits only its anchor (<c>data-cell</c> of the anchor plus
/// <c>colspan</c>/<c>rowspan</c>); the covered cells have no element at all.
/// Heading cells and the hidden column-fixup row never carry the attribute,
/// so "has <c>data-cell</c>" is exactly "is an addressable data cell".
/// </description></item>
/// <item><description>
/// <see cref="HtmlSaveOptions.ExportGridLines"/> is structural, not visual,
/// in the multi-sheet wrapper path: without it a run of empty cells collapses
/// into a single <c>colspan='N'</c> td (<c>mso-ignore:colspan</c>) and every
/// cell but the first loses its address; with it each used-range cell gets
/// its own td. The gridline border rule (<c>border:solid #b6b6b6 1px</c> on
/// the base <c>td</c>) appears only in the bare single-sheet path. Caveat
/// either way: a text cell overflowing empty right neighbours still absorbs
/// them via <c>colspan</c> + <c>mso-ignore:colspan</c> (observed on the
/// evaluation-warning sheet), so a missing td does not imply "outside the
/// used range".
/// </description></item>
/// <item><description>
/// <see cref="HtmlSaveOptions.ExportRowColumnHeadings"/> renders the column
/// letters as the first <c>&lt;tr&gt;</c> of each sheet table and the row
/// number as an extra first td of every data row (plus a 30px lead
/// <c>&lt;col&gt;</c>) — plain tds carrying the fixed inline style
/// <c>text-align:center;vertical-align:middle;background:#e6e6e6;border:1px
/// solid black;</c> and no <c>data-cell</c>. The wrapper structure is
/// unchanged: still exactly one <c>&lt;table&gt;</c> per
/// <c>&lt;div id='table_N' sheetName='…'&gt;</c> container, and a
/// single-sheet workbook (licensed mode — evaluation appends its warning
/// sheet) still exports as a bare body without wrapper divs or the
/// tab-switching script.
/// </description></item>
/// </list>
/// Every generated artifact is published through the Host-owned bounded sink.
/// </summary>
internal static class WorkbookGridExporter
{
    private const int MaxHtmlBytes = 64 * 1024 * 1024;
    /// <summary>Default file name of the grid part.</summary>
    private const string EntryFileName = "index.html";

    /// <summary>
    /// Name of the attribute that stamps each exported data cell with its A1
    /// address; the grid presenter patches and highlights cells through it.
    /// </summary>
    private const string CellAddressAttribute = "data-cell";

    /// <summary>
    /// Product-owned metadata read by the browser shell to restore the
    /// workbook's saved active sheet on the first page load.
    /// </summary>
    internal const string ActiveSheetMetaName = "aspose-active-sheet";

    /// <summary>
    /// Exports <paramref name="workbook"/> as one self-contained HTML grid
    /// through <paramref name="artifacts"/> and returns the part file name.
    /// </summary>
    public static string Export(
        Workbook workbook,
        Aspose.Cli.Sdk.Views.IViewArtifactSink artifacts,
        string entryFileName = EntryFileName)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(artifacts);

        var options = new HtmlSaveOptions
        {
            SaveAsSingleFile = true,
            ExportImagesAsBase64 = true,
            ExportActiveWorksheetOnly = false,
            ExportGridLines = true,
            ExportRowColumnHeadings = true,
            CellNameAttribute = CellAddressAttribute,
        };

        using var stream = new LimitedMemoryStream(MaxHtmlBytes);
        workbook.Save(stream, options);
        string html = Encoding.UTF8.GetString(
            stream.GetBuffer(),
            0,
            checked((int)stream.Length));
        artifacts.WriteText(
            entryFileName,
            StampActiveSheet(
                html,
                workbook.Worksheets[workbook.Worksheets.ActiveSheetIndex].Name));
        return entryFileName;
    }

    private static string StampActiveSheet(string html, string sheetName)
    {
        int headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headEnd < 0)
        {
            throw new InvalidOperationException(
                "Aspose.Cells HTML export did not contain a closing head element.");
        }

        string marker = $"<meta name=\"{ActiveSheetMetaName}\" content=\"{System.Net.WebUtility.HtmlEncode(sheetName)}\">\n";
        return html.Insert(headEnd, marker);
    }

    internal sealed class LimitedMemoryStream(long maximumBytes) : MemoryStream
    {
        public override void SetLength(long value)
        {
            EnsureLength(value);
            base.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureLength(checked(Position + count));
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureLength(checked(Position + buffer.Length));
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureLength(checked(Position + 1));
            base.WriteByte(value);
        }

        private void EnsureLength(long length)
        {
            if (length > maximumBytes)
            {
                throw CliErrors.PreviewBudgetExceeded(
                    "workbook HTML bytes",
                    length,
                    maximumBytes);
            }
        }
    }
}
