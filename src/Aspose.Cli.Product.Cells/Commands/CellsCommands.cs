using System.CommandLine;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>Product-owned <c>cells</c> command tree.</summary>
internal static class CellsCommands
{
    /// <summary>The password a writing command can put on its workbook.</summary>
    public static readonly EncryptedOutput EncryptedWorkbook = new("the output file");

    /// <summary>The workbook a reading command opens, with the command's own help.</summary>
    public static InputDocument Workbook(string description) => new(description, "the workbook");

    public static Command Create(IProductCommandHost<ICellsEngine> host)
    {
        var cells = new Command(
            "cells",
            "Spreadsheet operations (Excel and friends) with engine-grade fidelity.");
        cells.Subcommands.Add(InfoCommand.Create(host));
        cells.Subcommands.Add(QueryCommand.Create(host));
        cells.Subcommands.Add(NewCommand.Create(host));
        cells.Subcommands.Add(EditCommand.Create(host));
        cells.Subcommands.Add(DiffCommand.Create(host));
        cells.Subcommands.Add(ConvertCommand.Create(host));
        cells.Subcommands.Add(RenderCommand.Create(host));
        return cells.WithExamples(
            ["cells inspect book.xlsx --output json"],
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "editing", "the edit-operation vocabulary and recipes"),
                CommandHelpLink.Docs(CellsModule.Manifest, "workbook-standards", "professional workbook construction guidance"),
                CommandHelpLink.Docs(CellsModule.Manifest, "verification", "the spreadsheet delivery verification protocol"),
                CommandHelpLink.Schema(CellsModule.Manifest, "the operations JSON Schema"),
            ]);
    }
}
