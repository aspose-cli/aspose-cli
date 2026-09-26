using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Editing;

/// <summary>Cell comments (notes) for review workflows.</summary>
internal static class CommentOps
{
    public static long? AddComment(Worksheet sheet, AddCommentOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        Comment comment = sheet.Comments[sheet.Comments.Add(cell.Row, cell.Column)];
        comment.Note = op.Text;
        if (op.Author is { } author)
        {
            comment.Author = author;
        }

        return null;
    }

    public static long? EditComment(Worksheet sheet, EditCommentOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        Comment comment = sheet.Comments[cell.Row, cell.Column] ?? throw NotFound(sheet, op.Cell);

        comment.Note = op.Text;
        if (op.Author is { } author)
        {
            comment.Author = author;
        }

        return null;
    }

    public static long? DeleteComment(Worksheet sheet, DeleteCommentOp op)
    {
        CellRef cell = A1.ParseCell(op.Cell);
        if (sheet.Comments[cell.Row, cell.Column] is null)
        {
            throw NotFound(sheet, op.Cell);
        }

        sheet.Comments.RemoveAt(cell.Row, cell.Column);
        return null;
    }

    /// <summary>
    /// <c>COMMENT_NOT_FOUND</c> for a cell without a comment, listing the cells of the sheet
    /// that carry one in row-major order.
    /// </summary>
    private static CliException NotFound(Worksheet sheet, string cell)
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
        return CliErrors.NotFound(CellsDiagnostics.CommentNotFound, "comment", cell, cells);
    }
}
