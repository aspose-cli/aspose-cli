using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>Product-owned <c>cells</c> command tree.</summary>
internal static class CellsCommands
{
    /// <summary>The password a writing command can put on its workbook.</summary>
    public static readonly EncryptedOutput EncryptedWorkbook = new("the output file", CellsFormats.EncryptableIds);

    /// <summary>The workbook a reading command opens, with the command's own help.</summary>
    public static InputDocument Workbook(string description) => new(description, "the workbook");

    /// <summary>Links the documentation topic <paramref name="topic"/> of this product.</summary>
    public static CommandHelpLink Docs(string topic, string description) =>
        CommandHelpLink.Docs($"{CellsModule.Manifest.Id}/{topic}", description);

    /// <summary>Links the operation JSON schema.</summary>
    public static CommandHelpLink Schema(string description) =>
        CommandHelpLink.Schema(CellsModule.Manifest.Operations.Single(), description);

    public static Command Create(IProductCommandHost<IWorkbookEngine> host)
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
                Docs("editing", "the edit-operation vocabulary and recipes"),
                Docs("workbook-standards", "professional workbook construction guidance"),
                Docs("verification", "the spreadsheet delivery verification protocol"),
                Schema("the operations JSON Schema"),
            ]);
    }
}
