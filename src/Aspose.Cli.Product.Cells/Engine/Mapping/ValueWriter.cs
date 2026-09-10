using Aspose.Cells;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Writes normalized CLR values into cells. The ops pipeline guarantees the
/// value is a string, double, boolean or null (null clears the cell).
/// </summary>
internal static class ValueWriter
{
    public static void Write(Cell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.PutValue((string?)null);
                break;
            case string text:
                cell.PutValue(text);
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
