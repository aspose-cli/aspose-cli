namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>
/// Where one applied operation landed: a sheet, and a range within it when
/// the operation has one. A null sheet means the active sheet; a null range
/// means the sheet as a whole.
/// </summary>
public sealed record CellsEditTarget(string? Sheet, string? Range);
