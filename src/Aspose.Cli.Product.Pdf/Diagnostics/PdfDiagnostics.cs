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

    /// <summary>
    /// An operation removed or set document information that the engine writes into every PDF it
    /// saves, so the output still holds it.
    /// </summary>
    internal static readonly WarningCode MetadataRewritten = new("METADATA_REWRITTEN");

    /// <summary>A redaction moved the text that followed what it removed.</summary>
    internal static readonly WarningCode RedactionTextMoved = new("REDACTION_TEXT_MOVED");

    /// <summary>A redaction's cover overlapped text beside what it selected, and removed it too.</summary>
    internal static readonly WarningCode RedactionNearbyTextRemoved = new("REDACTION_NEARBY_TEXT_REMOVED");

    /// <summary>A raster image is drawn larger than its pixels allow, at fewer than 96 of them per inch, so it shows blurred.</summary>
    internal static readonly WarningCode ImageResolutionLow = new("IMAGE_RESOLUTION_LOW");

    /// <summary>The signature appearance made the engine embed a whole font, so the file grew by its size (PDF-SIGN-FULL-FONT).</summary>
    internal static readonly WarningCode SignatureFontEmbedded = new("SIGNATURE_FONT_EMBEDDED");

    /// <summary>The visible signature's rectangle cannot hold its text at the smallest font size, so the appearance clips it.</summary>
    internal static readonly WarningCode SignatureTextClipped = new("SIGNATURE_TEXT_CLIPPED");

    /// <summary>A table extraction found no table on the selected pages.</summary>
    internal static readonly WarningCode TablesNotFound = new("TABLES_NOT_FOUND");

    /// <summary>A form field of the output does not hold the value set_form_field set.</summary>
    internal static readonly DiagnosticDescriptor FieldValueMismatch = DiagnosticDescriptor.Verification("FIELD_VALUE_MISMATCH");

    /// <summary>The output still has a form field that flatten_forms flattened.</summary>
    internal static readonly DiagnosticDescriptor FieldNotFlattened = DiagnosticDescriptor.Verification("FIELD_NOT_FLATTENED");

    /// <summary>The output still contains text that redact_text redacted.</summary>
    internal static readonly DiagnosticDescriptor RedactedTextFound = DiagnosticDescriptor.Verification("REDACTED_TEXT_FOUND");

    /// <summary>The output's bookmarks differ from what the bookmark operations left.</summary>
    internal static readonly DiagnosticDescriptor BookmarkMismatch = DiagnosticDescriptor.Verification("BOOKMARK_MISMATCH");

    /// <summary>A document property differs from what set_properties set.</summary>
    internal static readonly DiagnosticDescriptor PropertiesMismatch = DiagnosticDescriptor.Verification("PROPERTIES_MISMATCH");

    /// <summary>An attachment is missing, still present or a different size than the batch left it.</summary>
    internal static readonly DiagnosticDescriptor AttachmentMismatch = DiagnosticDescriptor.Verification("ATTACHMENT_MISMATCH");

    /// <summary>The output's page count differs from what the page operations left.</summary>
    internal static readonly DiagnosticDescriptor PageCountMismatch = DiagnosticDescriptor.Verification("PAGE_COUNT_MISMATCH");

    /// <summary>A PDF/A check found a problem: the issue code of <c>pdf validate</c>.</summary>
    internal static readonly DiagnosticDescriptor PdfaNonconformance = DiagnosticDescriptor.Verification(PdfComplianceIssue.NonconformanceCode);

    /// <summary>An operation's effect could not be read back, so it was not checked.</summary>
    internal static readonly DiagnosticDescriptor VerificationIncomplete = DiagnosticDescriptor.Verification("VERIFICATION_INCOMPLETE");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        PdfaNonconformance,
        FieldValueMismatch,
        FieldNotFlattened,
        RedactedTextFound,
        BookmarkMismatch,
        PropertiesMismatch,
        AttachmentMismatch,
        PageCountMismatch,
        VerificationIncomplete,
        DiagnosticDescriptor.Error(SignCertInvalid),
        DiagnosticDescriptor.Error(FormXfaUnsupported),
        DiagnosticDescriptor.Error(PdfaConversionFailed),
        DiagnosticDescriptor.Error(AttachmentNotFound),
        DiagnosticDescriptor.Error(FieldNotFound),
        DiagnosticDescriptor.Warning(ScannedPagesSuspected),
        DiagnosticDescriptor.Warning(NavigationDegraded),
        DiagnosticDescriptor.Warning(NetworkResourcesRequested),
        DiagnosticDescriptor.Warning(RedactionTextMoved),
        DiagnosticDescriptor.Warning(RedactionNearbyTextRemoved),
        DiagnosticDescriptor.Warning(MetadataRewritten),
        DiagnosticDescriptor.Warning(ImageResolutionLow),
        DiagnosticDescriptor.Warning(SignatureFontEmbedded),
        DiagnosticDescriptor.Warning(SignatureTextClipped),
        DiagnosticDescriptor.Warning(TablesNotFound),
    ];
}
