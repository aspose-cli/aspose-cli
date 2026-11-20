using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SplitCommand
{
    public static CommandDefinition<PdfSplitRequest, PdfSplitResult> Create()
    {
        var split = new SplitOptions([SplitBy.Pages], groups: true);
        return new(
            "split",
            "Split a PDF by page ranges, each part a new document with Pages.Add of its pages; every part is written or none.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.Directory("Directory that receives the parts, named <stem>.<n>.pdf.", PdfFormats.Document),
                Encrypt = new EncryptedOutput("every output PDF"),
            },
            [.. split.Options],
            (parse, standard) => new PdfSplitRequest
            {
                Input = standard.Input,
                Plan = split.Read(parse),
                Output = standard.DirectoryOutput,
                Password = standard.InputPassword,
                EncryptPassword = standard.EncryptPassword(),
            })
        {
            Examples =
            [
                "pdf split report.pdf --by pages --every 2 --out-dir parts",
                "pdf split report.pdf --by pages --groups 1-3 --groups 4-6 --out-dir parts",
            ],
            Links =
            [
                CommandHelpLink.Schema(PdfModule.Manifest, "split-result", "the fields of the split result"),
            ],
        };
    }
}
