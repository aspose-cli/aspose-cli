using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Words;

internal static class WordsDiagnostics
{
    internal static readonly ErrorCode BlockNotFound = ErrorCode.NotFound("BLOCK_NOT_FOUND");
    internal static readonly ErrorCode SectionNotFound = ErrorCode.NotFound("SECTION_NOT_FOUND");
    internal static readonly ErrorCode AnchorNotFound = ErrorCode.NotFound("ANCHOR_NOT_FOUND");
    internal static readonly ErrorCode RevisionNotFound = ErrorCode.NotFound("REVISION_NOT_FOUND");
    internal static readonly ErrorCode MergeDataInvalid = Validation("MERGE_DATA_INVALID");
    internal static readonly ErrorCode DocumentHasRevisions =
        new("DOCUMENT_HAS_REVISIONS", ExitCode.InputError);

    internal static readonly WarningCode EncryptionRemoved = new("DOCUMENT_ENCRYPTION_REMOVED");
    internal static readonly WarningCode TrackedChangesPresent = new("TRACKED_CHANGES_PRESENT");
    internal static readonly WarningCode MacrosDropped = new("MACROS_DROPPED");
    internal static readonly WarningCode LayoutMayDiffer = new("LAYOUT_MAY_DIFFER");
    internal static readonly WarningCode LinkedImagesSkipped = new("LINKED_IMAGES_SKIPPED");
    internal static readonly WarningCode MergeValueMissing = new("MERGE_VALUE_MISSING");
    /// <summary>A review operation named an author whose revisions or comments the document does not hold, so it changed nothing.</summary>
    internal static readonly WarningCode AuthorNoMatch = new("AUTHOR_NO_MATCH");

    internal static readonly DiagnosticDescriptor FieldCountChanged = Verification("FIELD_COUNT_CHANGED");
    internal static readonly DiagnosticDescriptor RevisionCountChanged = Verification("REVISION_COUNT_CHANGED");
    internal static readonly DiagnosticDescriptor ProtectionChanged = Verification("PROTECTION_CHANGED");
    internal static readonly DiagnosticDescriptor OutputTruncated = Verification("OUTPUT_TRUNCATED");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(BlockNotFound),
        Error(SectionNotFound),
        Error(AnchorNotFound),
        Error(RevisionNotFound),
        Error(MergeDataInvalid),
        Error(DocumentHasRevisions),
        Warning(EncryptionRemoved),
        Warning(TrackedChangesPresent),
        Warning(MacrosDropped),
        Warning(LayoutMayDiffer),
        Warning(LinkedImagesSkipped),
        Warning(MergeValueMissing),
        Warning(AuthorNoMatch),
        FieldCountChanged,
        RevisionCountChanged,
        ProtectionChanged,
        OutputTruncated,
    ];

    private static ErrorCode Validation(string code) =>
        new(code, ExitCode.ValidationError);

    private static DiagnosticDescriptor Error(ErrorCode code) =>
        DiagnosticDescriptor.Error(code, "words");

    private static DiagnosticDescriptor Warning(WarningCode code) =>
        DiagnosticDescriptor.Warning(code, "words");

    private static DiagnosticDescriptor Verification(string code) =>
        DiagnosticDescriptor.Verification(code, "words");
}
