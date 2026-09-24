using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>Composes bounded PDF data queries under one stable command family.</summary>
internal static class QueryCommand
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var query = new Command("query", "Query bounded PDF pages, forms or text matches.");
        query.Subcommands.Add(ReadCommand.Create(host));
        query.Subcommands.Add(Forms(host));
        query.Subcommands.Add(SearchCommand.Create(host));
        return query;
    }

    private static Command Forms(IProductCommandHost<IPdfEngine> host) =>
        StandardCommand.Create(
            host,
            "forms",
            "List PDF form fields and current values.",
            new CommandTraits { Input = PdfCommands.Document },
            [],
            (_, standard) => standard.Port.ReadForm(
                standard.Input,
                new PdfFormReadRequest { Password = standard.InputPassword }));
}
