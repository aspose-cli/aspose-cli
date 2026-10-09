namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Bounded layout facts from the real workbook model, one entry per worksheet.</summary>
internal sealed record CellsReviewLayout(
    IReadOnlyList<CellsReviewSheetLayout> Sheets)
{
    public IReadOnlyList<Aspose.Cli.Sdk.Contracts.Warning>? Warnings { get; init; }
}

/// <summary>Layout facts for one worksheet.</summary>
internal sealed record CellsReviewSheetLayout
{
    public required string Name { get; init; }

    public required long UsedAreaCells { get; init; }

    public required long PopulatedCells { get; init; }

    public required string? ContentRange { get; init; }

    public required bool HasVisualObjects { get; init; }

    /// <summary>Whether the sheet is the warning sheet an evaluation save added.</summary>
    public required bool IsEvaluationWarning { get; init; }

    /// <summary>The zero-based last row when it holds only the evaluation notice, as an evaluation CSV or TSV export ends.</summary>
    public required int? EvaluationNoticeRow { get; init; }

    public required CellsReviewDimensionSet HiddenPopulatedColumns { get; init; }

    public required CellsReviewDimensionSet NarrowPopulatedColumns { get; init; }

    public required CellsReviewDimensionSet WidePopulatedColumns { get; init; }

    public required CellsReviewDimensionSet HiddenPopulatedRows { get; init; }

    public required CellsReviewDimensionSet ShortPopulatedRows { get; init; }

    public required CellsReviewDimensionSet TallPopulatedRows { get; init; }

    /// <summary>Cells whose value is wider than its column and shows cut off or as ####.</summary>
    public required CellsReviewCellSet ClippedCells { get; init; }

    /// <summary>Text cells wider than their column that spill over the empty cell to their right.</summary>
    public required CellsReviewCellSet OverflowingCells { get; init; }

    public required string? PrintArea { get; init; }

    public required bool PrintAreaInvalid { get; init; }

    public required bool PrintAreaExcludesContent { get; init; }

    public required bool PrintAreaExcessive { get; init; }

    public required IReadOnlyList<CellsReviewChartLayout> Charts { get; init; }
}

/// <summary>
/// The populated rows or columns of a worksheet in one layout condition: how many there are and
/// the zero-based indexes of the first few.
/// </summary>
internal sealed record CellsReviewDimensionSet(
    int Count,
    IReadOnlyList<int> Samples);

/// <summary>
/// Cells in one layout condition: how many there are, the A1 names of the first few, and whether
/// one of those holds East Asian text.
/// </summary>
internal sealed record CellsReviewCellSet(
    int Count,
    IReadOnlyList<string> Samples,
    bool SamplesHaveEastAsianText = false);

/// <summary>Layout facts for one chart on a worksheet.</summary>
internal sealed record CellsReviewChartLayout
{
    public required string Name { get; init; }

    public required bool Hidden { get; init; }

    public required int WidthPixels { get; init; }

    public required int HeightPixels { get; init; }

    public required int SeriesCount { get; init; }

    public required bool AnchoredInHiddenCells { get; init; }

    public required bool ExcludedByPrintArea { get; init; }

    /// <summary>The number of printed pages the chart reaches; more than one splits it in a PDF.</summary>
    public required int PrintedPages { get; init; }
}
