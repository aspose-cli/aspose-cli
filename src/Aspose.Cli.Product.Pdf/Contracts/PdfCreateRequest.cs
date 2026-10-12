using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for creating one PDF from exactly one source family.</summary>
public sealed record PdfCreateRequest
{
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public IReadOnlyList<string>? ImagePaths { get; init; }
    public string? HtmlPath { get; init; }

    /// <summary>Lets the HTML importer request the network resources the HTML names; each is disclosed.</summary>
    public bool AllowNetworkResources { get; init; }

    /// <summary>A UTF-8 text file, read as Markdown when its extension is <c>.md</c>.</summary>
    public string? TextPath { get; init; }
    public string PageSize { get; init; } = "A4";
    public PdfMargins Margins { get; init; } = PdfMargins.Default;

    /// <summary>The password the output opens with (<c>--encrypt</c>), or null to write it without one.</summary>
    public Secret? EncryptPassword { get; init; }
}

/// <summary>Page margins in PDF points.</summary>
public sealed record PdfMargins(double Top, double Right, double Bottom, double Left)
{
    public static PdfMargins Default { get; } = new(36, 36, 36, 36);
}
