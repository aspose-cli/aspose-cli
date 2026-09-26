namespace Aspose.Cli.Product.Cells.Ports;

/// <summary>Product-internal layout facts used only by the Cells review adapter.</summary>
internal interface ICellsReviewLayoutPort
{
    CellsReviewLayout Inspect(string filePath, string? password);
}
