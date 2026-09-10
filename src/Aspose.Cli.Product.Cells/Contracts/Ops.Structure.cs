namespace Aspose.Cli.Product.Cells.Contracts;

// Ops that change the row and column structure of a sheet.

/// <summary>Inserts rows before a 1-based row number.</summary>
public sealed record InsertRowsOp() : Op(OpNames.InsertRows)
{
    /// <summary>1-based row number to insert before.</summary>
    public required int At { get; init; }

    /// <summary>Number of rows; 1 when omitted.</summary>
    public int? Count { get; init; }
}

/// <summary>Deletes rows starting at a 1-based row number.</summary>
public sealed record DeleteRowsOp() : Op(OpNames.DeleteRows)
{
    /// <summary>1-based first row to delete.</summary>
    public required int At { get; init; }

    /// <summary>Number of rows; 1 when omitted.</summary>
    public int? Count { get; init; }
}

/// <summary>Inserts columns before a column letter.</summary>
public sealed record InsertColumnsOp() : Op(OpNames.InsertColumns)
{
    /// <summary>Column letter to insert before, e.g. <c>C</c>.</summary>
    public required string At { get; init; }

    /// <summary>Number of columns; 1 when omitted.</summary>
    public int? Count { get; init; }
}

/// <summary>Deletes columns starting at a column letter.</summary>
public sealed record DeleteColumnsOp() : Op(OpNames.DeleteColumns)
{
    /// <summary>First column letter to delete, e.g. <c>C</c>.</summary>
    public required string At { get; init; }

    /// <summary>Number of columns; 1 when omitted.</summary>
    public int? Count { get; init; }
}

/// <summary>Sets row heights, or auto-fits when <see cref="Height"/> is omitted.</summary>
public sealed record ResizeRowsOp() : Op(OpNames.ResizeRows)
{
    /// <summary>1-based first row.</summary>
    public required int From { get; init; }

    /// <summary>1-based last row; defaults to <see cref="From"/>.</summary>
    public int? To { get; init; }

    /// <summary>Height in points; omit to auto-fit.</summary>
    public double? Height { get; init; }
}

/// <summary>Sets column widths, or auto-fits when <see cref="Width"/> is omitted.</summary>
public sealed record ResizeColumnsOp() : Op(OpNames.ResizeColumns)
{
    /// <summary>First column letter.</summary>
    public required string From { get; init; }

    /// <summary>Last column letter; defaults to <see cref="From"/>.</summary>
    public string? To { get; init; }

    /// <summary>Width in characters; omit to auto-fit.</summary>
    public double? Width { get; init; }
}
