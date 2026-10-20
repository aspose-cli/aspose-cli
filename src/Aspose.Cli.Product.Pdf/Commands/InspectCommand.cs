using System.Globalization;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class InspectCommand
{
    private static readonly Dictionary<string, string> DetailNotes = new(StringComparer.Ordinal)
    {
        [PdfInspectDetails.Outline] = $"bookmarks, up to {PdfInspectResult.OutlineLimit}",
        [PdfInspectDetails.Links] = $"links to web addresses, up to {PdfInspectResult.LinkLimit}",
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

    internal static void Table(PdfInspectResult result, TableSurface surface)
    {
        PdfSummary pdf = result.Pdf;
        ResultText.Input(surface, result.Input);
        surface.Out.WriteLine(
            $"pages: {pdf.PageCount}   version: {pdf.Version}   encrypted: {TableText.YesNo(pdf.Encrypted)}   "
            + $"tagged: {TableText.YesNo(pdf.Tagged)}   PDF/A declared: {pdf.PdfaProfile ?? TableText.Missing}");
        surface.Out.WriteLine(
            $"form: {pdf.FormType ?? TableText.Missing}   attachments: {pdf.AttachmentCount}   signed: {TableText.YesNo(pdf.Signed)}   "
            + $"password access: {pdf.PasswordType ?? TableText.Missing}");
        if (pdf.DistinctPageSizes.Count > 0)
        {
            surface.Out.WriteLine(
                "page sizes: "
                + string.Join(
                    ", ",
                    pdf.DistinctPageSizes.Select(static size =>
                        $"{TableText.Points(size.PageSize.Width)} x {TableText.Points(size.PageSize.Height)} pt ({size.PageCount})")));
        }

        if (result.PageLabels is { Count: > 0 } labels)
        {
            surface.Out.WriteLine(
                "page labels: "
                + string.Join(
                    ", ",
                    labels.Select(static label =>
                        $"p{label.StartPage} {label.Prefix ?? string.Empty}{label.NumberStyle} from {label.Start}")));
        }

        if (result.Pages is { Count: > 0 } pages)
        {
            var table = new TextTable("page", "size (pt)", "rotation");
            foreach (PdfPageInfo page in pages)
            {
                table.AddRow(
                    TableText.Int(page.Page),
                    $"{TableText.Points(page.PageSize.Width)} x {TableText.Points(page.PageSize.Height)}",
                    TableText.Points(page.Rotation));
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        Sections(result, surface);
    }

    /// <summary>The sections --detail asked for, in the order the JSON result lists them.</summary>
    private static void Sections(PdfInspectResult result, TableSurface surface)
    {
        ResultText.Table(surface, "outline", result.Outline, ["bookmark", "title", "page"],
            static item =>
            [
                item.Bookmark,
                new string(' ', 2 * (item.Level - 1)) + item.Title,
                item.Page?.ToString(CultureInfo.InvariantCulture),
            ]);

        ResultText.Table(surface, "links", result.Links, ["page", "rectangle", "url"],
            static link => [link.Page.ToString(CultureInfo.InvariantCulture), PdfText.Rectangle(link.Rect), link.Url]);

        if (result.Forms is { } forms)
        {
            ResultText.Section(surface, "forms");
            surface.Out.WriteLine($"{forms.Kind ?? TableText.Missing}: {forms.FieldCount} field(s){(forms.ReadOnly ? ", read-only" : string.Empty)}");
            if (forms.Fields.Count > 0)
            {
                var table = new TextTable("name", "kind", "value", "accepts", "page", "rectangle", "flags");
                foreach (PdfFormField field in forms.Fields)
                {
                    table.AddRow(
                        field.Name,
                        field.Kind,
                        field.Value,
                        string.Join(", ", field.Options ?? []),
                        field.Page?.ToString(CultureInfo.InvariantCulture),
                        field.Rect is { } rect ? PdfText.Rectangle(rect) : null,
                        string.Join(", ", new[]
                        {
                            field.ReadOnly ? "read-only" : null,
                            field.Required ? "required" : null,
                        }.Where(static value => value is not null)));
                }

                table.WriteTo(surface.Out, surface.Format);
            }
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

        ResultText.Properties(surface, "properties", result.Properties);
    }
}
