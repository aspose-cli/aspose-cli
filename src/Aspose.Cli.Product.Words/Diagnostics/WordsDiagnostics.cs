using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Words;

internal static class WordsDiagnostics
{
    internal static readonly ErrorCode BlockNotFound = ErrorCode.NotFound("BLOCK_NOT_FOUND");
    internal static readonly ErrorCode SectionNotFound = ErrorCode.NotFound("SECTION_NOT_FOUND");
    internal static readonly ErrorCode AnchorNotFound = ErrorCode.NotFound("ANCHOR_NOT_FOUND");
    internal static readonly ErrorCode MergeDataInvalid = Validation("MERGE_DATA_INVALID");
    internal static readonly ErrorCode DocumentProtected =
        new("DOCUMENT_PROTECTED", ExitCode.InputError);
    internal static readonly ErrorCode DocumentHasRevisions =
        new("DOCUMENT_HAS_REVISIONS", ExitCode.InputError);

    internal const string EncryptionRemoved = "DOCUMENT_ENCRYPTION_REMOVED";
    internal const string TrackedChangesPresent = "TRACKED_CHANGES_PRESENT";
    internal const string MacrosDropped = "MACROS_DROPPED";
    internal const string LayoutMayDiffer = "LAYOUT_MAY_DIFFER";
    internal const string LinkedImagesSkipped = "LINKED_IMAGES_SKIPPED";
    internal const string ProtectionNotEnforced = "PROTECTION_NOT_ENFORCED";
    internal const string MergeValueMissing = "MERGE_VALUE_MISSING";

    internal static readonly DiagnosticDescriptor FieldCountChanged = Verification("FIELD_COUNT_CHANGED");
    internal static readonly DiagnosticDescriptor RevisionCountChanged = Verification("REVISION_COUNT_CHANGED");
    internal static readonly DiagnosticDescriptor ProtectionChanged = Verification("PROTECTION_CHANGED");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(BlockNotFound, "validation"),
        Error(SectionNotFound, "validation"),
        Error(AnchorNotFound, "validation"),
        Error(MergeDataInvalid, "validation"),
        Error(DocumentProtected, "input"),
        Error(DocumentHasRevisions, "input"),
        Warning(EncryptionRemoved),
        Warning(TrackedChangesPresent),
        Warning(MacrosDropped),
        Warning(LayoutMayDiffer),
        Warning(LinkedImagesSkipped),
        Warning(ProtectionNotEnforced),
        Warning(MergeValueMissing),
        FieldCountChanged,
        RevisionCountChanged,
        ProtectionChanged,
    ];

    private static ErrorCode Validation(string code) =>
        new(code, ExitCode.ValidationError);

    private static DiagnosticDescriptor Error(ErrorCode code, string category) =>
        DiagnosticDescriptor.Error(code, "words", category);

    private static DiagnosticDescriptor Warning(string code) =>
        DiagnosticDescriptor.Warning(code, "words", "warning");

    private static DiagnosticDescriptor Verification(string code) =>
        DiagnosticDescriptor.Verification(code, "words");
}
