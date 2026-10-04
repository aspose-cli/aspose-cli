using Aspose.Cells;
using Aspose.Cells.Drawing;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Maps the public format vocabulary to engine enums and back. A unit test
/// keeps this mapper and <c>CellsFormats</c> in lockstep, so adding a format
/// id without teaching the engine about it fails the build, not the user.
/// </summary>
internal static class FormatMapper
{
    /// <summary>Maps a canonical convert format id to the engine save format.</summary>
    public static SaveFormat ToSaveFormat(string formatId) => formatId switch
    {
        "xlsx" => SaveFormat.Xlsx,
        "xltx" => SaveFormat.Xltx,
        "xlsm" => SaveFormat.Xlsm,
        "xltm" => SaveFormat.Xltm,
        "xlsb" => SaveFormat.Xlsb,
        "xls" => SaveFormat.Excel97To2003,
        "ods" => SaveFormat.Ods,
        "csv" => SaveFormat.Csv,
        "tsv" => SaveFormat.TabDelimited,
        "html" => SaveFormat.Html,
        "mhtml" => SaveFormat.MHtml,
        "pdf" => SaveFormat.Pdf,
        "xps" => SaveFormat.Xps,
        "json" => SaveFormat.Json,
        "md" => SaveFormat.Markdown,
        _ => throw new ArgumentOutOfRangeException(
            nameof(formatId), formatId, "Format id is missing from the engine mapper."),
    };

    /// <summary>Maps a canonical render format id to the engine image type.</summary>
    public static ImageType ToImageType(string formatId) => formatId switch
    {
        "png" => ImageType.Png,
        "jpeg" => ImageType.Jpeg,
        "svg" => ImageType.Svg,
        _ => throw new ArgumentOutOfRangeException(
            nameof(formatId), formatId, "Format id is missing from the engine mapper."),
    };

    /// <summary><c>true</c> for raster render formats where DPI applies.</summary>
    public static bool IsRaster(string formatId) => formatId is "png" or "jpeg";

    /// <summary>Maps the engine's detected file format to a canonical format id.</summary>
    public static string ToFormatId(FileFormatType type) => type switch
    {
        FileFormatType.Xlsx => "xlsx",
        FileFormatType.Xltx => "xltx",
        FileFormatType.Xlsm => "xlsm",
        FileFormatType.Xltm => "xltm",
        FileFormatType.Xlsb => "xlsb",
        FileFormatType.Excel97To2003 => "xls",
        FileFormatType.Ods => "ods",
        FileFormatType.Csv => "csv",
        FileFormatType.TabDelimited => "tsv",
        FileFormatType.Html => "html",
        FileFormatType.Json => "json",
        // The engine distinguishes many more input formats than the public
        // vocabulary does; fall back to the lower-cased engine name so the
        // information is preserved without inventing contract ids.
        _ => type.ToString().ToLowerInvariant(),
    };
}
