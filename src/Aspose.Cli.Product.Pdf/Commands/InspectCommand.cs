using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class InspectCommand
{
    private static readonly Dictionary<string, string> DetailNotes = new(StringComparer.Ordinal)
    {
        [PdfInspectDetails.Outline] = $"bookmarks, up to {PdfInspectResult.OutlineLimit}",
        [PdfInspectDetails.Links] = $"links to web addresses, other files and places in the document, up to {PdfInspectResult.LinkLimit} out of it and {PdfInspectResult.LinkLimit} within",
        [PdfInspectDetails.Forms] = $"form kind and each field's value, up to {PdfInspectResult.FieldLimit} fields",
        [PdfInspectDetails.Layers] = "optional content layer names",
        [PdfInspectDetails.Properties] = "document information such as title and author",
    };

    public static CommandDefinition<PdfInspectRequest, PdfInspectResult> Create()
    {
        var sample = new SampleOption($"the size and rotation of each page, up to {PdfInspectResult.PagePreviewLimit} pages");
        var detail = new DetailOption(PdfInspectDetails.All, DetailNotes);
        return new(
            "inspect",
            "Show PDF structure, security state and properties.",
            new CommandTraits { Input = PdfInputs.Document },
            [.. sample.Options, .. detail.Options],
            (parse, standard) => new PdfInspectRequest
            {
                Input = standard.Input,
                IncludePreview = sample.Read(parse),
                Details = detail.Read(parse),
                Password = standard.InputPassword,
            })
        {
            Examples =
            [
                "pdf inspect report.pdf --output-format json",
                "pdf inspect report.pdf --sample --detail outline --detail fonts --detail permissions",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }
}
