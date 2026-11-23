using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<TableReadRequest, PdfReadResult> Create() =>
        TableCommands.Read<PdfReadResult>(
            PdfModule.Table,
            "Read a PDF's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = PdfInputs.Document },
            examples:
            [
                "pdf read report.pdf",
                "pdf read report.pdf --kind field",
                "pdf read report.pdf --at page/1/text",
                "pdf read report.pdf --kind text --max-objects 20",
            ],
            links:
            [
                CommandHelpLink.Schema(PdfModule.Manifest, "read-result", "the fields of the read result"),
            ],
            listedWhenNamed: PdfKinds.ListedWhenNamed);
}
