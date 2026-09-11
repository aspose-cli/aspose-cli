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

    private static Command Forms(IProductCommandHost<IPdfEngine> host)
    {
        Argument<string> file = PdfOptions.File();
        var password = new PasswordOptions("--password", "the PDF");
        var command = new Command("forms", "List PDF form fields and current values.");
        command.Arguments.Add(file);
        password.AddTo(command);
        command.SetAction(parse => host.Run(parse, context =>
            context.Port.ReadForm(
                context.Paths.ResolveInput(parse.GetRequiredValue(file)),
                new PdfFormReadRequest { Password = password.Resolve(parse, context.Inputs, context.ReadEnvironment) })));
        return command;
    }
}
