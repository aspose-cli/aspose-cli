using System.CommandLine;
using Aspose.Cli.Sdk.Addressing;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>The input documents and encrypted output the Cells commands declare.</summary>
internal static class CellsInputs
{
    /// <summary>
    /// A <c>--sheets</c> value as a selection of positions, as <see cref="PageRange"/> reads it,
    /// and the value as given when it may name a sheet, which only the open workbook can answer:
    /// a value that is no selection, or one like <c>2024</c> that is also a position past the
    /// last sheet.
    /// </summary>
    /// <exception cref="Aspose.Cli.Sdk.Errors.CliException"><c>OPTION_INVALID</c> for a value of numbers that is not a selection, or for <c>all</c> not in lowercase.</exception>
    public static (PageRange? Selection, string? Text) SheetSelection(PartRangeOption sheets, ParseResult parse)
    {
        if (sheets.Read(parse) is not { } text)
        {
            return (null, null);
        }

        if (PageRange.TryParse(text, out PageRange? range, out _))
        {
            // Only a selection of positions can be a sheet's name given in place of its position.
            return (range, ReferenceEquals(range, PageRange.All) ? null : text);
        }

        // A value of numbers, or the keyword all in another case, is a selection written wrong
        // rather than a sheet's name, so the selection's own error answers it.
        return text.All(static c => char.IsAsciiDigit(c) || c is '-' or ',' || char.IsWhiteSpace(c))
            || string.Equals(text.Trim(), PageRange.AllText, StringComparison.OrdinalIgnoreCase)
            ? (sheets.ReadRange(parse), text)
            : (null, text);
    }

    /// <summary>The password a writing command can put on its workbook.</summary>
    public static readonly EncryptedOutput EncryptedWorkbook = new("the output file");

    /// <summary>The workbook a reading command opens, with the command's own help.</summary>
    public static InputDocument Workbook(string description) => new(description, "the workbook");
}
