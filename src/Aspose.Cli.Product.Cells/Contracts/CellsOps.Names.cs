using Aspose.Cli.Sdk.Operations;

namespace Aspose.Cli.Product.Cells.Contracts;

// Ops for workbook-scoped defined names (named ranges).

/// <summary>Creates or replaces a workbook-scoped defined name (a named range).</summary>
[Operation("define_name")]
[MistakenFor("add_name", "create_name")]
public sealed record DefineNameOp : CellsOp
{
    /// <summary>The name, such as TaxRate.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsDefinedNameInfo/properties/name")]
    [Pattern(@"\S")] public required string Name { get; init; }

    /// <summary>The sheet-qualified range the name refers to, such as Config!$B$2.</summary>
    [ReadBack("inspect-result.schema.json#/$defs/cellsDefinedNameInfo/properties/refersTo")]
    [Pattern(@"\S")] public required string RefersTo { get; init; }
}

/// <summary>Removes a workbook-scoped defined name.</summary>
[Operation("delete_name")]
public sealed record DeleteNameOp : CellsOp
{
    /// <summary>The name to remove, such as TaxRate.</summary>
    [NotState]
    [Pattern(@"\S")] public required string Name { get; init; }
}
