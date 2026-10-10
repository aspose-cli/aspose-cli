using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for structural PDF inspection.</summary>
public sealed record PdfInfoRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for a budgeted page-text read.</summary>
public sealed record PdfReadRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public PageRange? Pages { get; init; }
    public string Mode { get; init; } = PdfReadModes.Plain;
    public int MaxCharacters { get; init; } = 20_000;
    public Secret? Password { get; init; }
}

/// <summary>Options for PDF conversion.</summary>
public sealed record PdfConvertRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for page rendering.</summary>
public sealed record PdfRenderRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public PageRange? Pages { get; init; }
    public bool AllPages { get; init; }
    public int Dpi { get; init; } = RenderPixelGuard.DefaultDpi;

    /// <summary>Spacing in points of a coordinate grid drawn on raster output, or null for none.</summary>
    public int? Grid { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for creating one PDF from exactly one source family.</summary>
public sealed record NewPdfRequest
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
}

/// <summary>Page margins in PDF points.</summary>
public sealed record PdfMargins(double Top, double Right, double Bottom, double Left)
{
    public static PdfMargins Default { get; } = new(36, 36, 36, 36);
}

/// <summary>Options for merging PDF inputs.</summary>
public sealed record PdfMergeRequest
{
    public required IReadOnlyList<string> InputPaths { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public bool PreserveBookmarks { get; init; } = true;
    public Secret? Password { get; init; }
}

/// <summary>Options for transactional PDF splitting.</summary>
public sealed record PdfSplitRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public IReadOnlyList<PageRange>? PageGroups { get; init; }
    public int? Every { get; init; }
    public bool ByBookmarks { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for bounded PDF extraction.</summary>
public sealed record PdfExtractRequest : IPdfExtractRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required string What { get; init; }
    /// <summary>The resolved directory that receives the files.</summary>
    public required ResolvedDirectory Output { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }

    /// <summary>Whether each table's UTF-8 CSV starts with a byte order mark.</summary>
    public bool ByteOrderMark { get; init; }
}

/// <summary>
/// A request of <c>pdf extract</c>: assets, text and tables (<see cref="PdfExtractRequest"/>) or
/// form data (<see cref="PdfFormExportRequest"/>), each with its own result.
/// </summary>
public interface IPdfExtractRequest
{
    /// <summary>The PDF to open.</summary>
    string Input { get; }
}

/// <summary>Stable PDF read projection modes.</summary>
public static class PdfReadModes
{
    public const string Plain = "plain";
    public const string Layout = "layout";
    public static IReadOnlyList<string> All { get; } = [Plain, Layout];
}

/// <summary>Stable PDF extraction families.</summary>
public static class PdfExtractKinds
{
    public static IReadOnlyList<string> All { get; } = ["images", "attachments", "text", "tables"];
}

public sealed record PdfEditRequest
{
    /// <summary>The PDF to edit.</summary>
    public required string Input { get; init; }

    /// <summary>The validated operation batch.</summary>
    public required PdfOpsBatch Batch { get; init; }

    /// <summary>The resolved output: its path, overwrite permission and in-place backup.</summary>
    public required ResolvedOutput Output { get; init; }
    public EditCommandOptions Options { get; init; } = new();
    public Secret? Password { get; init; }

    /// <summary>Whether to read the staged output back against the effect of each operation.</summary>
    public bool Verify { get; init; }

    /// <summary>The operations' secrets by the environment variable their <c>*Env</c> fields name.</summary>
    public IReadOnlyDictionary<string, Secret>? OpSecrets { get; init; }
}

public sealed record PdfFormReadRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for exporting form data, the <c>--what forms</c> request of <c>pdf extract</c>.</summary>
public sealed record PdfFormExportRequest : IPdfExtractRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public Secret? Password { get; init; }
}
public sealed record PdfSearchRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required SearchQuery Query { get; init; }
    public PageRange? Pages { get; init; }
    public Secret? Password { get; init; }
}
public sealed record PdfValidateRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required string Profile { get; init; }
    public Secret? Password { get; init; }
}

/// <summary>Options for applying one PKCS#7 PDF signature.</summary>
public sealed record PdfSignRequest
{
    /// <summary>The PDF to open.</summary>
    public required string Input { get; init; }
    public required string CertificatePath { get; init; }
    public required Secret CertificatePassword { get; init; }
    /// <summary>The resolved output: its format, path and overwrite permission.</summary>
    public required ResolvedOutput Output { get; init; }
    public Secret? Password { get; init; }
    public int Page { get; init; } = 1;
    public bool Visible { get; init; }
    public PdfSignatureRect? Rect { get; init; }
    public string? Reason { get; init; }
    public string? Location { get; init; }
    public string? Contact { get; init; }
}

/// <summary>A visible-signature rectangle in PDF points from the lower-left origin.</summary>
public sealed record PdfSignatureRect(double X, double Y, double Width, double Height);

/// <summary>Accepted values of <c>pdf inspect --detail</c>.</summary>
public static class InfoDetails
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
