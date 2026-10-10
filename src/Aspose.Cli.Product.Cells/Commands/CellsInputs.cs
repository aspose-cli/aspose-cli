namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>The input documents and encrypted output the Cells commands declare.</summary>
internal static class CellsInputs
{
    /// <summary>The password a writing command can put on its workbook.</summary>
    public static readonly EncryptedOutput EncryptedWorkbook = new("the output file");

    /// <summary>The workbook a reading command opens, with the command's own help.</summary>
    public static InputDocument Workbook(string description) => new(description, "the workbook");
}
