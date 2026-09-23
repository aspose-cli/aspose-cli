namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for workbook-scoped defined names (named ranges).

/// <summary>Creates or replaces a workbook-scoped defined name (a named range).</summary>
public sealed record DefineNameOp() : Op
{
    /// <summary>The name, e.g. <c>TaxRate</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The range the name refers to, sheet-qualified, e.g. <c>Config!$B$2</c>.</summary>
    public required string RefersTo { get; init; }
}

/// <summary>Removes a workbook-scoped defined name.</summary>
public sealed record DeleteNameOp() : Op
{
    /// <summary>The name to remove.</summary>
    public required string Name { get; init; }
}
