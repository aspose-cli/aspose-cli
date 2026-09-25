using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for workbook-scoped defined names (named ranges).

/// <summary>Creates or replaces a workbook-scoped defined name (a named range).</summary>
[Operation("define_name")]
public sealed record DefineNameOp : Op
{
    /// <summary>The name, such as TaxRate.</summary>
    [Pattern(@"\S")] public required string Name { get; init; }

    /// <summary>The sheet-qualified range the name refers to, such as Config!$B$2.</summary>
    [Pattern(@"\S")] public required string RefersTo { get; init; }
}

/// <summary>Removes a workbook-scoped defined name.</summary>
[Operation("delete_name")]
public sealed record DeleteNameOp : Op
{
    [Pattern(@"\S")] public required string Name { get; init; }
}
