namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Accepted values of the status of a changed sheet.</summary>
public static class CellsSheetChangeStatuses
{
    /// <summary>The sheet is present only on the right.</summary>
    public const string Added = "added";

    /// <summary>The sheet is present only on the left.</summary>
    public const string Removed = "removed";

    /// <summary>Both sides hold the sheet under one name and its cells differ.</summary>
    public const string Modified = "modified";

    /// <summary>Both sides hold the sheet under different names, paired by its internal id.</summary>
    public const string Renamed = "renamed";
}
