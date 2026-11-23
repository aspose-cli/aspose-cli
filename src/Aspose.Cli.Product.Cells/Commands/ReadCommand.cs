using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<TableReadRequest, CellsReadResult> Create() =>
        TableCommands.Read<CellsReadResult>(
            CellsModule.Table,
            "Read a workbook's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = CellsInputs.Workbook },
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
