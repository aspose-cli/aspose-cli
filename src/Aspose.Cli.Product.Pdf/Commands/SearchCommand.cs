using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SearchCommand
{
    public static CommandDefinition<PdfSearchRequest, PdfSearchResult> Create() =>
        TableCommands.Search<PdfSearchRequest, PdfSearchResult>(
            "Search the text of a PDF's pages with the engine's TextFragmentAbsorber; each hit names the page's text and gives the engine's rectangle of the match.",
            new CommandTraits { Input = PdfInputs.Document },
            static search => new PdfSearchRequest { Input = search.Input, Password = search.Password, Query = search.Query },
            static (request, result) => result with { Window = SearchOptions.Continue(request.Query, result.Window) },
            examples:
            [
                "pdf search report.pdf --pattern Contoso",
                "pdf search report.pdf --pattern \"[0-9]{18}\" --regex",
            ],
            links:
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
            ]);
}
