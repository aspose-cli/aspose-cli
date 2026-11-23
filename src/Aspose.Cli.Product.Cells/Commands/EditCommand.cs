using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Commands;

internal static class EditCommand
{
    public static CommandDefinition<TableEditRequest, CellsEditResult> Create() =>
        TableCommands.Edit<CellsEditResult>(
            CellsModule.Table,
            "Apply a batch of set, add, delete, move and do operations to a workbook; nothing is written when one fails.",
            new CommandTraits
            {
                Input = CellsInputs.Workbook,
                Encrypt = CellsInputs.EncryptedWorkbook,
                UsesFonts = true,
            },
            CellsFormats.Editable,
            examples:
            [
                "cells edit book.xlsx --ops ops.json --out revised.xlsx",
                "cells edit book.xlsx --in-place --ops ops.json",
            ],
            links:
            [
                CommandHelpLink.Schema(CellsModule.Manifest, "edit-result", "the fields of the edit result"),
            ]);
}
