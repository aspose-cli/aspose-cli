using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The engine side of each format id, in one table: the format a workbook is detected as when
/// read, the save format it is written in, or the image type a sheet renders to. Each save takes
/// the engine's own options for its format, with nothing set but the warning callback, except an
/// HTML or Markdown output, which holds its images itself so that the output is one file.
/// </summary>
internal static class CellsEngineFormats
{
    /// <summary>The workbook formats the engine opens: each detected type and the format id it is read as.</summary>
    private static readonly Dictionary<FileFormatType, string> Read = new()
    {
        [FileFormatType.Xlsx] = "xlsx",
        [FileFormatType.Xltx] = "xltx",
        [FileFormatType.Xlsm] = "xlsm",
        [FileFormatType.Xltm] = "xltm",
        [FileFormatType.Xlam] = "xlam",
        [FileFormatType.Xlsb] = "xlsb",
        [FileFormatType.Excel97To2003] = "xls",
        [FileFormatType.Xlt] = "xlt",
        [FileFormatType.Excel95] = "excel95",
        [FileFormatType.Excel4] = "excel4",
        [FileFormatType.Excel3] = "excel3",
        [FileFormatType.Excel2] = "excel2",
    };

    /// <summary>The save format of each format id a workbook is written in.</summary>
    private static readonly Dictionary<string, SaveFormat> Saved = new(StringComparer.Ordinal)
    {
        ["xlsx"] = SaveFormat.Xlsx,
        ["xltx"] = SaveFormat.Xltx,
        ["xlsm"] = SaveFormat.Xlsm,
        ["xltm"] = SaveFormat.Xltm,
        ["xlam"] = SaveFormat.Xlam,
        ["xlsb"] = SaveFormat.Xlsb,
        ["xls"] = SaveFormat.Excel97To2003,
        ["xlt"] = SaveFormat.Xlt,
        ["ods"] = SaveFormat.Ods,
        ["csv"] = SaveFormat.Csv,
        ["tsv"] = SaveFormat.TabDelimited,
        ["html"] = SaveFormat.Html,
        ["mhtml"] = SaveFormat.MHtml,
        ["pdf"] = SaveFormat.Pdf,
        ["xps"] = SaveFormat.Xps,
        ["json"] = SaveFormat.Json,
        ["md"] = SaveFormat.Markdown,
    };

    /// <summary>The load format of each text format id convert reads.</summary>
    private static readonly Dictionary<string, LoadFormat> TextLoads = new(StringComparer.Ordinal)
    {
        ["csv"] = LoadFormat.Csv,
        ["tsv"] = LoadFormat.Tsv,
    };

    /// <summary>The image type of each render format id.</summary>
    private static readonly Dictionary<string, ImageType> Images = new(StringComparer.Ordinal)
    {
        ["png"] = ImageType.Png,
        ["jpeg"] = ImageType.Jpeg,
        ["svg"] = ImageType.Svg,
    };

    /// <summary>The format id a workbook of a detected type is read as; null for a type no command opens as a workbook.</summary>
    internal static string? ReadAs(FileFormatType type) => Read.GetValueOrDefault(type);

    /// <summary>The engine's name of a detected type, as an error states it.</summary>
    internal static string Name(FileFormatType type) => Read.GetValueOrDefault(type) ?? type.ToString().ToLowerInvariant();

    /// <summary>The engine's load format of a text format id.</summary>
    internal static LoadFormat TextLoad(string formatId) => TextLoads[formatId];

    /// <summary>The member of the engine's CellValueFormatStrategy that <paramref name="name"/> names, in its case.</summary>
    /// <exception cref="CliException"><c>OPTION_INVALID</c> for a name no member has.</exception>
    internal static CellValueFormatStrategy FormatStrategy(string name) =>
        name is { Length: > 0 } && !char.IsDigit(name[0]) && Enum.TryParse(name, ignoreCase: false, out CellValueFormatStrategy strategy) && Enum.IsDefined(strategy)
            ? strategy
            : throw CliErrors.OptionInvalid(CellsOptionNames.FormatStrategy,
                $"'{name}' is no member of CellValueFormatStrategy; it is one of: {string.Join(", ", Enum.GetNames<CellValueFormatStrategy>())}");

    /// <summary>The engine image type of a render format id.</summary>
    internal static ImageType Image(string formatId) => Images[formatId];

    /// <summary>The engine's save options of <paramref name="formatId"/>, with the warning callback set.</summary>
    internal static SaveOptions Options(string formatId, EngineWarningLog warnings)
    {
        SaveFormat format = Saved[formatId];
        SaveOptions options = format switch
        {
            SaveFormat.Xlsx or SaveFormat.Xltx or SaveFormat.Xlsm or SaveFormat.Xltm or SaveFormat.Xlam => new OoxmlSaveOptions(format),
            SaveFormat.Xlsb => new XlsbSaveOptions(),
            SaveFormat.Excel97To2003 or SaveFormat.Xlt => new XlsSaveOptions(format),
            SaveFormat.Ods => new OdsSaveOptions(),
            SaveFormat.Csv or SaveFormat.TabDelimited => new TxtSaveOptions(format),
            // One file: the page holds its images instead of a folder beside it.
            SaveFormat.Html or SaveFormat.MHtml => new HtmlSaveOptions(format) { SaveAsSingleFile = true, ExportImagesAsBase64 = true },
            SaveFormat.Pdf => new PdfSaveOptions(),
            SaveFormat.Xps => new XpsSaveOptions(),
            SaveFormat.Json => new JsonSaveOptions(),
            // One file: the text holds its images instead of a folder beside it.
            SaveFormat.Markdown => new MarkdownSaveOptions { ExportImagesAsBase64 = true },
            _ => throw new ArgumentOutOfRangeException(nameof(formatId), formatId, "No save options for this format."),
        };
        options.WarningCallback = new CellsWarnings(warnings);
        return options;
    }
}

/// <summary>Passes each warning Aspose.Cells reports to the log, as it reported it, and lets the engine go on.</summary>
internal sealed class CellsWarnings(EngineWarningLog log) : IWarningCallback
{
    public void Warning(WarningInfo warningInfo) =>
        log.Add(Enum.GetName(warningInfo.Type) ?? warningInfo.Type.ToString(), warningInfo.Description);
}
