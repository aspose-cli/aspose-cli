using System.Text.Json.Serialization;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>The form fields <c>pdf query forms</c> lists.</summary>
public sealed record PdfFormResult() : EngineResultEnvelope("form-result", 2)
{
    /// <summary>The PDF read.</summary>
    [JsonPropertyOrder(-50)]
    public required InputInfo Input { get; init; }

    /// <summary>The kind of form: none, an AcroForm or an XFA form.</summary>
    [AllowedValues(typeof(PdfFormKinds))]
    public required string Type { get; init; }

    /// <summary>Whether the form's fields cannot be filled.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>Each field, and each button of a radio group, in document order.</summary>
    public required IReadOnlyList<PdfFormField> Fields { get; init; }
}

/// <summary>One form field, or one button of a radio group.</summary>
public sealed record PdfFormField
{
    /// <summary>The field's full name, which <c>set_form_field</c> accepts.</summary>
    public required string Name { get; init; }

    /// <summary>The field kind, such as <c>text</c>, <c>checkbox</c>, or <c>radio-option</c> for one button of a radio group.</summary>
    [AllowedValues(typeof(PdfFormFieldTypes))]
    public required string Type { get; init; }

    /// <summary>The field's value; for a radio button, the value its group has selected.</summary>
    public string? Value { get; init; }

    /// <summary>
    /// The values a choice field accepts; for a radio button, the values of its group, which
    /// set_form_field accepts under the group's name.
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>A check box's appearance states, <c>Off</c> first; set_form_field accepts only these.</summary>
    public IReadOnlyList<string>? States { get; init; }

    /// <summary>
    /// The value that checks a check box, when it has exactly one state besides <c>Off</c>, or
    /// that selects a radio button.
    /// </summary>
    public string? OnValue { get; init; }

    /// <summary>Whether the field cannot be filled.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>Whether the form requires a value for the field.</summary>
    public required bool Required { get; init; }

    /// <summary>
    /// The page of the field's widget; absent when it has none or, in evaluation mode, lies
    /// past the pages the engine shows, which EVALUATION_INPUT_TRUNCATED then discloses.
    /// </summary>
    [Minimum(1)]
    public int? Page { get; init; }

    /// <summary>
    /// The field's widget on that page, in points from its top-left corner; for a radio button,
    /// the button. Present only with <see cref="Page"/>.
    /// </summary>
    public PdfRect? Rect { get; init; }
}
