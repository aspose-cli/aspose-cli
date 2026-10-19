using System.Text;
using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cells.Rendering;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The engine side of each public format id, in one table: the format a workbook is detected
/// as when read, the save format it is converted to, or the image type a sheet renders to. A
/// product test keeps it in step with <c>CellsFormats</c>, so a declared format the engine
/// cannot read or write fails the build, not the user.
/// </summary>
internal static class CellsEngineFormats
{
    /// <summary>Every format id the engine maps, with what it maps to.</summary>
    internal static IReadOnlyList<CellsEngineFormat> All { get; } =
    [
        new("xlsx") { Detected = FileFormatType.Xlsx, Save = SaveFormat.Xlsx, KeepsSheetProtection = true, EvaluationMarks = CellsEvaluationMarkKind.Workbook },
        new("xltx") { Detected = FileFormatType.Xltx, Save = SaveFormat.Xltx, KeepsSheetProtection = true, EvaluationMarks = CellsEvaluationMarkKind.Workbook },
        new("xlsm") { Detected = FileFormatType.Xlsm, Save = SaveFormat.Xlsm, KeepsSheetProtection = true, EvaluationMarks = CellsEvaluationMarkKind.Workbook },
        new("xltm") { Detected = FileFormatType.Xltm, Save = SaveFormat.Xltm, KeepsSheetProtection = true, EvaluationMarks = CellsEvaluationMarkKind.Workbook },
        new("xlsb") { Detected = FileFormatType.Xlsb, Save = SaveFormat.Xlsb, KeepsSheetProtection = true, EvaluationMarks = CellsEvaluationMarkKind.Workbook },
        new("xls") { Detected = FileFormatType.Excel97To2003, Save = SaveFormat.Excel97To2003, KeepsSheetProtection = true, EvaluationMarks = CellsEvaluationMarkKind.Workbook },
        new("ods") { Detected = FileFormatType.Ods, Save = SaveFormat.Ods, KeepsSheetProtection = true, EvaluationMarks = CellsEvaluationMarkKind.Workbook },
        new("csv")
        {
            Detected = FileFormatType.Csv, Save = SaveFormat.Csv, EvaluationMarks = CellsEvaluationMarkKind.DataRow,
            Options = static (byteOrderMark, _) => DelimitedText(SaveFormat.Csv, byteOrderMark),
        },
        new("tsv")
        {
            Detected = FileFormatType.TabDelimited, Save = SaveFormat.TabDelimited, EvaluationMarks = CellsEvaluationMarkKind.DataRow,
            Options = static (byteOrderMark, _) => DelimitedText(SaveFormat.TabDelimited, byteOrderMark),
        },
        new("html")
        {
            Detected = FileFormatType.Html, Save = SaveFormat.Html, EvaluationMarks = CellsEvaluationMarkKind.Pages,
            Options = static (_, _) => new HtmlSaveOptions { SaveAsSingleFile = true, ExportImagesAsBase64 = true },
        },
        new("mhtml") { Detected = FileFormatType.MHtml, Save = SaveFormat.MHtml, EvaluationMarks = CellsEvaluationMarkKind.Pages },
        new("pdf")
        {
            Save = SaveFormat.Pdf, Paginated = true, EvaluationMarks = CellsEvaluationMarkKind.Pages,
            Options = static (_, selectedSheets) => selectedSheets is { } sheets ? new PdfSaveOptions { SheetSet = new SheetSet([.. sheets]) } : null,
        },
        new("xps") { Save = SaveFormat.Xps, Paginated = true, EvaluationMarks = CellsEvaluationMarkKind.Pages },
        new("json") { Detected = FileFormatType.Json, Save = SaveFormat.Json, WritesRecords = true, EvaluationMarks = CellsEvaluationMarkKind.JsonRecord },
        new("md")
        {
            Save = SaveFormat.Markdown, EvaluationMarks = CellsEvaluationMarkKind.MarkdownHeading,
            Options = static (_, _) => new MarkdownSaveOptions { FormatStrategy = CellValueFormatStrategy.None },
        },
        new("png") { Image = ImageType.Png, EvaluationMarks = CellsEvaluationMarkKind.Pages },
        new("jpeg") { Image = ImageType.Jpeg, EvaluationMarks = CellsEvaluationMarkKind.Pages },
        new("svg") { Image = ImageType.Svg, EvaluationMarks = CellsEvaluationMarkKind.Pages },
    ];

    /// <summary>The engine save format of a convert format id.</summary>
    public static SaveFormat Save(string formatId) =>
        Find(formatId).Save ?? throw Missing(formatId);

    /// <summary>The engine image type of a render format id.</summary>
    public static ImageType Image(string formatId) =>
        Find(formatId).Image ?? throw Missing(formatId);

    /// <summary><c>true</c> for convert formats that lay each sheet out on its printed pages.</summary>
    public static bool IsPaginated(string formatId) => Find(formatId).Paginated;

    /// <summary>Whether an output in <paramref name="formatId"/> keeps the protection of its sheets.</summary>
    public static bool KeepsSheetProtection(string formatId) => Find(formatId).KeepsSheetProtection;

    /// <summary><c>true</c> for a convert format that writes each sheet's rows as records (json).</summary>
    public static bool WritesRecords(string formatId) => Find(formatId).WritesRecords;

    /// <summary>
    /// The evaluation marks an output in <paramref name="formatId"/> carries; null for an id the
    /// table does not map.
    /// </summary>
    public static CellsEvaluationMarkKind? EvaluationMarksOf(string formatId) =>
        All.FirstOrDefault(format => string.Equals(format.Id, formatId, StringComparison.Ordinal))?.EvaluationMarks;

    /// <summary>The engine save options of a convert format id, or null when its save format alone is enough.</summary>
    public static SaveOptions? Options(string formatId, bool byteOrderMark, IReadOnlyList<int>? selectedSheets) =>
        Find(formatId).Options?.Invoke(byteOrderMark, selectedSheets);

    /// <summary>The format id of a format the engine detected.</summary>
    /// <remarks>
    /// The engine distinguishes many more input formats than the public vocabulary does; those
    /// fall back to the lower-cased engine name, so the information is preserved without
    /// inventing contract ids.
    /// </remarks>
    public static string IdOf(FileFormatType type) =>
        All.FirstOrDefault(format => format.Detected == type)?.Id ?? type.ToString().ToLowerInvariant();

    private static CellsEngineFormat Find(string formatId) =>
        All.FirstOrDefault(format => string.Equals(format.Id, formatId, StringComparison.Ordinal))
            ?? throw Missing(formatId);

    // The values as they are: no number formats applied, leading blank rows and columns kept,
    // in UTF-8 with a byte order mark only on request.
    private static TxtSaveOptions DelimitedText(SaveFormat format, bool byteOrderMark) => new(format)
    {
        FormatStrategy = CellValueFormatStrategy.None, TrimLeadingBlankRowAndColumn = false,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: byteOrderMark),
    };

    private static ArgumentOutOfRangeException Missing(string formatId) =>
        new(nameof(formatId), formatId, "Format id is missing from the engine format table.");
}

/// <summary>What one public format id is to the engine.</summary>
/// <param name="Id">The public format id.</param>
internal sealed record CellsEngineFormat(string Id)
{
    /// <summary>The detected format a workbook of this id reads as; null when it is never read.</summary>
    public FileFormatType? Detected { get; init; }

    /// <summary>The format a workbook is saved in; null when it is not a convert format.</summary>
    public SaveFormat? Save { get; init; }

    /// <summary>The image type a sheet renders to; null when it is not a render format.</summary>
    public ImageType? Image { get; init; }

    /// <summary>
    /// Whether the output lays each sheet out on its printed pages, as printing does, so a chart
    /// that reaches two of them is split.
    /// </summary>
    public bool Paginated { get; init; }

    /// <summary>
    /// Whether a protected sheet saved in this format reads back protected; a product test checks
    /// it by a round trip through the engine.
    /// </summary>
    public bool KeepsSheetProtection { get; init; }

    /// <summary>Whether the output writes each sheet's rows as records (json), so it chooses the sheets it holds.</summary>
    public bool WritesRecords { get; init; }

    /// <summary>The evaluation marks an output in this format carries; null when the format is never written.</summary>
    public CellsEvaluationMarkKind? EvaluationMarks { get; init; }

    /// <summary>The save options of this format, or null when its save format alone is enough.</summary>
    public CellsSaveOptionsFactory? Options { get; init; }
}

/// <summary>Creates the save options of one convert format.</summary>
/// <param name="byteOrderMark">Whether a text output starts with a UTF-8 byte order mark (<c>--bom</c>).</param>
/// <param name="selectedSheets">The zero-based sheets <c>--sheets</c> selected, or null for every sheet.</param>
internal delegate SaveOptions? CellsSaveOptionsFactory(bool byteOrderMark, IReadOnlyList<int>? selectedSheets);

/// <summary>What evaluation mode leaves in an output, as <see cref="CellsEvaluationProfile"/> names it.</summary>
internal enum CellsEvaluationMarkKind
{
    /// <summary>Every evaluation warning sheet, as a workbook keeps them.</summary>
    Workbook,

    /// <summary>The pages of the visible warning sheets, as a page format or an image prints them.</summary>
    Pages,

    /// <summary>The notice as the last data row of a delimited text output.</summary>
    DataRow,

    /// <summary>The notice as a heading after the table of a Markdown output.</summary>
    MarkdownHeading,

    /// <summary>The notice as a record after the records of each sheet of a JSON output.</summary>
    JsonRecord,
}
