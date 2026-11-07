using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for PDF conversion.</summary>
public sealed record PdfConvertRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }

    /// <summary>
    /// The command lines, quoted and followed by <c>instead</c> and the part they leave to delete,
    /// that write an editable file of the output's format from the input, or from the selected
    /// pages split from it, over the output, with the input password's environment variable when
    /// one gave it, which the conversion's <c>LOSSY_CONVERSION</c> names; null when no other product
    /// writes an editable file of the format.
    /// </summary>
    public string? EditableConversion { get; init; }

    /// <summary>
    /// The quoted command line that reads the input's form fields with their values, with the
    /// input password's environment variable when one gave it, which the warning that the output
    /// leaves out field values names; null for a conversion made without a command line.
    /// </summary>
    public string? FormsInspection { get; init; }
}
