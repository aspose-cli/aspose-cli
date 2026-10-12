using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells convert</c>.</summary>
public sealed record CellsConvertRequest
{
    /// <summary>The workbook to convert.</summary>
    public required string Input { get; init; }

    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>Restrict the conversion to one sheet, where the format supports it.</summary>
    public string? SheetName { get; init; }

    /// <summary>Password for encrypted files.</summary>
    public Secret? Password { get; init; }

    /// <summary>New output password; null preserves source encryption where the target format supports it.</summary>
    public Secret? EncryptPassword { get; init; }

    /// <summary>How a delimited text input is decoded and its numbers and dates parsed.</summary>
    public CellsTextImportOptions? TextImport { get; init; }

    /// <summary>Whether a CSV or TSV output starts with a UTF-8 byte order mark.</summary>
    public bool ByteOrderMark { get; init; }
}

/// <summary>
/// How <c>cells convert</c> reads a delimited text input. Other commands read text inputs as
/// UTF-8 with invariant number and date formats and refuse inputs that need these choices.
/// </summary>
public sealed record CellsTextImportOptions
{
    /// <summary>Text encoding name, such as <c>gb18030</c>; null reads a byte order mark or UTF-8.</summary>
    public string? Encoding { get; init; }

    /// <summary>Culture name for numbers and dates, such as <c>de-DE</c>; null uses invariant formats.</summary>
    public string? Culture { get; init; }
}
