using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Pdf;

internal static class PdfDiagnostics
{
    internal static readonly ErrorCode SignCertInvalid =
        new("SIGN_CERT_INVALID", ExitCode.InputError);
    internal static readonly ErrorCode FormXfaUnsupported =
        new("FORM_XFA_UNSUPPORTED", ExitCode.FormatError);

    internal const string PagesSkipped = "PAGES_SKIPPED";
    internal const string ScannedPagesSuspected = "SCANNED_PAGES_SUSPECTED";

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        DiagnosticDescriptor.Error(SignCertInvalid, "pdf", "input"),
        DiagnosticDescriptor.Error(FormXfaUnsupported, "pdf", "format"),
        DiagnosticDescriptor.Warning(PagesSkipped, "pdf", "warning"),
        DiagnosticDescriptor.Warning(ScannedPagesSuspected, "pdf", "warning"),
    ];
}
