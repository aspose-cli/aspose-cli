namespace Aspose.Cli.Product.Pdf.Contracts;

public sealed record PdfFormReadRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>The product vocabulary of form field kinds, independent of the engine's class names.</summary>
public static class PdfFormFieldTypes
{
    /// <summary>A text box.</summary>
    public const string Text = "text";

    /// <summary>A check box.</summary>
    public const string Checkbox = "checkbox";

    /// <summary>A radio group.</summary>
    public const string Radio = "radio";

    /// <summary>One button of a radio group.</summary>
    public const string RadioOption = "radio-option";

    /// <summary>A drop-down list.</summary>
    public const string ComboBox = "combobox";

    /// <summary>A list box.</summary>
    public const string ListBox = "listbox";

    /// <summary>A push button.</summary>
    public const string Button = "button";

    /// <summary>A signature field.</summary>
    public const string Signature = "signature";

    /// <summary>A field kind the vocabulary does not name.</summary>
    public const string Other = "other";
}
