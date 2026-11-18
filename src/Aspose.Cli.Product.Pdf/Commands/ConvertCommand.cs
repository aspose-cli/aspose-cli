using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class ConvertCommand
{
    public static CommandDefinition<PdfConvertRequest, PdfConvertResult> Create()
    {
        var pages = new PartRangeOption("page", "every page");
        return new(
            "convert",
            "Convert selected PDF pages to a supported format.",
            new CommandTraits
            {
                Input = PdfInputs.Document,
                Output = OutputTarget.File("Output path; defaults to a sibling using the target extension."),
                UsesFonts = true,
                Target = TargetFormat.Convert("Target PDF export format.", PdfFormats.Definitions),
            },
            [.. pages.Options],
            (parse, standard) =>
            {
                ResolvedOutput output = standard.Output;
                return new PdfConvertRequest
                {
                    Input = standard.Input,
                    Output = output,
                    Pages = pages.ReadRange(parse),
                    Password = standard.InputPassword,
                };
            })
        {
            Examples =
            [
                "pdf convert report.pdf --out report.docx",
                "pdf convert report.pdf --to pdfa-2b --out archive.pdf",
                "pdf convert report.pdf --pages 1-3 --out summary.md",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "pdf-standards", "PDF/A conversion and standards"),
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
            ],
        };
    }
}
