using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>One <c>pdf convert</c>.</summary>
public sealed record PdfConvertRequest
{
    /// <summary>The PDF or HTML to open.</summary>
    public required string Input { get; init; }

    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }

    /// <summary>The pages an html or tiff output holds, or null for every page.</summary>
    public PageRange? Pages { get; init; }

    /// <summary>The password the input opens with.</summary>
    public Secret? Password { get; init; }

    /// <summary>Document.Convert's ConvertErrorAction for a PDF/A output, by its member name; null for any other.</summary>
    public string? ConvertErrorAction { get; init; }
}
