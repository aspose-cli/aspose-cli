using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

internal static class EditCommand
{
    public static CommandDefinition<CellsEditRequest, CellsEditResult> Create() =>
        TableCommands.Edit<CellsEditRequest, CellsEditResult>(
            CellsModule.Table,
            "Apply a batch of set, add, delete, move and do operations to a workbook; nothing is written when one fails.",
            new CommandTraits
            {
                Input = CellsInputs.Workbook,
                Encrypt = CellsInputs.EncryptedWorkbook,
                UsesFonts = true,
            },
            CellsFormats.Editable,
            static edit => new CellsEditRequest
            {
                Input = edit.Input,
                Batch = edit.Batch,
                Output = edit.Output,
                Password = edit.Password,
                EncryptPassword = edit.EncryptPassword,
            },
            examples:
            [
                "cells edit book.xlsx --ops ops.json --out revised.xlsx",
                "cells edit book.xlsx --in-place --ops ops.json",
            ],
            links:
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "addresses, the five operation shapes and the read-back"),
            ]);
}
