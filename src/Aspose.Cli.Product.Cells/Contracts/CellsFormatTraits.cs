namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>How the commands treat one declared Cells format.</summary>
/// <param name="Id">The public format id.</param>
internal sealed record CellsFormatTraits(string Id)
{
    /// <summary>Which sheets a convert output in this format holds.</summary>
    public CellsSheetScope SheetScope { get; init; }

    /// <summary>A delimited text format (CSV or TSV), whose output can start with a byte order mark.</summary>
    public bool Delimited { get; init; }
}
