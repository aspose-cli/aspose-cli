using System.Globalization;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class InspectCommand
{
    private static readonly Dictionary<string, string> DetailNotes = new(StringComparer.Ordinal)
    {
        [InfoDetails.Outline] = $"bookmarks, up to {PdfInfoResult.OutlineLimit}",
        [InfoDetails.Forms] = "form type and field count",
        [InfoDetails.Layers] = "optional content layer names",
        [InfoDetails.Metadata] = "document information such as title and author",
    };

    public static CommandDefinition<PdfInfoRequest, PdfInfoResult> Create()
    {
        var sample = new SampleOption($"the size and rotation of each page, up to {PdfInfoResult.PagePreviewLimit} pages");
        var detail = new DetailOption(InfoDetails.All, DetailNotes);
        return new(
            "inspect",
            "Show PDF structure, security state and metadata.",
            new CommandTraits { Input = PdfInputs.Document },
            [.. sample.Options, .. detail.Options],
            (parse, standard) => new PdfInfoRequest
            {
                Input = standard.Input,
                IncludePreview = sample.Read(parse),
                Details = detail.Read(parse),
                Password = standard.InputPassword,
            },
            Table)
        {
            Examples =
            [
                "pdf inspect report.pdf --output-format json",
                "pdf inspect report.pdf --sample --detail outline fonts permissions",
            ],
            Links =
            [
                CommandHelpLink.Docs(PdfModule.Manifest, "overview", "what each PDF command is for"),
                new("aspose-cli docs reading", "reading every product within bounded windows"),
            ],
        };
    }

    internal static void Table(PdfInfoResult result, TableSurface surface)
    {
        PdfSummary pdf = result.Pdf;
        ResultText.Input(surface, result.Input);
        surface.Out.WriteLine(
            $"pages: {pdf.PageCount}   version: {pdf.Version}   encrypted: {TableText.YesNo(pdf.Encrypted)}   "
            + $"tagged: {TableText.YesNo(pdf.Tagged)}   PDF/A declared: {pdf.PdfaProfile ?? TableText.Missing}");
        surface.Out.WriteLine(
            $"form: {pdf.FormType}   attachments: {pdf.AttachmentCount}   signed: {TableText.YesNo(pdf.Signed)}   "
            + $"password access: {pdf.PasswordType}");
        if (pdf.DistinctPageSizes.Count > 0)
        {
            surface.Out.WriteLine(
                "page sizes: "
                + string.Join(
                    ", ",
                    pdf.DistinctPageSizes.Select(static size =>
                        $"{TableText.Points(size.WidthPoints)} x {TableText.Points(size.HeightPoints)} pt ({size.PageCount})")));
        }

        if (result.PageLabels is { Count: > 0 } labels)
        {
            surface.Out.WriteLine(
                "page labels: "
                + string.Join(
                    ", ",
                    labels.Select(static label =>
                        $"p{label.StartPage} {label.Prefix ?? string.Empty}{label.Style} from {label.StartingValue}")));
        }

        if (result.Pages is { Count: > 0 } pages)
        {
            var table = new TextTable("page", "size (pt)", "rotation");
            foreach (PdfPageInfo page in pages)
            {
                table.AddRow(
                    TableText.Int(page.Page),
                    $"{TableText.Points(page.WidthPoints)} x {TableText.Points(page.HeightPoints)}",
                    TableText.Int(page.Rotation));
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        Sections(result, surface);
    }

    /// <summary>The sections --detail asked for, in the order the JSON result lists them.</summary>
    private static void Sections(PdfInfoResult result, TableSurface surface)
    {
        ResultText.Table(surface, "outline", result.Outline, ["index", "title", "page"],
            static item =>
            [
                item.Index,
                new string(' ', 2 * (item.Level - 1)) + item.Title,
                item.Page?.ToString(CultureInfo.InvariantCulture),
            ]);

        if (result.Forms is { } forms)
        {
            ResultText.Section(surface, "forms");
            surface.Out.WriteLine($"{forms.Type}: {forms.FieldCount} field(s){(forms.ReadOnly ? ", read-only" : string.Empty)}");
        }

        ResultText.Table(surface, "attachments", result.Attachments, ["name", "type", "size"],
            static attachment =>
            [
                attachment.Name,
                attachment.MimeType,
                attachment.SizeBytes is long size ? TableText.Bytes(size) : null,
            ]);
        ResultText.Table(surface, "fonts", result.Fonts, ["font", "embedded", "subset"],
            static font => [font.Name, TableText.YesNo(font.Embedded), TableText.YesNo(font.Subset)]);

        if (result.Permissions is { } permissions)
        {
            ResultText.Section(surface, "permissions");
            surface.Out.WriteLine(
                $"open password: {TableText.YesNo(permissions.HasOpenPassword)}   owner password: {TableText.YesNo(permissions.HasOwnerPassword)}   "
                + $"owner access: {TableText.YesNo(permissions.OwnerAccess)}");
            surface.Out.WriteLine(
                $"print: {TableText.YesNo(permissions.Print)}   copy: {TableText.YesNo(permissions.Copy)}   modify: {TableText.YesNo(permissions.Modify)}   "
                + $"annotate: {TableText.YesNo(permissions.Annotate)}   fill forms: {TableText.YesNo(permissions.FillForms)}   "
                + $"accessibility: {TableText.YesNo(permissions.ExtractAccessibility)}   assemble: {TableText.YesNo(permissions.Assemble)}   "
                + $"high-resolution print: {TableText.YesNo(permissions.PrintHighResolution)}");
        }

        ResultText.Table(surface, "signatures", result.Signatures, ["field", "signed", "valid"],
            static signature =>
            [
                signature.Name,
                TableText.YesNo(signature.Signed),
                signature.Valid is bool valid ? TableText.YesNo(valid) : null,
            ]);
        ResultText.List(surface, "layers", result.Layers);

        ResultText.Properties(surface, "metadata", result.Metadata);
    }
}
