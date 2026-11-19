using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<CellsSearchRequest, CellsSearchResult> Create() =>
        TableCommands.Search<CellsSearchRequest, CellsSearchResult>(
            "Search the text of a workbook's cells, as their number formats show it, their formulas and their comments; each hit names the object's address and row.",
            new CommandTraits { Input = CellsInputs.Workbook },
            static search => new CellsSearchRequest { Input = search.Input, Password = search.Password, Query = search.Query },
            static (request, result) => result with { Window = SearchOptions.Continue(request.Query, result.Window) },
            examples:
            [
                "cells search book.xlsx --pattern Total",
                "cells search book.xlsx --pattern \"SUM\\(\" --regex",
            ],
            links:
            [
                CommandHelpLink.Docs(CellsModule.Manifest, "overview", "what each Cells command is for"),
            ]);
}
