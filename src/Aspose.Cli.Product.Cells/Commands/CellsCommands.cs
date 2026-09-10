using System.CommandLine;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

/// <summary>Product-owned <c>cells</c> command tree.</summary>
internal static class CellsCommands
{
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
        CellsHelpMetadata.Attach(cells);

        return cells;
    }
}
