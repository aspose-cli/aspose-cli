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

    /// <summary>No embedded file carries the requested attachment name.</summary>
    internal static readonly ErrorCode AttachmentNotFound = ErrorCode.NotFound("ATTACHMENT_NOT_FOUND");

    /// <summary>No AcroForm field carries the requested full name.</summary>
    internal static readonly ErrorCode FieldNotFound = ErrorCode.NotFound("FIELD_NOT_FOUND");

    internal static readonly WarningCode ScannedPagesSuspected = new("SCANNED_PAGES_SUSPECTED");

    /// <summary>Bookmarks, links or named destinations that no longer lead to their page.</summary>
    internal static readonly WarningCode NavigationDegraded = new("NAVIGATION_DEGRADED");

    /// <summary>An HTML import allowed network resources and the importer requested them.</summary>
    internal static readonly WarningCode NetworkResourcesRequested = new("NETWORK_RESOURCES_REQUESTED");

    /// <summary>A redact_text operation matched no text, so it redacted nothing.</summary>
    internal static readonly WarningCode RedactionNoMatch = new("REDACTION_NO_MATCH");

    /// <summary>A redaction moved the text that followed what it removed.</summary>
    internal static readonly WarningCode RedactionTextMoved = new("REDACTION_TEXT_MOVED");

    /// <summary>A form field of the output does not hold the value set_form_field set.</summary>
    internal static readonly DiagnosticDescriptor FieldValueMismatch = Verification("PDF_FIELD_VALUE_MISMATCH");

    /// <summary>The output still has a form field that flatten_forms flattened.</summary>
    internal static readonly DiagnosticDescriptor FieldNotFlattened = Verification("PDF_FIELD_NOT_FLATTENED");

    /// <summary>The output still contains text that redact_text redacted.</summary>
    internal static readonly DiagnosticDescriptor RedactedTextFound = Verification("PDF_REDACTED_TEXT_FOUND");

    /// <summary>The output's bookmarks differ from what the bookmark operations left.</summary>
    internal static readonly DiagnosticDescriptor BookmarkMismatch = Verification("PDF_BOOKMARK_MISMATCH");

    /// <summary>A document information entry differs from what set_metadata set.</summary>
    internal static readonly DiagnosticDescriptor MetadataMismatch = Verification("PDF_METADATA_MISMATCH");

    /// <summary>An attachment is missing, still present or a different size than the batch left it.</summary>
    internal static readonly DiagnosticDescriptor AttachmentMismatch = Verification("PDF_ATTACHMENT_MISMATCH");

    /// <summary>The output's page count differs from what the page operations left.</summary>
    internal static readonly DiagnosticDescriptor PageCountMismatch = Verification("PDF_PAGE_COUNT_MISMATCH");

    /// <summary>An operation's effect could not be read back, so it was not checked.</summary>
    internal static readonly DiagnosticDescriptor VerificationIncomplete = Verification("PDF_VERIFICATION_INCOMPLETE");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        FieldValueMismatch,
        FieldNotFlattened,
        RedactedTextFound,
        BookmarkMismatch,
        MetadataMismatch,
        AttachmentMismatch,
        PageCountMismatch,
        VerificationIncomplete,
        DiagnosticDescriptor.Error(SignCertInvalid, "pdf"),
        DiagnosticDescriptor.Error(FormXfaUnsupported, "pdf"),
        DiagnosticDescriptor.Error(PdfaConversionFailed, "pdf"),
        DiagnosticDescriptor.Error(AttachmentNotFound, "pdf"),
        DiagnosticDescriptor.Error(FieldNotFound, "pdf"),
        DiagnosticDescriptor.Warning(ScannedPagesSuspected, "pdf"),
        DiagnosticDescriptor.Warning(NavigationDegraded, "pdf"),
        DiagnosticDescriptor.Warning(NetworkResourcesRequested, "pdf"),
        DiagnosticDescriptor.Warning(RedactionNoMatch, "pdf"),
        DiagnosticDescriptor.Warning(RedactionTextMoved, "pdf"),
    ];

    private static DiagnosticDescriptor Verification(string code) =>
        DiagnosticDescriptor.Verification(code, "pdf");
}
