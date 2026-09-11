namespace Aspose.Cli.Product.Cells.Ports;

/// <summary>Product-internal layout facts used only by the Cells review adapter.</summary>
internal interface IWorkbookReviewPort
{
    WorkbookReviewLayout InspectReviewLayout(string filePath, string? password);
}

internal sealed record WorkbookReviewLayout(
    IReadOnlyList<WorksheetReviewLayout> Sheets)
{
    public IReadOnlyList<Aspose.Cli.Sdk.Contracts.Warning>? Warnings { get; init; }
}

internal sealed record WorksheetReviewLayout
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

    public required IReadOnlyList<DimensionReviewIssue> DimensionIssues { get; init; }

    public required string? PrintArea { get; init; }

    public required bool PrintAreaInvalid { get; init; }

    public required bool PrintAreaExcludesContent { get; init; }

    public required bool PrintAreaExcessive { get; init; }

    public required IReadOnlyList<ChartReviewLayout> Charts { get; init; }
}

internal sealed record DimensionReviewIssue(
    string Kind,
    int Index,
    double Size);

internal sealed record ChartReviewLayout
{
    public required string Name { get; init; }

    public required bool Hidden { get; init; }

    public required int WidthPixels { get; init; }

    public required int HeightPixels { get; init; }

    public required int SeriesCount { get; init; }

    public required bool AnchoredInHiddenCells { get; init; }

    public required bool ExcludedByPrintArea { get; init; }
}
