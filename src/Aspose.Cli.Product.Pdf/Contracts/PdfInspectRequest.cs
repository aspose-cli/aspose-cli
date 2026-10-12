namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for structural PDF inspection.</summary>
public sealed record PdfInspectRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Accepted values of <c>pdf inspect --detail</c>.</summary>
public static class PdfInspectDetails
{
    /// <summary>The bookmarks, in reading order.</summary>
    public const string Outline = "outline";

    /// <summary>The links to web addresses, with their pages and rectangles.</summary>
    public const string Links = "links";

    /// <summary>The interactive form and its fields' values.</summary>
    public const string Forms = "forms";

    /// <summary>The embedded files.</summary>
    public const string Attachments = "attachments";

    /// <summary>The distinct font resources.</summary>
    public const string Fonts = "fonts";

    /// <summary>The passwords and permissions.</summary>
    public const string Permissions = "permissions";

    /// <summary>The signature fields.</summary>
    public const string Signatures = "signatures";

    /// <summary>The names of the optional content layers.</summary>
    public const string Layers = "layers";

    /// <summary>The document information and XMP properties.</summary>
    public const string Properties = "properties";

    /// <summary>Every detail id, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Outline, Links, Forms, Attachments, Fonts, Permissions, Signatures, Layers, Properties];
}

/// <summary>The kinds of interactive form a PDF can have.</summary>
public static class PdfFormKinds
{
    /// <summary>An AcroForm.</summary>
    public const string Acro = "acro";

    /// <summary>An XFA form.</summary>
    public const string Xfa = "xfa";
}

/// <summary>The product vocabulary of form field kinds, independent of the engine's class names.</summary>
public static class PdfFormFieldKinds
{
    /// <summary>A text box.</summary>
    public const string Text = "text";

    /// <summary>A check box.</summary>
    public const string Checkbox = "checkbox";

    /// <summary>A radio group.</summary>
    public const string Radio = "radio";

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
