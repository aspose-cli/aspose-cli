using System.Globalization;
using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>
/// Writes normalized CLR values into cells. The ops pipeline guarantees the
/// value is a string, a double, a decimal (a <c>--set</c> number, which keeps the
/// digits it was written with), a boolean or null (null clears the cell). A string
/// is text whatever it looks like: the engine types the text of an error value,
/// such as #N/A, as that error (known issue CELLS-CSV-ERROR-TEXT, KNOWN-ISSUES.md),
/// and takes a leading apostrophe for a quote prefix, so such text is written with a
/// quote prefix of its own, as typing '#N/A or ''abc in Excel holds it. A
/// number or a Boolean drops the quote prefix an earlier text left, as typing one
/// into such a cell in Excel does. A <c>--set</c> number into a delimited text input
/// read as written is written as a load reads its text.
/// </summary>
internal static class ValueWriter
{
    public static void Write(Cell cell, object? value, bool readAsWritten)
    {
        if (value is double or decimal or bool && cell.GetStyle() is { QuotePrefix: true } quoted)
        {
            quoted.QuotePrefix = false;
            cell.SetStyle(quoted);
        }

        switch (value)
        {
            case null:
                cell.PutValue((string?)null);
                break;
            case string text:
                cell.PutValue(text.StartsWith('\'') || text.StartsWith('#') && CellsTextImport.ReadType(text) == CellValueType.IsError ? "'" + text : text);
                break;
            case decimal written when readAsWritten:
                CellsTextImport.PutAsWritten(cell, written.ToString(CultureInfo.InvariantCulture));
                break;
            case decimal written:
                cell.PutValue((double)written);
                break;
            case double number:
                cell.PutValue(number);
                break;
            case bool flag:
                cell.PutValue(flag);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unnormalized value of type {value.GetType().Name} reached the engine.");
        }
    }
}
