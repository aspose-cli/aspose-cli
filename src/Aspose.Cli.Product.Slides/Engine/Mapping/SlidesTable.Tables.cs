using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Row = Aspose.Cli.Sdk.Tables.Row;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal static partial class SlidesTable
{
    /// <summary>A row of a table, addressed by its position: <c>…/row/0</c>.</summary>
    internal static readonly ObjectKind<IRow> RowKind = new("row", "A row of a table.")
    {
        Row.Number<IRow>("height", "IRow.Height, in points", static r => r.Height),
    };

    /// <summary>A column of a table, addressed by its position: <c>…/column/0</c>.</summary>
    internal static readonly ObjectKind<IColumn> ColumnKind = new("column", "A column of a table.")
    {
        Row.Number<IColumn>("width", "IColumn.Width, in points", static c => c.Width),
    };

    /// <summary>
    /// A cell of a table, addressed by its row and column, the engine's positions from 0:
    /// <c>…/cell/1/2</c>, which is <c>ITable[2, 1]</c>. A merged cell is the one object the engine
    /// gives for each position it covers; it is listed once, at its first row and column.
    /// </summary>
    internal static readonly ObjectKind<ICell> CellKind = new("cell", "A cell of a table; a merged cell once, at its first row and column.")
    {
        Row.Text<ICell>("text", "ICell.TextFrame.Text", static c => c.TextFrame?.Text, static (c, v) => (c.TextFrame ?? throw TableErrors.Absent("ICell.TextFrame")).Text = v),
        Row.Integer<ICell>("rowSpan", "ICell.RowSpan", static c => c.RowSpan),
        Row.Integer<ICell>("colSpan", "ICell.ColSpan", static c => c.ColSpan),
        Fill<ICell>("fill", static c => c.CellFormat.FillFormat),
        LineFill<ICell>("borderTop", static c => c.CellFormat.BorderTop.FillFormat),
        LineFill<ICell>("borderBottom", static c => c.CellFormat.BorderBottom.FillFormat),
        LineFill<ICell>("borderLeft", static c => c.CellFormat.BorderLeft.FillFormat),
        LineFill<ICell>("borderRight", static c => c.CellFormat.BorderRight.FillFormat),
    };

    private static void PlaceTables(DocumentTable<Presentation> table)
    {
        table.Place(ShapeKind, RowKind, new Locator<IShape, IRow>
        {
            Segment = "row",
            Key = [KeyPart.Index],
            Find = static (shape, key) => shape is ITable t && key.Index() < t.Rows.Count ? t.Rows[key.Index()] : null,
            List = static shape => shape is ITable t ? t.Rows : [],
            KeyOf = static (shape, row) => Key.Of(Position(((ITable)shape).Rows, row)),
        });
        table.Place(ShapeKind, ColumnKind, new Locator<IShape, IColumn>
        {
            Segment = "column",
            Key = [KeyPart.Index],
            Find = static (shape, key) => shape is ITable t && key.Index() < t.Columns.Count ? t.Columns[key.Index()] : null,
            List = static shape => shape is ITable t ? t.Columns : [],
            KeyOf = static (shape, column) => Key.Of(Position(((ITable)shape).Columns, column)),
        });
        table.Place(ShapeKind, CellKind, new Locator<IShape, ICell>
        {
            Segment = "cell",
            Key = [KeyPart.Index, KeyPart.Index],
            Find = static (shape, key) => shape is ITable t && key.Index(0) < t.Rows.Count && key.Index(1) < t.Columns.Count ? t[key.Index(1), key.Index(0)] : null,
            List = static shape => shape is ITable t ? Cells(t) : [],
            KeyOf = static (_, cell) => Key.Of(cell.FirstRowIndex, cell.FirstColumnIndex),
        });
        PlaceParagraphs<ICell, ICell>(table, CellKind, static cell => cell.TextFrame, "ICell.TextFrame");
    }

    /// <summary>Every cell of a table in row order, a merged cell once, at its first row and column.</summary>
    private static IEnumerable<ICell> Cells(ITable table)
    {
        for (int row = 0; row < table.Rows.Count; row++)
        {
            for (int column = 0; column < table.Columns.Count; column++)
            {
                ICell cell = table[column, row];
                if (cell.FirstRowIndex == row && cell.FirstColumnIndex == column)
                {
                    yield return cell;
                }
            }
        }
    }
}
