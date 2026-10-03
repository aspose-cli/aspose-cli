using Aspose.Cli.Sdk.Diagnostics;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells;

internal static class CellsDiagnostics
{
    internal static readonly ErrorCode SheetNotFound = ErrorCode.NotFound("SHEET_NOT_FOUND");
    internal static readonly ErrorCode NameNotFound = ErrorCode.NotFound("NAME_NOT_FOUND");
    internal static readonly ErrorCode ChartNotFound = ErrorCode.NotFound("CHART_NOT_FOUND");
    internal static readonly ErrorCode PivotNotFound = ErrorCode.NotFound("PIVOT_NOT_FOUND");
    internal static readonly ErrorCode CommentNotFound = ErrorCode.NotFound("COMMENT_NOT_FOUND");
    internal static readonly ErrorCode HyperlinkNotFound = ErrorCode.NotFound("HYPERLINK_NOT_FOUND");
    internal static readonly ErrorCode RangeInvalid =
        new("RANGE_INVALID", ExitCode.ValidationError);
    /// <summary>An explicit read range exceeds the --max-cells budget.</summary>
    internal static readonly ErrorCode RangeTooLarge =
        new("RANGE_TOO_LARGE", ExitCode.ValidationError);
    /// <summary>The selected sheet has no content to render.</summary>
    internal static readonly ErrorCode RenderEmpty =
        new("RENDER_EMPTY", ExitCode.ValidationError);

    internal const string SheetsDropped = "SHEETS_DROPPED";
    internal const string SheetsSkipped = "SHEETS_SKIPPED";
    internal const string DataTruncated = "DATA_TRUNCATED";
    internal const string MhtmlResourceCoverageUnverified = "MHTML_RESOURCE_COVERAGE_UNVERIFIED";
    internal const string FormulasBroken = "FORMULAS_BROKEN";
    internal const string EncryptionRemoved = "WORKBOOK_ENCRYPTION_REMOVED";
    internal const string FormulasCalculatedOnOpen = "FORMULAS_CALCULATED_ON_OPEN";
    internal const string SheetPartiallyRendered = "CELLS_SHEET_PARTIALLY_RENDERED";
    /// <summary>An evaluation save added its warning sheet and made it the active sheet.</summary>
    internal const string EvaluationSheetAdded = "EVALUATION_SHEET_ADDED";
    /// <summary>The input's active sheet is an evaluation warning sheet, so defaults use another sheet.</summary>
    internal const string EvaluationSheetSkipped = "EVALUATION_SHEET_SKIPPED";
    /// <summary>An evaluation save wrote its notice into a data output as content.</summary>
    internal const string EvaluationNoticeAdded = "EVALUATION_NOTICE_ADDED";

    /// <summary>A delimited text input has a preamble before its header, empty rows or a total row.</summary>
    internal const string TextTableLayout = "TEXT_TABLE_LAYOUT";

    /// <summary>A PDF export splits a chart across pages.</summary>
    internal const string ChartSplitAcrossPages = "CHART_SPLIT_ACROSS_PAGES";

    /// <summary>An import copied formulas that read a link without cached values, and their results changed.</summary>
    internal const string ExternalLinkCacheMissing = "EXTERNAL_LINK_CACHE_MISSING";

    /// <summary>An edit added a link that the output stores as a file name relative to its folder.</summary>
    internal const string ExternalLinkRelative = "EXTERNAL_LINK_RELATIVE";

    /// <summary>Verification: the edit changed more cells than verification lists.</summary>
    internal static readonly DiagnosticDescriptor DiffTruncated = DiagnosticDescriptor.Verification("DIFF_TRUNCATED", "cells");

    /// <summary>Verification: the edited workbook contains formula errors.</summary>
    internal static readonly DiagnosticDescriptor FormulaErrors = DiagnosticDescriptor.Verification("FORMULA_ERRORS", "cells");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        Error(SheetNotFound),
        Error(NameNotFound),
        Error(ChartNotFound),
        Error(PivotNotFound),
        Error(CommentNotFound),
        Error(HyperlinkNotFound),
        Error(RangeInvalid),
        Error(RangeTooLarge),
        Error(RenderEmpty),
        Warning(SheetsDropped),
        Warning(SheetsSkipped),
        Warning(DataTruncated),
        Warning(FormulasBroken),
        Warning(EncryptionRemoved),
        Warning(FormulasCalculatedOnOpen),
        Warning(SheetPartiallyRendered),
        Warning(MhtmlResourceCoverageUnverified),
        Warning(EvaluationSheetAdded),
        Warning(EvaluationSheetSkipped),
        Warning(EvaluationNoticeAdded),
        Warning(TextTableLayout),
        Warning(ExternalLinkCacheMissing),
        Warning(ExternalLinkRelative),
        Warning(ChartSplitAcrossPages),
        DiffTruncated,
        FormulaErrors,
    ];

    private static DiagnosticDescriptor Error(ErrorCode code) =>
        DiagnosticDescriptor.Error(code, "cells", "validation");

    private static DiagnosticDescriptor Warning(string code) =>
        DiagnosticDescriptor.Warning(code, "cells", "warning");
}
