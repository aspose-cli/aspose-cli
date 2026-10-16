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

    /// <summary>The one source the PDF is created from.</summary>
    public required PdfCreateSource Source { get; init; }

    /// <summary>The page size preset (<c>--page-size</c>), one of <see cref="PageSizes"/>.</summary>
    public string PageSize { get; init; } = PageSizePresets.A4;
    public PdfMargins Margins { get; init; } = PdfMargins.Default;

    /// <summary>The password the output opens with (<c>--encrypt</c>), or null to write it without one.</summary>
    public Secret? EncryptPassword { get; init; }
}

/// <summary>
/// The one source a PDF is created from, as the option that names it reads it: the file's
/// extension never chooses how it is read. The sources below are the only ones.
/// </summary>
public abstract record PdfCreateSource
{
    private protected PdfCreateSource()
    {
    }
}

/// <summary>Image files, one per page (<c>--from-images</c>).</summary>
public sealed record PdfImageSource(IReadOnlyList<SourceFile> Files) : PdfCreateSource;

/// <summary>
/// An HTML file (<c>--from-html</c>); <paramref name="AllowNetworkResources"/> lets the
/// importer request the network resources the HTML names, each of which is disclosed.
/// </summary>
public sealed record PdfHtmlSource(SourceFile File, bool AllowNetworkResources) : PdfCreateSource;

/// <summary>A UTF-8 text file laid out line by line (<c>--from-text</c>).</summary>
public sealed record PdfTextSource(SourceFile File) : PdfCreateSource;

/// <summary>A UTF-8 Markdown file (<c>--from-markdown</c>).</summary>
public sealed record PdfMarkdownSource(SourceFile File) : PdfCreateSource;

/// <summary>Page margins in PDF points.</summary>
public sealed record PdfMargins(double Top, double Right, double Bottom, double Left)
{
    public static PdfMargins Default { get; } = new(36, 36, 36, 36);
}
