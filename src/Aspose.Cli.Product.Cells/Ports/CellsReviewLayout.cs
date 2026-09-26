namespace Aspose.Cli.Product.Cells.Ports;

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

    public required int HiddenPopulatedColumns { get; init; }

    public required int NarrowPopulatedColumns { get; init; }

    public required int WidePopulatedColumns { get; init; }

    public required int HiddenPopulatedRows { get; init; }

    public required int ShortPopulatedRows { get; init; }

    public required int TallPopulatedRows { get; init; }

    public required IReadOnlyList<CellsReviewDimensionIssue> DimensionIssues { get; init; }

    public required string? PrintArea { get; init; }

    public required bool PrintAreaInvalid { get; init; }

    public required bool PrintAreaExcludesContent { get; init; }

    public required bool PrintAreaExcessive { get; init; }

    public required IReadOnlyList<CellsReviewChartLayout> Charts { get; init; }
}

/// <summary>A populated row or column that is hidden, or unusually small or large.</summary>
internal sealed record CellsReviewDimensionIssue(
    string Kind,
    int Index,
    double Size);

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
}
