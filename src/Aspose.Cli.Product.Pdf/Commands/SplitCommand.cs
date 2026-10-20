using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class SplitCommand
{
    public static CommandDefinition<PdfSplitRequest, PdfSplitResult> Create()
    {
        var split = new SplitOptions([SplitBy.Pages, SplitBy.Bookmarks], groups: true);
        return new(
            "split",
            "Split a PDF into an atomic output set.",
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
            },
            Table)
        {
            Examples =
            [
                "pdf split report.pdf --by pages --every 2 --out-dir parts",
                "pdf split report.pdf --by pages --groups 1-3 --groups 4-6 --out-dir parts",
                "pdf split report.pdf --by bookmarks --out-dir chapters",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "assemble-review-report", "dividing and assembling PDFs"),
            ],
        };
    }

    internal static void Table(PdfSplitResult result, TableSurface surface)
    {
        ResultText.Files(surface, "wrote", "part", [.. result.Outputs.Select(static (output, index) =>
            new ProducedFile(
                $"part {index + 1}",
                output.Path,
                output.Format,
                output.SizeBytes,
                $"pages {output.Pages}"))]);
    }
}
