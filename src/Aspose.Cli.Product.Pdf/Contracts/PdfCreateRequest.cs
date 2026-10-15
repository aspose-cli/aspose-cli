using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for creating one PDF from exactly one source family.</summary>
public sealed record PdfCreateRequest
{
    /// <summary>The page size presets <c>pdf create --page-size</c> accepts.</summary>
    public static IReadOnlyList<string> PageSizes { get; } =
        [PageSizePresets.A3, PageSizePresets.A4, PageSizePresets.A5, PageSizePresets.Letter, PageSizePresets.Legal, PageSizePresets.Tabloid];

    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public IReadOnlyList<string>? ImagePaths { get; init; }
    public string? HtmlPath { get; init; }

    /// <summary>Lets the HTML importer request the network resources the HTML names; each is disclosed.</summary>
    public bool AllowNetworkResources { get; init; }

    /// <summary>A UTF-8 text file, read as Markdown when its extension is <c>.md</c>.</summary>
    public string? TextPath { get; init; }
    /// <summary>The page size preset (<c>--page-size</c>), one of <see cref="PageSizes"/>.</summary>
    public string PageSize { get; init; } = PageSizePresets.A4;
    public PdfMargins Margins { get; init; } = PdfMargins.Default;

    /// <summary>The password the output opens with (<c>--encrypt</c>), or null to write it without one.</summary>
    public Secret? EncryptPassword { get; init; }
}

/// <summary>Page margins in PDF points.</summary>
public sealed record PdfMargins(double Top, double Right, double Bottom, double Left)
{
    public static PdfMargins Default { get; } = new(36, 36, 36, 36);
}
