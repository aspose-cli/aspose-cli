using Aspose.Cells;
using Aspose.Cells.Drawing;
using Aspose.Cli.Sdk.Tables;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// The inventory of a workbook: how many objects of each kind of content it holds, counted with
/// the engine's own collections, and whether <c>read</c> lists them. An edit reports the counts
/// that changed; a read reports all of them.
/// </summary>
internal static class CellsInventory
{
    /// <summary>The kinds the inventory counts, in the order a result lists them; read once the table's kinds exist.</summary>
    internal static IReadOnlyList<InventoryKind> Kinds => Declared.Value;

    private static class Declared
    {
        internal static readonly IReadOnlyList<InventoryKind> Value =
        [
            new("sheets", "The sheets: Workbook.Worksheets.Count.", CellsTable.SheetKind),
            new("cells", "The cells the engine holds, of every sheet: Cells.CountLarge.", CellsTable.CellKind),
            new("formulaCells", "The cells of every sheet whose Cell.IsFormula is true.", CellsTable.CellKind),
            new("rows", "The rows the engine holds, of every sheet: Cells.Rows.Count.", CellsTable.RowKind),
            new("columns", "The columns the engine holds, of every sheet: Cells.Columns.Count.", CellsTable.ColumnKind),
            new("names", "The defined names: Workbook.Worksheets.Names.Count.", CellsTable.NameKind),
            new("mergedAreas", "The merged areas of every sheet: Cells.GetMergedAreas().Length.", CellsTable.MergeKind),
            new("comments", "The comments of every sheet: Worksheet.Comments.Count.", CellsTable.CommentKind),
            new("threadedComments", "The threaded comments of every sheet's comments: Comment.ThreadedComments.Count.", Kind: null),
            new("hyperlinks", "The hyperlinks of every sheet: Worksheet.Hyperlinks.Count.", CellsTable.HyperlinkKind),
            new("validations", "The data validations of every sheet: Worksheet.Validations.Count.", CellsTable.ValidationKind),
            new("conditionalFormattings", "The conditional formattings of every sheet: Worksheet.ConditionalFormattings.Count.", CellsTable.ConditionalFormattingKind),
            new("tables", "The tables of every sheet: Worksheet.ListObjects.Count.", CellsTable.TableKind),
            new("charts", "The charts of every sheet: Worksheet.Charts.Count.", CellsTable.ChartKind),
            new("pivotTables", "The pivot tables of every sheet: Worksheet.PivotTables.Count.", CellsTable.PivotKind),
            new("pictures", "The pictures of every sheet: Worksheet.Pictures.Count.", CellsTable.PictureKind),
            new("sparklineGroups", "The sparkline groups of every sheet: Worksheet.SparklineGroups.Count.", CellsTable.SparklineGroupKind),
            new("otherShapes", "The shapes of every sheet that are no ChartShape, Picture or CommentShape, such as text boxes and groups.", Kind: null),
            new("oleObjects", "The OLE objects of every sheet: Worksheet.OleObjects.Count.", Kind: null),
            new("slicers", "The slicers of every sheet: Worksheet.Slicers.Count.", Kind: null),
            new("queryTables", "The query tables of every sheet: Worksheet.QueryTables.Count.", Kind: null),
            new("externalLinks", "The links to other workbooks: Workbook.Worksheets.ExternalLinks.Count.", Kind: null),
            new("dataConnections", "The data connections: Workbook.DataConnections.Count.", Kind: null),
            new("vbaProjects", "1 when Workbook.HasMacro is true, else 0.", Kind: null),
            new("customProperties", "The custom document properties: Workbook.CustomDocumentProperties.Count.", Kind: null),
        ];
    }

    /// <summary>Counts every kind.</summary>
    internal static IReadOnlyDictionary<string, long> Count(Workbook workbook)
    {
        Worksheet[] sheets = [.. workbook.Worksheets];
        return new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["sheets"] = sheets.Length,
            ["cells"] = sheets.Sum(static sheet => sheet.Cells.CountLarge),
            ["formulaCells"] = sheets.Sum(static sheet => sheet.Cells.Cast<Cell>().LongCount(static cell => cell.IsFormula)),
            ["rows"] = sheets.Sum(static sheet => (long)sheet.Cells.Rows.Count),
            ["columns"] = sheets.Sum(static sheet => (long)sheet.Cells.Columns.Count),
            ["names"] = workbook.Worksheets.Names.Count,
            ["mergedAreas"] = sheets.Sum(static sheet => (long)sheet.Cells.GetMergedAreas().Length),
            ["comments"] = sheets.Sum(static sheet => (long)sheet.Comments.Count),
            ["threadedComments"] = sheets.Sum(static sheet => sheet.Comments.Cast<Comment>().Sum(static comment => (long)comment.ThreadedComments.Count)),
            ["hyperlinks"] = sheets.Sum(static sheet => (long)sheet.Hyperlinks.Count),
            ["validations"] = sheets.Sum(static sheet => (long)sheet.Validations.Count),
            ["conditionalFormattings"] = sheets.Sum(static sheet => (long)sheet.ConditionalFormattings.Count),
            ["tables"] = sheets.Sum(static sheet => (long)sheet.ListObjects.Count),
            ["charts"] = sheets.Sum(static sheet => (long)sheet.Charts.Count),
            ["pivotTables"] = sheets.Sum(static sheet => (long)sheet.PivotTables.Count),
            ["pictures"] = sheets.Sum(static sheet => (long)sheet.Pictures.Count),
            ["sparklineGroups"] = sheets.Sum(static sheet => (long)sheet.SparklineGroups.Count),
            ["otherShapes"] = sheets.Sum(static sheet => sheet.Shapes.Cast<Shape>().LongCount(static shape => shape is not (ChartShape or Picture or CommentShape))),
            ["oleObjects"] = sheets.Sum(static sheet => (long)sheet.OleObjects.Count),
            ["slicers"] = sheets.Sum(static sheet => (long)sheet.Slicers.Count),
            ["queryTables"] = sheets.Sum(static sheet => (long)sheet.QueryTables.Count),
            ["externalLinks"] = workbook.Worksheets.ExternalLinks.Count,
            ["dataConnections"] = workbook.DataConnections.Count,
            ["vbaProjects"] = workbook.HasMacro ? 1 : 0,
            ["customProperties"] = workbook.CustomDocumentProperties.Count,
        };
    }
}
