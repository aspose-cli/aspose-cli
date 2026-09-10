using Aspose.Cells;
using Aspose.Cli.Product.Cells.Addressing;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

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
        Comment? comment = sheet.Comments[cell.Row, cell.Column];
        if (comment is null)
        {
            throw CellsErrors.OpsInvalid($"no comment on cell '{op.Cell}' to edit");
        }

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
            throw CellsErrors.OpsInvalid($"no comment on cell '{op.Cell}' to delete");
        }

        sheet.Comments.RemoveAt(cell.Row, cell.Column);
        return null;
    }
}
