namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Supported Cells preview views.</summary>
public static class CellsPreviewViews
{
    /// <summary>Interactive preview of the whole workbook.</summary>
    public const string Workbook = "workbook";

    /// <summary>Pixel-accurate preview of the active worksheet.</summary>
    public const string Sheet = "sheet";

    /// <summary>Every preview view in this build.</summary>
    public static IReadOnlyList<string> All { get; } = [Workbook, Sheet];
}
