namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>The documents the Cells commands read and write.</summary>
internal static class CellsInputs
{
    /// <summary>The workbook every reading command opens.</summary>
    public static readonly InputDocument Workbook = new("Workbook file path.", "the workbook");

    /// <summary>The password a writing command can put on its workbook.</summary>
    public static readonly EncryptedOutput EncryptedWorkbook = new("the output workbook");
}
