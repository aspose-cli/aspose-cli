using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ReadCommand
{
    public static CommandDefinition<PdfReadRequest, PdfReadResult> Create() =>
        TableCommands.Read<PdfReadRequest, PdfReadResult>(
            PdfModule.Table,
            "Read a PDF's objects, each with every row of the object table, and its inventory.",
            new CommandTraits { Input = PdfInputs.Document },
            static read => new PdfReadRequest
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
                "pdf read report.pdf",
                "pdf read report.pdf --kind field",
                "pdf read report.pdf --at page/1/text",
                "pdf read report.pdf --kind text --max-objects 20",
            ],
            links:
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "addresses, windows and the inventory"),
            ],
            listedWhenNamed: PdfKinds.ListedWhenNamed);
}
