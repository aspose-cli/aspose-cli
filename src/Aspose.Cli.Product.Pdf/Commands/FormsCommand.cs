using System.Globalization;
using Aspose.Cli.Sdk.Extensibility.Output;

namespace Aspose.Cli.Product.Pdf.Commands;

internal static class FormsCommand
{
    public static CommandDefinition<PdfFormReadRequest, PdfFormResult> Create() => new(
        "forms",
        "List PDF form fields and current values.",
        new CommandTraits { Input = PdfInputs.Document },
        [],
        static (_, standard) => new PdfFormReadRequest
        {
            Input = standard.Input,
            Password = standard.InputPassword,
        },
        Table);

    internal static void Table(PdfFormResult result, TableSurface surface)
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
                field.Rect is { } rect ? PdfText.Rectangle(rect) : string.Empty,
                string.Join(", ", new[]
                {
                    field.ReadOnly ? "read-only" : null,
                    field.Required ? "required" : null,
                }.Where(static value => value is not null)));
        }

        table.WriteTo(surface.Out, surface.Format);
    }
}
