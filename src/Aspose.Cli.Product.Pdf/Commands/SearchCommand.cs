using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<TableSearchRequest, PdfSearchResult> Create() =>
        TableCommands.Search<PdfSearchResult>(
            "Search the text of a PDF's pages with the engine's TextFragmentAbsorber; each hit names the page's text and gives the engine's rectangle of the match.",
            new CommandTraits { Input = PdfInputs.Document },
            examples:
            [
                "pdf search report.pdf --pattern Contoso",
                "pdf search report.pdf --pattern \"[0-9]{18}\" --regex",
            ],
            links:
            [
                CommandHelpLink.Schema(PdfModule.Manifest, "search-result", "the fields of the search result"),
            ]);
}
