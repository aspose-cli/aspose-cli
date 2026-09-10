using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Words;

internal static class WordsDiagnostics
{
    internal static readonly ErrorCode BlockNotFound = Validation("BLOCK_NOT_FOUND");
    internal static readonly ErrorCode SectionNotFound = Validation("SECTION_NOT_FOUND");
    internal static readonly ErrorCode BookmarkNotFound = Validation("BOOKMARK_NOT_FOUND");
    internal static readonly ErrorCode AnchorNotFound = Validation("ANCHOR_NOT_FOUND");
    internal static readonly ErrorCode StyleNotFound = Validation("STYLE_NOT_FOUND");
    internal static readonly ErrorCode MergeDataInvalid = Validation("MERGE_DATA_INVALID");
    internal static readonly ErrorCode DocumentProtected =
        new("DOCUMENT_PROTECTED", ExitCode.InputError);
    internal static readonly ErrorCode DocumentHasRevisions =
        new("DOCUMENT_HAS_REVISIONS", ExitCode.InputError);

    internal const string TrackedChangesPresent = "TRACKED_CHANGES_PRESENT";
    internal const string MacrosDropped = "MACROS_DROPPED";
    internal const string LayoutMayDiffer = "LAYOUT_MAY_DIFFER";

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(BlockNotFound, "validation"),
        Error(SectionNotFound, "validation"),
        Error(BookmarkNotFound, "validation"),
        Error(AnchorNotFound, "validation"),
        Error(StyleNotFound, "validation"),
        Error(MergeDataInvalid, "validation"),
        Error(DocumentProtected, "input"),
        Error(DocumentHasRevisions, "input"),
        Warning(TrackedChangesPresent),
        Warning(MacrosDropped),
        Warning(LayoutMayDiffer),
    ];

    private static ErrorCode Validation(string code) =>
        new(code, ExitCode.ValidationError);

    private static DiagnosticDescriptor Error(ErrorCode code, string category) =>
        DiagnosticDescriptor.Error(code, "words", category);

    private static DiagnosticDescriptor Warning(string code) =>
        DiagnosticDescriptor.Warning(code, "words", "warning");
}
