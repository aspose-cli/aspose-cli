using Aspose.Cells;
using Aspose.Cells.Drawing;

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
        new("xlsx") { Detected = FileFormatType.Xlsx, Save = SaveFormat.Xlsx },
        new("xltx") { Detected = FileFormatType.Xltx, Save = SaveFormat.Xltx },
        new("xlsm") { Detected = FileFormatType.Xlsm, Save = SaveFormat.Xlsm },
        new("xltm") { Detected = FileFormatType.Xltm, Save = SaveFormat.Xltm },
        new("xlsb") { Detected = FileFormatType.Xlsb, Save = SaveFormat.Xlsb },
        new("xls") { Detected = FileFormatType.Excel97To2003, Save = SaveFormat.Excel97To2003 },
        new("ods") { Detected = FileFormatType.Ods, Save = SaveFormat.Ods },
        new("csv") { Detected = FileFormatType.Csv, Save = SaveFormat.Csv },
        new("tsv") { Detected = FileFormatType.TabDelimited, Save = SaveFormat.TabDelimited },
        new("html") { Detected = FileFormatType.Html, Save = SaveFormat.Html },
        new("mhtml") { Detected = FileFormatType.MHtml, Save = SaveFormat.MHtml },
        new("pdf") { Save = SaveFormat.Pdf },
        new("xps") { Save = SaveFormat.Xps },
        new("json") { Detected = FileFormatType.Json, Save = SaveFormat.Json },
        new("md") { Save = SaveFormat.Markdown },
        new("png") { Image = ImageType.Png, Raster = true },
        new("jpeg") { Image = ImageType.Jpeg, Raster = true },
        new("svg") { Image = ImageType.Svg },
    ];

    /// <summary>The engine save format of a convert format id.</summary>
    public static SaveFormat Save(string formatId) =>
        Find(formatId).Save ?? throw Missing(formatId);

    /// <summary>The engine image type of a render format id.</summary>
    public static ImageType Image(string formatId) =>
        Find(formatId).Image ?? throw Missing(formatId);

    /// <summary><c>true</c> for raster render formats where DPI applies.</summary>
    public static bool IsRaster(string formatId) =>
        All.Any(format => format.Raster && string.Equals(format.Id, formatId, StringComparison.Ordinal));

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

    /// <summary>Whether the image is a bitmap whose resolution the DPI sets.</summary>
    public bool Raster { get; init; }
}
