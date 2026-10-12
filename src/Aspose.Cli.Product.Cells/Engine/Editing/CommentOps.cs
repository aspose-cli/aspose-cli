using Aspose.Cells;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Cell comments (notes) for review workflows.</summary>
internal static class CommentOps
{
    /// <summary>
    /// Adds a note to a cell: a cell without a comment gets one, and a cell that has one gets the
    /// text as a new paragraph of it, so each add keeps what earlier ones wrote.
    /// </summary>
    public static void AddComment(Worksheet sheet, AddCommentOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        if (sheet.Comments[cell.Row, cell.Column] is { } existing)
        {
            existing.Note = existing.Note + "\n" + op.Text;
            if (op.Author is { } addedBy)
            {
                existing.Author = addedBy;
            }

            return;
        }

        Comment comment = sheet.Comments[sheet.Comments.Add(cell.Row, cell.Column)];
        comment.Note = op.Text;
        if (op.Author is { } author)
        {
            comment.Author = author;
        }
    }

    public static void EditComment(Worksheet sheet, SetCommentOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        Comment comment = sheet.Comments[cell.Row, cell.Column]
            ?? throw NotFound(sheet, op.Cell, $"set_comment changes a comment the cell has; to add one to {op.Cell}, use add_comment.");

        comment.Note = op.Text;
        if (op.Author is { } author)
        {
            comment.Author = author;
        }
    }

    public static void DeleteComment(Worksheet sheet, DeleteCommentOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        if (sheet.Comments[cell.Row, cell.Column] is null)
        {
            throw NotFound(sheet, op.Cell);
        }

        sheet.Comments.RemoveAt(cell.Row, cell.Column);
    }

    /// <summary>
    /// <c>COMMENT_NOT_FOUND</c> for a cell without a comment, listing the cells of the sheet
    /// that carry one in row-major order.
    /// </summary>
    private static CliException NotFound(Worksheet sheet, string cell, string? hint = null)
    {
        var commented = new List<CellRef>(sheet.Comments.Count);
        foreach (Comment comment in sheet.Comments)
        {
            commented.Add(new CellRef(comment.Row, comment.Column));
        }

        string[] cells = commented
            .OrderBy(static at => at.Row)
            .ThenBy(static at => at.Column)
            .Select(A1.FormatCell)
            .ToArray();
        return CliErrors.NotFound(CellsDiagnostics.CommentNotFound, "comment", cell, cells, hint);
    }
}
