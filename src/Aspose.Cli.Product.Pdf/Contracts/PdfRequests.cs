using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Pdf.Contracts;

/// <summary>Options for structural PDF inspection.</summary>
public sealed record PdfInfoRequest
{
    public bool IncludePreview { get; init; }
    public IReadOnlyList<string>? Details { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for a budgeted page-text read.</summary>
public sealed record PdfReadRequest
{
    public PageRange? Pages { get; init; }
    public string Mode { get; init; } = PdfReadModes.Plain;
    public int MaxCharacters { get; init; } = 20_000;
    public string? Password { get; init; }
}

/// <summary>Options for PDF conversion.</summary>
public sealed record PdfConvertRequest
{
    public required string TargetFormatId { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public PageRange? Pages { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for page rendering.</summary>
public sealed record PdfRenderRequest
{
    public required string TargetFormatId { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public PageRange? Pages { get; init; }
    public bool AllPages { get; init; }
    public int Dpi { get; init; } = 192;
    public string? Password { get; init; }
}

/// <summary>Options for creating one PDF from exactly one source family.</summary>
public sealed record NewPdfRequest
{
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public IReadOnlyList<string>? ImagePaths { get; init; }
    public string? HtmlPath { get; init; }

    /// <summary>Lets the HTML importer request the network resources the HTML names; each is disclosed.</summary>
    public bool AllowNetworkResources { get; init; }
    public string? TextPath { get; init; }
    public bool Markdown { get; init; }
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
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public bool PreserveBookmarks { get; init; } = true;
    public string? Password { get; init; }
}

/// <summary>Options for transactional PDF splitting.</summary>
public sealed record PdfSplitRequest
{
    public IReadOnlyList<PageRange>? PageGroups { get; init; }
    public int? Every { get; init; }
    public bool ByBookmarks { get; init; }
    public required string OutputDirectory { get; init; }
    public string NameTemplate { get; init; } = "{stem}.{n}.pdf";
    public bool Overwrite { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for bounded PDF extraction.</summary>
public sealed record PdfExtractRequest
{
    public required string What { get; init; }
    public required string OutputDirectory { get; init; }
    public PageRange? Pages { get; init; }
    public bool Overwrite { get; init; }
    public string? Password { get; init; }
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
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public string? BackupPath { get; init; }
    public EditCommandOptions Options { get; init; } = new();
    public string? Password { get; init; }
    /// <summary>The operations' secrets by the environment variable their <c>*Env</c> fields name.</summary>
    public IReadOnlyDictionary<string, string>? OpSecrets { get; init; }
}

public sealed record PdfFormReadRequest { public string? Password { get; init; } }
public sealed record PdfFormExportRequest
{
    public required string TargetFormatId { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public string? Password { get; init; }
}
public sealed record PdfSearchRequest
{
    public required string Pattern { get; init; }
    public bool Regex { get; init; }
    public bool CaseSensitive { get; init; }
    public PageRange? Pages { get; init; }
    public int MaxHits { get; init; } = 100;
    public string? Password { get; init; }
}
public sealed record PdfValidateRequest
{
    public required string Profile { get; init; }
    public string? Password { get; init; }
}

/// <summary>Options for applying one PKCS#7 PDF signature.</summary>
public sealed record PdfSignRequest
{
    public required string CertificatePath { get; init; }
    public required string CertificatePassword { get; init; }
    public required string OutputPath { get; init; }
    public bool Overwrite { get; init; }
    public string? Password { get; init; }
    public int Page { get; init; } = 1;
    public bool Visible { get; init; }
    public PdfSignatureRect? Rect { get; init; }
    public string? Reason { get; init; }
    public string? Location { get; init; }
    public string? Contact { get; init; }
}

/// <summary>A visible-signature rectangle in PDF points from the lower-left origin.</summary>
public sealed record PdfSignatureRect(double X, double Y, double Width, double Height);
