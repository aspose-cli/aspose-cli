using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<CellsReadRequest, CellsReadResult> Create() =>
        TableCommands.Read<CellsReadRequest, CellsReadResult>(
            CellsModule.Table,
            "Read a workbook's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = CellsInputs.Workbook },
            static read => new CellsReadRequest
            {
                Input = read.Input,
                Password = read.Password,
                At = read.At,
                From = read.From,
                Kinds = read.Kinds,
                MaxObjects = read.MaxObjects,
            },
            examples:
            [
                "cells read book.xlsx",
                "cells read book.xlsx --at \"sheet/Sales 2026/range/A1:F6\"",
                "cells read book.xlsx --at \"sheet/Sales 2026\" --kind cell",
            ],
            links:
            [
                CommandHelpLink.Schema(CellsModule.Manifest, "read-result", "the fields of the read result"),
            ],
            listedWhenNamed: CellsKinds.ListedWhenNamed);
}
