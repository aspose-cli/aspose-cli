using System.Globalization;
using System.Text;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells convert</c>.</summary>
public sealed record CellsConvertRequest
{
    /// <summary>The workbook, or CSV or TSV text, to convert.</summary>
    public required string Input { get; init; }

    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The sheets written, by name (<c>--sheet</c>): one for a CSV, TSV or Markdown output, any for PDF; null writes what the engine writes for the format.</summary>
    public IReadOnlyList<string>? Sheets { get; init; }

    /// <summary>Password for an encrypted input.</summary>
    public Secret? Password { get; init; }

    /// <summary>The password the output opens with, or null.</summary>
    public Secret? EncryptPassword { get; init; }

    /// <summary>How a CSV or TSV input is read; null for a workbook input.</summary>
    public CellsTextInput? TextInput { get; init; }

    /// <summary>
    /// How a CSV, TSV or Markdown output writes its values, the engine's <c>CellValueFormatStrategy</c>
    /// member name (<c>--format-strategy</c>); null for any other output.
    /// </summary>
    public string? FormatStrategy { get; init; }

    /// <summary>Whether a CSV or TSV output starts with a UTF-8 byte order mark (<c>--bom</c>): <c>TxtSaveOptions.Encoding</c> is UTF-8 with its mark.</summary>
    public bool ByteOrderMark { get; init; }
}

/// <summary>How <c>cells convert</c> reads a CSV or TSV input: every engine option the caller states.</summary>
/// <param name="Format">The input's format by its name: <c>tsv</c> for a .tsv file, <c>csv</c> otherwise.</param>
/// <param name="Separator">The separator (<c>TxtLoadOptions.Separator</c>): the one stated, or the format's own, the tab of TSV.</param>
/// <param name="Encoding">The text encoding (<c>TxtLoadOptions.Encoding</c>).</param>
/// <param name="ConvertNumbers">Whether numbers become numbers (<c>TxtLoadOptions.ConvertNumericData</c>).</param>
/// <param name="ConvertDates">Whether dates become dates (<c>TxtLoadOptions.ConvertDateTimeData</c>).</param>
/// <param name="Culture">The culture numbers and dates are read in (<c>LoadOptions.CultureInfo</c>); given when either is converted.</param>
public sealed record CellsTextInput(string Format, char Separator, Encoding Encoding, bool ConvertNumbers, bool ConvertDates, CultureInfo? Culture);
