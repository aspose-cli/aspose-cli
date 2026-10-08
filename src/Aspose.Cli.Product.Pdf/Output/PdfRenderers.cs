using System.Globalization;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Output;

/// <summary>Human renderers for PDF result families.</summary>
internal static class PdfRenderers
{
    public static void Render(PdfInfoResult result, TableSurface surface)
    {
        PdfSummary pdf = result.Pdf;
        surface.Out.WriteLine($"{result.Source.Path} ({result.Source.Format}, {TableText.Bytes(result.Source.SizeBytes)})");
        surface.Out.WriteLine(
            $"pages: {pdf.PageCount}   version: {pdf.Version}   encrypted: {TableText.YesNo(pdf.Encrypted)}   "
            + $"tagged: {TableText.YesNo(pdf.Tagged)}   PDF/A declared: {pdf.PdfaProfile ?? "no"}");
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
                        $"{Points(size.WidthPoints)} x {Points(size.HeightPoints)} pt ({size.PageCount})")));
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
                    $"{Points(page.WidthPoints)} x {Points(page.HeightPoints)}",
                    TableText.Int(page.Rotation));
            }

            table.WriteTo(surface.Out, surface.Format);
        }

        RenderDetails(result, surface);
    }

    /// <summary>The sections --detail asked for, in the order the JSON result lists them.</summary>
    private static void RenderDetails(PdfInfoResult result, TableSurface surface)
    {
        ResultText.Table(surface, "outline", result.Outline, ["index", "title", "page"],
            static item =>
            [
                item.Index,
                new string(' ', 2 * (item.Level - 1)) + item.Title,
                item.Page?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
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
                attachment.MimeType ?? string.Empty,
                attachment.SizeBytes is long size ? TableText.Bytes(size) : string.Empty,
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
                signature.Valid is bool valid ? TableText.YesNo(valid) : string.Empty,
            ]);
        ResultText.List(surface, "layers", result.Layers);

        ResultText.Properties(surface, "metadata", result.Metadata, nameColumn: "property");
    }

    public static void Render(PdfReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path}: {result.PageCount} page(s) ({result.Mode})");
        foreach (PdfPageText page in result.Pages)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- page {page.Page}{(page.Truncated ? " (truncated)" : string.Empty)} ---");
            surface.Out.WriteLine(page.Text);
        }
    }

    public static void Render(PdfConvertResult result, TableSurface surface)
    {
        foreach (var output in result.Outputs)
        {
            ResultText.Produced(surface, output, result.Pages is null ? null : $"pages {result.Pages}");
        }
    }

    public static void Render(PdfRenderResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"rendered {result.Outputs.Count} page(s)");
        foreach (PdfPageOutput output in result.Outputs)
        {
            surface.Out.WriteLine(
                $"  page {output.Page}: {output.Output.Path} ({output.Output.Format}, {TableText.Bytes(output.Output.SizeBytes)})");
        }

        if (result.Grid is { } grid)
        {
            surface.Out.WriteLine(
                $"grid: lines every {grid.Spacing} {grid.Unit}, labelled every {grid.LabelSpacing} {grid.Unit}, origin {grid.Origin}");
        }
    }

    public static void Render(PdfWriteResult result, TableSurface surface) =>
        surface.Out.WriteLine(
            $"{result.Action}: {result.Output.Path} ({result.Output.Format}, {TableText.Bytes(result.Output.SizeBytes)})");

    public static void Render(PdfSplitResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"wrote {result.Outputs.Count} PDF part(s)");
        foreach (PdfSplitOutput output in result.Outputs)
        {
            string bookmark = output.Bookmark is null ? string.Empty : $" [{output.Bookmark}]";
            surface.Out.WriteLine(
                $"  {output.Index}: pages {output.Pages}{bookmark} -> {output.Output.Path} ({TableText.Bytes(output.Output.SizeBytes)})");
        }
    }

    public static void Render(PdfExtractResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"extracted {result.Items.Count} {result.What} item(s)");
        foreach (PdfExtractedItem item in result.Items)
        {
            string page = item.Page.HasValue ? $" page {item.Page.Value}" : string.Empty;
            surface.Out.WriteLine($"  {item.Path} ({item.Kind}{page}, {TableText.Bytes(item.SizeBytes)})");
        }
    }

    public static void Render(PdfEditResult result, TableSurface surface)
    {
        ResultText.Edit(surface, result.DryRun, result.Output, result.Applied, result.Backup);
        if (result.PagesTouched is { Count: > 0 } pages)
        {
            surface.Out.WriteLine($"pages touched: {string.Join(", ", pages)}");
        }

        if (result.Verification is { } verification)
        {
            surface.Out.WriteLine(
                $"verification: {(verification.Ok ? "ok" : "needs attention")} "
                + $"({verification.CheckedOps.Count} operation(s) read back: {(verification.CheckedOps.Count == 0 ? "none" : string.Join(", ", verification.CheckedOps))})");
            foreach (VerificationIssue issue in verification.Issues)
            {
                string location = issue.Location is null ? string.Empty : $" [{issue.Location}]";
                surface.Out.WriteLine($"  {issue.Code}{location}: {issue.Message}");
                if (issue.Hint is { } hint)
                {
                    surface.Out.WriteLine($"    hint: {hint}");
                }
            }
        }
    }

    public static void Render(PdfFormResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Type} form: {result.Fields.Count} field(s)");
        var table = new TextTable("name", "type", "value", "on value", "accepts", "page", "rectangle", "flags");
        foreach (PdfFormField field in result.Fields)
        {
            table.AddRow(
                field.Name,
                field.Type,
                field.Value ?? string.Empty,
                field.OnValue ?? string.Empty,
                string.Join(", ", field.States ?? field.Options ?? []),
                field.Page?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                field.Rect is { } rect ? Rectangle(rect) : string.Empty,
                string.Join(", ", new[]
                {
                    field.ReadOnly ? "read-only" : null,
                    field.Required ? "required" : null,
                }.Where(static value => value is not null)));
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(PdfFormExportResult result, TableSurface surface) =>
        surface.Out.WriteLine(
            $"exported form data: {result.Output.Path} ({result.Output.Format}, {TableText.Bytes(result.Output.SizeBytes)})");

    public static void Render(PdfSearchResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"found {result.Hits.Count} hit(s)");
        var table = new TextTable("page", "occurrence", "rectangle", "text");
        foreach (PdfSearchHit hit in result.Hits)
        {
            table.AddRow(
                TableText.Int(hit.Page),
                TableText.Int(hit.Occurrence),
                Rectangle(hit.Rect),
                hit.Context ?? hit.Snippet);
        }

        table.WriteTo(surface.Out, surface.Format);
    }

    public static void Render(PdfValidateResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Profile}: {(result.Valid ? "valid" : "not valid")}");
        foreach (string issue in result.Issues)
        {
            surface.Out.WriteLine($"  {issue}");
        }
    }

    public static void Render(PdfSignResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"signed: {result.Output.Path} ({TableText.Bytes(result.Output.SizeBytes)})");
        surface.Out.WriteLine($"  field: {result.Signature.Name}");
        surface.Out.WriteLine($"  valid: {result.Signature.Valid?.ToString().ToLowerInvariant() ?? "unknown"}");
        surface.Out.WriteLine($"  appearance: {(result.Visible ? $"visible on page {result.Page}" : "invisible")}");
    }

    private static string Points(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A rectangle as its top-left corner and size, such as <c>72,202 20x20</c>.</summary>
    private static string Rectangle(PdfRect rect) =>
        $"{Points(rect.X)},{Points(rect.Y)} {Points(rect.Width)}x{Points(rect.Height)}";
}
