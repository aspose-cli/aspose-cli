using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Forms;
using static Aspose.Cli.Product.Pdf.Engine.PdfEngineSupport;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Owns AcroForm and XFA read, fill and export behavior.</summary>
internal sealed class PdfFormService
{
    private readonly OutputPipeline<Document> _outputs;
    private readonly PdfDocumentLoader _loader;

    internal PdfFormService(
        OutputPipeline<Document> outputs,
        PdfDocumentLoader loader)
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        _loader = loader;
    }

    /// <inheritdoc />

    public PdfFormResult ReadForm(string filePath, PdfFormReadRequest request)
    {
        LicenseState state = _outputs.License;
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        Form form = loaded.Document.Form;
        var unpaged = new List<string>();
        var fields = form.Fields.OrderBy(static field => field.FullName, StringComparer.Ordinal)
            .Select(field => Project(loaded.Document, field, unpaged))
            .ToArray();
        return new PdfFormResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Type = form.HasXfa ? "xfa" : form.Count > 0 ? "acro" : "none",
            ReadOnly = form.HasXfa,
            Fields = fields,
            License = EnvelopeParts.License(state),
            Warnings = unpaged.Count > 0 ? [PdfEvaluation.FieldsWithoutPage(loaded.Document.Pages.Count, unpaged)] : null,
        };
    }

    /// <summary>
    /// Projects one field. A check box reports its appearance states and, when it has exactly
    /// one state besides Off, that state as the value that checks it. The engine lists a radio
    /// group as one field per button under the group's name; each reports the group's selected
    /// value as its value, the group's values as its options and its own value as the one that
    /// selects it, and each has its own rectangle. A field whose page evaluation mode hides is
    /// added to <paramref name="unpaged"/>.
    /// </summary>
    private static PdfFormField Project(Document document, Field field, List<string> unpaged)
    {
        int? page = PageOf(field, unpaged);
        IReadOnlyList<string>? options = null;
        IReadOnlyList<string>? states = null;
        string? onValue = null;
        string? value = field.Value;
        switch (field)
        {
            case ChoiceField choice:
                options = ChoiceValues(choice);
                break;
            case CheckboxField checkbox:
                IReadOnlyList<string> allowed = CheckboxStates(checkbox);
                string[] on = allowed.Where(static state => state != CheckboxOff).ToArray();
                states = allowed.Count > 0 ? allowed : null;
                onValue = on.Length == 1 ? on[0] : null;
                break;
            case RadioButtonOptionField button:
                RadioButtonField? group = RadioGroup(button);
                options = group is null ? null : ChoiceValues(group);
                value = group?.Value ?? value;
                onValue = string.IsNullOrEmpty(button.OptionName) ? null : button.OptionName;
                break;
        }

        return new PdfFormField
        {
            Name = field.FullName,
            Type = FieldType(field),
            Value = value,
            Options = options,
            States = states,
            OnValue = onValue,
            ReadOnly = field.ReadOnly,
            Required = field.Required,
            Page = page,
            Rect = page is int number && field.Rect is { } rect ? ToContractRect(document.Pages[number], rect) : null,
        };
    }

    private static int? PageOf(Field field, List<string> unpaged)
    {
        try
        {
            return field.PageIndex > 0 ? field.PageIndex : null;
        }
        catch (Exception exception) when (PdfEvaluation.IsCollectionLimit(exception))
        {
            unpaged.Add(field.FullName);
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
    private static string FieldType(Field field) => field switch
    {
        TextBoxField => PdfFormFieldTypes.Text,
        CheckboxField => PdfFormFieldTypes.Checkbox,
        RadioButtonField => PdfFormFieldTypes.Radio,
        RadioButtonOptionField => PdfFormFieldTypes.RadioOption,
        ComboBoxField => PdfFormFieldTypes.ComboBox,
        ListBoxField => PdfFormFieldTypes.ListBox,
        ButtonField => PdfFormFieldTypes.Button,
        SignatureField => PdfFormFieldTypes.Signature,
        _ => PdfFormFieldTypes.Other,
    };

    public PdfFormExportResult ExportForm(string filePath, PdfFormExportRequest request)
    {
        LicenseState state = _outputs.License;
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        EnsureAcroForm(loaded.Document);
        long size = _outputs.Write(request.Output.Path, request.Output.Overwrite, null, temp =>
        {
            var facade = new Aspose.Pdf.Facades.Form(loaded.Document);
            using FileStream stream = File.Create(temp);
            switch (request.Output.Format.Id)
            {
                case "json": facade.ExportJson(stream, indented: true); break;
                case "fdf": facade.ExportFdf(stream); break;
                case "xfdf": facade.ExportXfdf(stream); break;
                default: throw new InvalidOperationException($"'{request.Output.Format.Id}' is not a form data format.");
            }
        });
        return new PdfFormExportResult
        {
            Input = PdfInfoProjection.Source(filePath),
            Output = BuildOutput(request.Output.Path, request.Output.Format.Id, size),
            License = EnvelopeParts.License(state),
        };
    }
}
