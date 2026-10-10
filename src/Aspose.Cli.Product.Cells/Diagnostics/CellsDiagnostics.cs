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

    internal static readonly WarningCode SheetsDropped = new("SHEETS_DROPPED");
    internal static readonly WarningCode SheetsSkipped = new("SHEETS_SKIPPED");
    internal static readonly WarningCode DataTruncated = new("DATA_TRUNCATED");
    internal static readonly WarningCode MhtmlResourceCoverageUnverified = new("MHTML_RESOURCE_COVERAGE_UNVERIFIED");
    internal static readonly WarningCode FormulasBroken = new("FORMULAS_BROKEN");
    internal static readonly WarningCode EncryptionRemoved = new("WORKBOOK_ENCRYPTION_REMOVED");
    internal static readonly WarningCode FormulasCalculatedOnOpen = new("FORMULAS_CALCULATED_ON_OPEN");
    internal static readonly WarningCode SheetPartiallyRendered = new("CELLS_SHEET_PARTIALLY_RENDERED");
    /// <summary>The active sheet is an evaluation warning sheet, so a command that names no sheet used another one.</summary>
    internal static readonly WarningCode ActiveSheetSkipped = new("ACTIVE_SHEET_SKIPPED");

    /// <summary>A delimited text input has a preamble before its header, empty rows or a total row.</summary>
    internal static readonly WarningCode TextTableLayout = new("TEXT_TABLE_LAYOUT");

    /// <summary>A PDF export splits a chart across pages.</summary>
    internal static readonly WarningCode ChartSplitAcrossPages = new("CHART_SPLIT_ACROSS_PAGES");

    /// <summary>
    /// How to keep a chart on one page, shared by the conversion warning and the review finding,
    /// after "Fit the sheet on fewer pages with" and the operation.
    /// </summary>
    internal const string ChartSplitRemedy = "(fitToWidth 1 and fitToHeight 0, or orientation landscape), or move or resize the chart";

    /// <summary>An import copied formulas that read a link without cached values, and their results changed.</summary>
    internal static readonly WarningCode ExternalLinkCacheMissing = new("EXTERNAL_LINK_CACHE_MISSING");

    /// <summary>An edit added a link that the output stores as a file name relative to its folder.</summary>
    internal static readonly WarningCode ExternalLinkRelative = new("EXTERNAL_LINK_RELATIVE");

    /// <summary>An edit wrote formulas that call functions the engine does not know.</summary>
    internal static readonly WarningCode FormulaFunctionUnknown = new("FORMULA_FUNCTION_UNKNOWN");

    /// <summary>A comparison found rows one side inserted or deleted, which shift the cells below them.</summary>
    internal static readonly WarningCode RowsShifted = new("ROWS_SHIFTED");

    /// <summary>Verification: the edited workbook contains formula errors.</summary>
    internal static readonly DiagnosticDescriptor FormulaErrors = DiagnosticDescriptor.Verification("FORMULA_ERRORS");

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        DiagnosticDescriptor.Error(SheetNotFound),
        DiagnosticDescriptor.Error(NameNotFound),
        DiagnosticDescriptor.Error(ChartNotFound),
        DiagnosticDescriptor.Error(PivotNotFound),
        DiagnosticDescriptor.Error(CommentNotFound),
        DiagnosticDescriptor.Error(HyperlinkNotFound),
        DiagnosticDescriptor.Error(RangeInvalid),
        DiagnosticDescriptor.Error(RangeTooLarge),
        DiagnosticDescriptor.Error(RenderEmpty),
        DiagnosticDescriptor.Warning(SheetsDropped),
        DiagnosticDescriptor.Warning(SheetsSkipped),
        DiagnosticDescriptor.Warning(DataTruncated),
        DiagnosticDescriptor.Warning(FormulasBroken),
        DiagnosticDescriptor.Warning(EncryptionRemoved),
        DiagnosticDescriptor.Warning(FormulasCalculatedOnOpen),
        DiagnosticDescriptor.Warning(SheetPartiallyRendered),
        DiagnosticDescriptor.Warning(ActiveSheetSkipped),
        DiagnosticDescriptor.Warning(MhtmlResourceCoverageUnverified),
        DiagnosticDescriptor.Warning(TextTableLayout),
        DiagnosticDescriptor.Warning(ExternalLinkCacheMissing),
        DiagnosticDescriptor.Warning(ExternalLinkRelative),
        DiagnosticDescriptor.Warning(FormulaFunctionUnknown),
        DiagnosticDescriptor.Warning(ChartSplitAcrossPages),
        DiagnosticDescriptor.Warning(RowsShifted),
        FormulaErrors,
    ];
}
