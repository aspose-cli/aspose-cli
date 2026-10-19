using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using static Aspose.Cli.Product.Pdf.Engine.PdfGeometry;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>
/// Projects AcroForm and XFA fields (<c>pdf inspect --detail forms</c>) and exports form data
/// (<c>pdf extract --what forms</c>); the field helpers serve editing too.
/// </summary>
internal static class PdfForms
{
    /// <summary>
    /// The document's form and its first <see cref="PdfInspectResult.FieldLimit"/> fields, one
    /// per field name in ordinal order; <paramref name="truncated"/> is set when there are more.
    /// </summary>
    internal static PdfFormInfo Project(Document document, out bool truncated)
    {
        Form form = document.Form;
        (string Name, Field Field)[] named = [.. Named(document)];
        truncated = named.Length > PdfInspectResult.FieldLimit;
        return new PdfFormInfo
        {
            Kind = form.HasXfa ? PdfFormKinds.Xfa : form.Count > 0 ? PdfFormKinds.Acro : null,
            // A radio group's buttons share its name and are one field.
            FieldCount = named.Length,
            ReadOnly = form.HasXfa,
            Fields = [.. named.Take(PdfInspectResult.FieldLimit).Select(item => Project(document, item.Name, item.Field))],
        };
    }

    /// <summary>The field names in ordinal order, each with the field a read projects: the first of its name.</summary>
    internal static IEnumerable<(string Name, Field Field)> Named(Document document) =>
        document.Form.Fields
            .GroupBy(static field => field.FullName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => (group.Key, group.First()));

    /// <summary>The kind and value a read reports for a field; a radio button reports its group's.</summary>
    internal static (string Kind, string? Value) State(Field field)
    {
        Field projected = RadioGroup(field) ?? field;
        return (FieldKind(projected), projected.Value);
    }

    /// <summary>
    /// Projects one field. A check box reports its appearance states as the values it accepts.
    /// The engine lists a radio group as one field per button under the group's name; the
    /// group is projected once, with its selected value and the values of its buttons, and
    /// without a page, since each button has its own.
    /// </summary>
    private static PdfFormField Project(Document document, string name, Field field)
    {
        Field projected = RadioGroup(field) ?? field;
        bool radio = projected is RadioButtonField;
        int? page = radio ? null : PageOf(field);
        IReadOnlyList<string>? options = projected switch
        {
            ChoiceField choice => ChoiceValues(choice),
            CheckboxField checkbox when CheckboxStates(checkbox) is { Count: > 0 } states => states,
            _ => null,
        };
        (string kind, string? value) = State(field);
        return new PdfFormField
        {
            Name = name,
            Kind = kind,
            Value = value,
            Options = options,
            ReadOnly = projected.ReadOnly,
            Required = projected.Required,
            Page = page,
            Rect = page is int number && field.Rect is { } rect ? ToContractRect(document.Pages[number], rect) : null,
        };
    }

    /// <summary>
    /// How many field names have a widget, or a radio button, that lies on no page among the
    /// first <paramref name="pages"/>: review reports them as unresolved.
    /// </summary>
    internal static int FieldsWithoutPage(Document document, int pages) =>
        document.Form.Fields
            .Where(field => PageOf(field) is not int page || page > pages)
            .Select(static field => field.FullName)
            .Distinct(StringComparer.Ordinal)
            .Count();

    /// <summary>The page of the field's widget, or null when it has none or evaluation mode hides it.</summary>
    private static int? PageOf(Field field)
    {
        try
        {
            return field.PageIndex > 0 ? field.PageIndex : null;
        }
        catch (Exception exception) when (PdfEvaluationProfile.IsPageLimit(exception))
        {
            return null;
        }
    }

    /// <summary>The check box state that leaves it unchecked.</summary>
    private const string CheckboxOff = "Off";

    /// <summary>A check box's appearance states without repeats, Off first when it has one.</summary>
    internal static IReadOnlyList<string> CheckboxStates(CheckboxField checkbox) =>
        checkbox.AllowedStates.Distinct(StringComparer.Ordinal)
            .OrderBy(static state => state == CheckboxOff ? 0 : 1)
            .ToArray();

    /// <summary>The radio group a button belongs to; the group, not the button, holds the selection.</summary>
    internal static RadioButtonField? RadioGroup(Field field) =>
        field is RadioButtonOptionField { Parent: RadioButtonField group } ? group : null;

    /// <summary>The values a choice field or radio group accepts, in its order.</summary>
    internal static IReadOnlyList<string> ChoiceValues(ChoiceField choice) =>
        choice.Options.Select(static option => option.Value ?? option.Name).ToArray();

    /// <summary>
    /// Maps an engine field class to the product vocabulary. Specialized text boxes
    /// (date, number, password, barcode, rich text, file selection) are text fields.
    /// </summary>
    private static string FieldKind(Field field) => field switch
    {
        TextBoxField => PdfFormFieldKinds.Text,
        CheckboxField => PdfFormFieldKinds.Checkbox,
        RadioButtonField => PdfFormFieldKinds.Radio,
        ComboBoxField => PdfFormFieldKinds.ComboBox,
        ListBoxField => PdfFormFieldKinds.ListBox,
        ButtonField => PdfFormFieldKinds.Button,
        SignatureField => PdfFormFieldKinds.Signature,
        _ => PdfFormFieldKinds.Other,
    };

    /// <summary>Exports the AcroForm data as one JSON, FDF or XFDF file of the extraction.</summary>
    internal static PublishedFile Export(Document document, FormatDescriptor format, ExtractionGuard guard)
    {
        Action<Aspose.Pdf.Facades.Form, Stream> export = PdfEngineFormats.Of(format.Id).Export
            ?? throw new InvalidOperationException($"'{format.Id}' is not a form data format.");
        EnsureAcroForm(document);
        using var data = new MemoryStream();
        export(new Aspose.Pdf.Facades.Form(document), data);

        return guard.StageAllBytes("form" + (format.OutputExtension ?? format.Extensions[0]), data.ToArray());
    }

    internal static void EnsureAcroForm(Document document)
    {
        if (document.Form.HasXfa)
        {
            throw new CliException(
                PdfDiagnostics.FormXfaUnsupported,
                "XFA forms are read-only in the current PDF command surface.",
                hint: "Convert the XFA form to AcroForm before filling, flattening or exporting it.");
        }
    }
}
