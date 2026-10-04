using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Words;

/// <summary>Every check a Words review can report, declared once.</summary>
internal static class WordsReviewChecks
{
    public static ReviewCheck DocumentEmpty { get; } = new(
        "WORDS_DOCUMENT_EMPTY",
        ReviewSeverities.Warning,
        "The document has no readable text, tables or images.");

    public static ReviewCheck RevisionsPresent { get; } = new(
        "WORDS_REVISIONS_PRESENT",
        ReviewSeverities.Info,
        "The document contains tracked revisions, which a visual-only repair must not accept.");

    public static ReviewCheck CommentsPresent { get; } = new(
        "WORDS_COMMENTS_PRESENT",
        ReviewSeverities.Info,
        "The document contains comments, which a delivered document usually should not keep.");

    public static ReviewCheck PageBlank { get; } = new(
        "WORDS_PAGE_BLANK",
        ReviewSeverities.Warning,
        "A laid-out page has no visible body text, table rows or drawing objects.");

    public static ReviewCheck PageUtilizationLow { get; } = new(
        "WORDS_PAGE_UTILIZATION_LOW",
        ReviewSeverities.Warning,
        "A page after the first holds only a sliver of visible content, such as a stranded line.");

    public static ReviewCheck TextTooSmall { get; } = new(
        "WORDS_TEXT_TOO_SMALL",
        ReviewSeverities.Warning,
        "Visible body text on a page is smaller than 7 pt.");

    public static ReviewCheck TextTooLarge { get; } = new(
        "WORDS_TEXT_TOO_LARGE",
        ReviewSeverities.Warning,
        "Visible body text on a page is larger than 72 pt.");

    public static ReviewCheck ObjectOutsidePage { get; } = new(
        "WORDS_OBJECT_OUTSIDE_PAGE",
        ReviewSeverities.Warning,
        "A drawing object extends beyond the physical page boundary.");

    public static ReviewCheck PageBreaksExcessive { get; } = new(
        "WORDS_PAGE_BREAKS_EXCESSIVE",
        ReviewSeverities.Warning,
        "A page contains more than one explicit page-break control.");

    public static ReviewCheck HeadingOrphaned { get; } = new(
        "WORDS_HEADING_ORPHANED",
        ReviewSeverities.Warning,
        "A heading ends a page after prior content while its body starts on the next page.");

    public static ReviewCheck EvaluationMarks { get; } = new(
        "WORDS_EVALUATION_MARKS",
        ReviewSeverities.Warning,
        "The document holds the banner or footer text that a save without a license wrote into the file, such as 'Created with an evaluation copy of Aspose.Words'; a license does not remove it. Checked only with a license.");

    public static IReadOnlyList<ReviewCheck> All { get; } =
    [
        DocumentEmpty,
        RevisionsPresent,
        CommentsPresent,
        PageBlank,
        PageUtilizationLow,
        TextTooSmall,
        TextTooLarge,
        ObjectOutsidePage,
        PageBreaksExcessive,
        HeadingOrphaned,
        EvaluationMarks,
    ];
}
