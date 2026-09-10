using System.CommandLine;
using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

/// <summary>The PDF product command group.</summary>
internal static class PdfCommands
{
    public static Command Create(IProductCommandHost<IPdfEngine> host)
    {
        var pdf = new Command("pdf", "PDF automation with page, security and fixed-layout semantics.");
        pdf.Subcommands.Add(InfoCommand.Create(host));
        pdf.Subcommands.Add(QueryCommand.Create(host));
        pdf.Subcommands.Add(ConvertCommand.Create(host));
        pdf.Subcommands.Add(RenderCommand.Create(host));
        pdf.Subcommands.Add(NewCommand.Create(host));
        pdf.Subcommands.Add(MergeCommand.Create(host));
        pdf.Subcommands.Add(SplitCommand.Create(host));
        pdf.Subcommands.Add(ExtractCommand.Create(host));
        pdf.Subcommands.Add(EditCommand.Create(host));
        pdf.Subcommands.Add(ValidateCommand.Create(host));
        pdf.Subcommands.Add(SignCommand.Create(host));
        PdfHelpMetadata.Attach(pdf);
        return pdf;
    }
}
