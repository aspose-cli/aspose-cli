using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Pdf;

internal static class PdfDiagnostics
{
    internal static readonly ErrorCode SignCertInvalid =
        new("SIGN_CERT_INVALID", ExitCode.InputError);
    internal static readonly ErrorCode FormXfaUnsupported =
        new("FORM_XFA_UNSUPPORTED", ExitCode.FormatError);

    /// <summary>The SDK could not make the document conform to the requested PDF/A profile.</summary>
    internal static readonly ErrorCode PdfaConversionFailed =
        new("PDFA_CONVERSION_FAILED", ExitCode.FormatError);

    internal const string ScannedPagesSuspected = "SCANNED_PAGES_SUSPECTED";

    /// <summary>Bookmarks, links or named destinations that no longer lead to their page.</summary>
    internal const string NavigationDegraded = "NAVIGATION_DEGRADED";

    /// <summary>An HTML import allowed network resources and the importer requested them.</summary>
    internal const string NetworkResourcesRequested = "NETWORK_RESOURCES_REQUESTED";

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        DiagnosticDescriptor.Error(SignCertInvalid, "pdf", "input"),
        DiagnosticDescriptor.Error(FormXfaUnsupported, "pdf", "format"),
        DiagnosticDescriptor.Error(PdfaConversionFailed, "pdf", "format"),
        DiagnosticDescriptor.Warning(ScannedPagesSuspected, "pdf", "warning"),
        DiagnosticDescriptor.Warning(NavigationDegraded, "pdf", "warning"),
        DiagnosticDescriptor.Warning(NetworkResourcesRequested, "pdf", "warning"),
    ];
}
