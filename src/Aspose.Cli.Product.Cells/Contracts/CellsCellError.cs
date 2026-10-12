namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>A cell whose formula evaluates to an error — a delivery-time QA signal.</summary>
public sealed record CellsCellError
{
    /// <summary>Sheet the cell is on.</summary>
    public required string Sheet { get; init; }

    /// <summary>Cell address, e.g. <c>C7</c>.</summary>
    public required string Cell { get; init; }

    /// <summary>The error value, e.g. <c>#REF!</c>, <c>#DIV/0!</c>, <c>#VALUE!</c>.</summary>
    public required string Error { get; init; }

    /// <summary>
    /// <c>true</c> in edit verification when the input cell had the same formula and the same
    /// error value; omitted otherwise.
    /// </summary>
    [AllowedValues(true)]
    public bool? Preexisting { get; init; }
}
