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

    /// <summary>The interactive form: its type and field count.</summary>
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

    /// <summary>The document information and XMP metadata.</summary>
    public const string Metadata = "metadata";

    /// <summary>Every detail id, in documentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [Outline, Forms, Attachments, Fonts, Permissions, Signatures, Layers, Metadata];
}
