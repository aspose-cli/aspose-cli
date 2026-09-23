using System.Globalization;
using Aspose.Cli.Product.Pdf.Contracts;
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
            $"pages: {pdf.Pages}   version: {pdf.Version}   encrypted: {TableText.YesNo(pdf.Encrypted)}   "
            + $"tagged: {TableText.YesNo(pdf.Tagged)}   PDF/A: {TableText.YesNo(pdf.PdfaCompliant)}");
        surface.Out.WriteLine(
            $"form: {pdf.FormType}   attachments: {pdf.Attachments}   signed: {TableText.YesNo(pdf.Signed)}   "
            + $"password access: {pdf.PasswordType}");
        if (pdf.DistinctPageSizes.Count > 0)
        {
            surface.Out.WriteLine(
                "page sizes: "
                + string.Join(
                    ", ",
                    pdf.DistinctPageSizes.Select(static size =>
                        $"{Points(size.WidthPoints)} x {Points(size.HeightPoints)} pt ({size.Count})")));
        }

        if (result.PageLabels is { Count: > 0 } labels)
        {
            surface.Out.WriteLine(
                "page labels: "
                + string.Join(
                    ", ",
                    labels.Select(static label =>
                        $"p{label.StartPage} {label.Prefix ?? string.Empty}{label.NumberingStyle} from {label.StartingValue}")));
        }

        if (result.Pages is { Count: > 0 } pages)
        {
            var table = new TextTable("page", "size (pt)", "rotation");
            foreach (PdfPageInfo page in pages)
            {
                table.AddRow(
                    TableText.Int(page.Number),
                    $"{Points(page.WidthPoints)} x {Points(page.HeightPoints)}",
                    TableText.Int(page.Rotation));
            }

            table.WriteTo(surface.Out, surface.Format);
        }
    }

    public static void Render(PdfReadResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Source.Path}: pages {result.Window.Pages} of {result.Window.Of} ({result.Mode})");
        foreach (PdfPageText page in result.Pages)
        {
            surface.Out.WriteLine();
            surface.Out.WriteLine($"--- page {page.Number}{(page.Truncated ? " (truncated)" : string.Empty)} ---");
            surface.Out.WriteLine(page.Text);
        }

        if (result.Next is not null)
        {
            surface.Out.WriteLine($"next: {result.Next}");
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
    }

    public static void Render(PdfFormResult result, TableSurface surface)
    {
        surface.Out.WriteLine($"{result.Type} form: {result.Fields.Count} field(s)");
        var table = new TextTable("name", "type", "value", "page", "flags");
        foreach (PdfFormField field in result.Fields)
        {
            table.AddRow(
                field.Name,
                field.Type,
                field.Value ?? string.Empty,
                field.Page?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
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
        surface.Out.WriteLine($"found {result.Hits.Count} hit(s){(result.Truncated ? " (truncated)" : string.Empty)}");
        var table = new TextTable("page", "occurrence", "rectangle", "text");
        foreach (PdfSearchHit hit in result.Hits)
        {
            table.AddRow(
                TableText.Int(hit.Page),
                TableText.Int(hit.Occurrence),
                $"{Points(hit.Rect.X)},{Points(hit.Rect.Y)} {Points(hit.Rect.Width)}x{Points(hit.Rect.Height)}",
                hit.Snippet);
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
}
