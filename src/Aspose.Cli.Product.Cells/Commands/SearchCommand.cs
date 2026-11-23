using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<TableSearchRequest, CellsSearchResult> Create() =>
        TableCommands.Search<CellsSearchResult>(
            "Search the text of a workbook's cells, as their number formats show it, their formulas and their comments; each hit names the object's address and row.",
            new CommandTraits { Input = CellsInputs.Workbook },
            static (request, result) => result with { Window = SearchOptions.Continue(request.Query, result.Window) },
            examples:
            [
                "cells search book.xlsx --pattern Total",
                "cells search book.xlsx --pattern \"SUM\\(\" --regex",
            ],
            links:
            [
                CommandHelpLink.Schema(CellsModule.Manifest, "search-result", "the fields of the search result"),
            ]);
}
