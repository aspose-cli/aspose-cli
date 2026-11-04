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
    /// The command line that writes an editable file of the output's format from the input over
    /// the output, with the input password's environment variable when one gave it, which the
    /// conversion's <c>LOSSY_CONVERSION</c> names; null when no other product writes an editable
    /// file of the format.
    /// </summary>
    public string? EditableConversion { get; init; }
}
